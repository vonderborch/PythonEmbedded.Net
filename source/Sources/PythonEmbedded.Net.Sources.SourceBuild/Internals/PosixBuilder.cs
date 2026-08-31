using Microsoft.Extensions.Logging;
using PythonEmbedded.Net.Exceptions;
using PythonEmbedded.Net.Models;

namespace PythonEmbedded.Net.Sources.SourceBuild.Internals;

/// <summary>
/// Configures, compiles and installs CPython on macOS and Linux.
/// <para>
/// The install is staged the way python-build-standalone stages it: <c>--prefix=/install</c> plus
/// <c>make install DESTDIR=…</c>. That bakes the literal prefix <c>/install</c> into
/// <c>_sysconfigdata_*.py</c>, which is exactly what the core <c>SysconfigPatcher</c> rewrites once the
/// host moves the staged tree into place — so a source build lands in the same shape as a downloaded
/// archive and needs no special handling anywhere else in the library.
/// </para>
/// </summary>
internal static class PosixBuilder
{
    /// <summary>The prefix baked into the build; never a real directory, always rewritten after the move.</summary>
    internal const string StagedPrefix = "/install";

    public static async Task BuildAsync(
        BuildOptions options, BuildPaths paths, ToolchainEnvironment toolchain, BuildLog log, CancellationToken ct)
    {
        Directory.CreateDirectory(paths.BuildDirectory);

        string configure = Path.Combine(paths.SourceDirectory, "configure");
        if (!File.Exists(configure))
        {
            throw new PythonException(
                PythonErrorKind.InstallFailed,
                $"The unpacked source tree at '{paths.SourceDirectory}' has no configure script.");
        }

        EnsureExecutable(configure);

        Dictionary<string, string> environment = ComposeEnvironment(options, toolchain);
        IReadOnlyList<string> arguments = ComposeConfigureArguments(options, toolchain);

        await log.RunAsync("configure", configure, arguments, paths.BuildDirectory, environment, ct).ConfigureAwait(false);
        WarnAboutMissingModules(log);

        string make = ResolveTool("make", environment) ?? "make";
        await log.RunAsync(
            "make", make, [$"-j{Math.Max(1, options.JobCount)}"], paths.BuildDirectory, environment, ct).ConfigureAwait(false);

        string destination = Path.Combine(paths.StagingDirectory, ".destdir");
        await log.RunAsync(
            "make install", make, ["install", $"DESTDIR={destination}"], paths.BuildDirectory, environment, ct)
            .ConfigureAwait(false);

        Hoist(Path.Combine(destination, StagedPrefix.TrimStart('/')), paths.StagingDirectory);
        Directory.Delete(destination, recursive: true);

        if (OperatingSystem.IsMacOS() && options.Shared)
        {
            await MakeDylibRelocatableAsync(paths, log, ct).ConfigureAwait(false);
        }
    }

    /// <summary>The full configure command line, minus the script itself.</summary>
    internal static IReadOnlyList<string> ComposeConfigureArguments(BuildOptions options, ToolchainEnvironment toolchain)
    {
        List<string> arguments =
        [
            $"--prefix={StagedPrefix}",

            // 'make install' would otherwise write bin/pip3 with a '#!/install/bin/python3' shebang that
            // nothing can fix after the move. The ensurepip package itself is still installed as part of
            // the stdlib, so 'python -m venv' continues to seed pip into every environment.
            "--with-ensurepip=no",
        ];

        if (options.Shared)
        {
            arguments.Add("--enable-shared");
        }

        if (options.Optimize)
        {
            arguments.Add("--enable-optimizations");
        }

        if (options.Lto)
        {
            arguments.Add("--with-lto");
        }

        if (options.FreeThreaded)
        {
            arguments.Add("--disable-gil");
        }

        if (toolchain.OpenSslPrefix is { } openssl)
        {
            arguments.Add($"--with-openssl={openssl}");
            arguments.Add("--with-openssl-rpath=auto");
        }

        arguments.AddRange(options.ConfigureArguments);
        return arguments;
    }

    /// <summary>
    /// The environment configure and make run under. Caller-supplied
    /// <see cref="SourceBuildSource.BuildEnvironment"/> entries are applied last and win outright.
    /// </summary>
    internal static Dictionary<string, string> ComposeEnvironment(BuildOptions options, ToolchainEnvironment toolchain)
    {
        Dictionary<string, string> environment = new(StringComparer.Ordinal);
        foreach ((string key, string value) in toolchain.Variables)
        {
            environment[key] = value;
        }

        List<string> ldFlags = [.. toolchain.LdFlags];
        if (options.Shared)
        {
            ldFlags.Add(RelativeRunPath);
        }

        Append(environment, "CPPFLAGS", toolchain.CppFlags);
        Append(environment, "LDFLAGS", ldFlags);

        foreach ((string key, string value) in options.BuildEnvironment)
        {
            environment[key] = value;
        }

        return environment;

        static void Append(Dictionary<string, string> target, string name, IReadOnlyList<string> flags)
        {
            if (flags.Count == 0)
            {
                return;
            }

            string existing = Environment.GetEnvironmentVariable(name) ?? string.Empty;
            target[name] = string.Join(' ', existing.Length > 0 ? [existing, .. flags] : flags).Trim();
        }
    }

    /// <summary>
    /// The runpath that makes the interpreter find its own libpython wherever the tree ends up.
    /// <para>
    /// On Linux the quoting is load-bearing and easy to break: this string is written verbatim into the
    /// generated Makefile, so <c>$$</c> is what survives make's expansion as a single <c>$</c>, and the
    /// single quotes are what stop the shell make invokes from expanding <c>$ORIGIN</c> to nothing. The
    /// linker ultimately sees <c>-Wl,-rpath,$ORIGIN/../lib</c>.
    /// </para>
    /// </summary>
    internal static string RelativeRunPath =>
        OperatingSystem.IsMacOS() ? "-Wl,-rpath,@loader_path/../lib" : "-Wl,-rpath,'$$ORIGIN/../lib'";

    /// <summary>
    /// Rewrites the dylib's install name and the interpreter's reference to it as <c>@rpath</c>-relative,
    /// so the pair survives being moved out of staging. Every edit invalidates the binary's signature, and
    /// on Apple silicon an invalidly-signed binary is killed on launch — hence the ad-hoc re-sign.
    /// </summary>
    private static async Task MakeDylibRelocatableAsync(BuildPaths paths, BuildLog log, CancellationToken ct)
    {
        string libraryDirectory = Path.Combine(paths.StagingDirectory, "lib");
        string? dylib = Directory.Exists(libraryDirectory)
            ? Directory.EnumerateFiles(libraryDirectory, "libpython*.dylib").FirstOrDefault(path => !IsSymlink(path))
            : null;
        if (dylib is null)
        {
            throw new PythonException(
                PythonErrorKind.InstallFailed,
                $"A shared build was requested but no libpython dylib was installed under '{libraryDirectory}'.");
        }

        string name = Path.GetFileName(dylib);
        string interpreter = Path.Combine(paths.StagingDirectory, "bin", $"python{paths.Version.Major}.{paths.Version.Minor}");
        if (!File.Exists(interpreter))
        {
            interpreter = Path.Combine(paths.StagingDirectory, "bin", "python3");
        }

        const string tool = "install_name_tool";
        await log.RunAsync("install_name_tool -id", tool, ["-id", $"@rpath/{name}", dylib], ct: ct).ConfigureAwait(false);
        await log.RunAsync(
            "install_name_tool -change", tool,
            ["-change", $"{StagedPrefix}/lib/{name}", $"@rpath/{name}", interpreter], ct: ct).ConfigureAwait(false);

        // Already present in most builds; a duplicate is a non-fatal error, so failure is tolerated.
        await log.RunAllowingFailureAsync(
            "install_name_tool -add_rpath", tool,
            ["-add_rpath", "@loader_path/../lib", interpreter], ct: ct).ConfigureAwait(false);

        await log.RunAsync("codesign", "codesign", ["--force", "--sign", "-", dylib], ct: ct).ConfigureAwait(false);
        await log.RunAsync("codesign", "codesign", ["--force", "--sign", "-", interpreter], ct: ct).ConfigureAwait(false);
    }

    /// <summary>
    /// Configure prints the optional modules it could not build in one block. It names them far more
    /// precisely than an import probe can, so it is worth surfacing even when the build succeeds.
    /// </summary>
    private static void WarnAboutMissingModules(BuildLog log)
    {
        const string marker = "necessary bits to build these optional modules were not found";
        string transcript = log.Transcript.ToString();
        int index = transcript.IndexOf(marker, StringComparison.Ordinal);
        if (index < 0)
        {
            return;
        }

        int start = transcript.LastIndexOf('\n', index) + 1;
        int end = transcript.IndexOf("\n\n", index, StringComparison.Ordinal);
        string block = (end < 0 ? transcript[start..] : transcript[start..end]).Trim();
        log.Logger.LogWarning("configure could not build some optional modules:{NewLine}{Details}", Environment.NewLine, block);
    }

    /// <summary>Moves everything from the DESTDIR-staged prefix up into the directory the host will commit.</summary>
    private static void Hoist(string from, string to)
    {
        if (!Directory.Exists(from))
        {
            throw new PythonException(
                PythonErrorKind.InstallFailed,
                $"'make install' did not produce '{from}'.");
        }

        foreach (string directory in Directory.EnumerateDirectories(from))
        {
            Directory.Move(directory, Path.Combine(to, Path.GetFileName(directory)));
        }

        foreach (string file in Directory.EnumerateFiles(from))
        {
            File.Move(file, Path.Combine(to, Path.GetFileName(file)), overwrite: true);
        }
    }

    /// <summary>Resolves a tool against a PATH the toolchain may have prepended to, then the ambient one.</summary>
    private static string? ResolveTool(string name, IReadOnlyDictionary<string, string> environment)
    {
        if (environment.TryGetValue("PATH", out string? path))
        {
            foreach (string directory in path.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries))
            {
                string candidate = Path.Combine(directory, name);
                if (File.Exists(candidate))
                {
                    return candidate;
                }
            }
        }

        return Executables.Which(name);
    }

    /// <summary>The tarball ships an executable configure script, but a stray umask or copy can strip that.</summary>
    private static void EnsureExecutable(string path)
    {
        if (OperatingSystem.IsWindows())
        {
            return;
        }

        UnixFileMode mode = File.GetUnixFileMode(path);
        if (!mode.HasFlag(UnixFileMode.UserExecute))
        {
            File.SetUnixFileMode(path, mode | UnixFileMode.UserExecute);
        }
    }

    private static bool IsSymlink(string path) => File.ResolveLinkTarget(path, returnFinalTarget: false) is not null;
}
