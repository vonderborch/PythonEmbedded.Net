using System.Runtime.InteropServices;
using PythonEmbedded.Net.Exceptions;

namespace PythonEmbedded.Net.Sources.SourceBuild.Internals;

/// <summary>
/// Builds CPython with MSVC and lays the result out with <c>PC\layout</c>.
/// <para>
/// Windows needs none of the POSIX relocation work: <c>PC\layout</c> emits a self-contained tree with
/// <c>python.exe</c> at its root — already one of the layouts <c>PythonHost</c> probes for — and Windows
/// builds bake no absolute prefix into sysconfig.
/// </para>
/// </summary>
internal static class WindowsBuilder
{
    public static async Task BuildAsync(
        BuildOptions options, BuildPaths paths, BuildLog log, CancellationToken ct)
    {
        (string platform, string outputDirectory) = RuntimeInformation.OSArchitecture switch
        {
            Architecture.X64 => ("x64", "amd64"),
            Architecture.Arm64 => ("ARM64", "arm64"),
            var other => throw new PythonException(
                PythonErrorKind.UnsupportedPlatform, $"CPython cannot be built for '{other}' on Windows."),
        };

        string buildScript = Path.Combine(paths.SourceDirectory, "PCbuild", "build.bat");
        if (!File.Exists(buildScript))
        {
            throw new PythonException(
                PythonErrorKind.InstallFailed,
                $"The unpacked source tree at '{paths.SourceDirectory}' has no PCbuild\\build.bat.");
        }

        List<string> arguments = ["/c", buildScript, "-p", platform, "-c", "Release"];
        if (options.Optimize)
        {
            arguments.Add("--pgo");
        }

        if (options.FreeThreaded)
        {
            arguments.Add("--disable-gil");
        }

        arguments.AddRange(options.ConfigureArguments);

        // build.bat runs get_externals.bat, which downloads OpenSSL/tcl/tk/... regardless of whether the
        // source tarball came from cache. That is why a Windows source build always needs the network.
        await log.RunAsync(
            "build.bat", "cmd.exe", arguments, paths.SourceDirectory, options.BuildEnvironment, ct).ConfigureAwait(false);

        string built = Path.Combine(paths.SourceDirectory, "PCbuild", outputDirectory, "python.exe");
        if (!File.Exists(built))
        {
            throw new PythonException(
                PythonErrorKind.InstallFailed,
                $"The build reported success but produced no interpreter at '{built}'. Full build log: {log.FilePath}");
        }

        await log.RunAsync(
            "PC/layout",
            built,
            [
                Path.Combine(paths.SourceDirectory, "PC", "layout"),
                "--source", paths.SourceDirectory,
                "--build", Path.Combine(paths.SourceDirectory, "PCbuild", outputDirectory),
                "--copy", paths.StagingDirectory,
                "--preset-default",
            ],
            paths.SourceDirectory,
            options.BuildEnvironment,
            ct).ConfigureAwait(false);
    }
}
