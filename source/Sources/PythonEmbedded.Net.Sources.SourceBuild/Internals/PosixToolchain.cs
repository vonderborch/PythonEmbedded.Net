using Microsoft.Extensions.Logging;
using PythonEmbedded.Net.Exceptions;
using PythonEmbedded.Net.Extensibility;
using PythonEmbedded.Net.Models;

namespace PythonEmbedded.Net.Sources.SourceBuild.Internals;

/// <summary>What the toolchain probe found, in the form <see cref="PosixBuilder"/> needs to configure with.</summary>
/// <param name="Variables">Environment variables to set for configure/make (CC, PATH, PKG_CONFIG_PATH, SDKROOT, ...).</param>
/// <param name="CppFlags">Fragments appended to <c>CPPFLAGS</c>.</param>
/// <param name="LdFlags">Fragments appended to <c>LDFLAGS</c>.</param>
/// <param name="OpenSslPrefix">Prefix passed to <c>--with-openssl</c>, when OpenSSL lives outside the default search path.</param>
/// <param name="DependencyPrefix">A provisioned conda-forge prefix the built interpreter will link against, if one was created.</param>
internal sealed record ToolchainEnvironment(
    IReadOnlyDictionary<string, string> Variables,
    IReadOnlyList<string> CppFlags,
    IReadOnlyList<string> LdFlags,
    string? OpenSslPrefix = null,
    string? DependencyPrefix = null)
{
    public static ToolchainEnvironment Empty { get; } = new(new Dictionary<string, string>(), [], []);
}

/// <summary>
/// Finds the compiler and libraries a POSIX CPython build needs, provisioning them when
/// <see cref="SourceBuildSource.ProvisionDependencies"/> is set and failing with the exact install
/// command when it isn't.
/// </summary>
internal static class PosixToolchain
{
    /// <summary>Homebrew formulae whose headers/libraries configure should be pointed at, beyond OpenSSL.</summary>
    private static readonly string[] BrewLinkedFormulae = ["xz", "sqlite", "readline"];

    /// <summary>Known OpenSSL prefixes, in probe order: Homebrew (arm64, then x64), then MacPorts.</summary>
    private static readonly string[] MacOpenSslPrefixes =
        ["/opt/homebrew/opt/openssl@3", "/usr/local/opt/openssl@3", "/opt/local"];

    public static Task<ToolchainEnvironment> PrepareAsync(
        BuildOptions options, PythonVersion version, SourceContext context, BuildLog log, CancellationToken ct)
        => OperatingSystem.IsMacOS()
            ? PrepareMacAsync(options, context, log, ct)
            : PrepareLinuxAsync(options, version, context, log, ct);

    private static async Task<ToolchainEnvironment> PrepareMacAsync(
        BuildOptions options, SourceContext context, BuildLog log, CancellationToken ct)
    {
        await RequireCommandLineToolsAsync(log, ct).ConfigureAwait(false);

        string? brew = Executables.Which("brew");
        string? openssl = await FindMacOpenSslAsync(brew, log, ct).ConfigureAwait(false);
        if (openssl is null)
        {
            if (!options.ProvisionDependencies || brew is null)
            {
                throw new PythonException(
                    PythonErrorKind.InstallFailed,
                    $"""
                     OpenSSL was not found, so CPython would build without the 'ssl' module.
                     {(brew is null
                         ? "Install Homebrew (https://brew.sh), then run:"
                         : "Run:")}

                         brew install {string.Join(' ', DependencyProvisioner.BrewFormulae)}

                     Or set ProvisionDependencies = true on SourceBuildSource to install it automatically.
                     """);
            }

            await DependencyProvisioner.BrewInstallAsync(brew, log, ct).ConfigureAwait(false);
            openssl = await FindMacOpenSslAsync(brew, log, ct).ConfigureAwait(false)
                      ?? throw new PythonException(
                          PythonErrorKind.InstallFailed,
                          $"'brew install {string.Join(' ', DependencyProvisioner.BrewFormulae)}' succeeded but OpenSSL still could not be located.");
        }

        Dictionary<string, string> variables = new(StringComparer.Ordinal);
        List<string> cppFlags = [];
        List<string> ldFlags = [];
        List<string> pkgConfigPaths = [Path.Combine(openssl, "lib", "pkgconfig")];

        foreach (string formula in BrewLinkedFormulae)
        {
            if (await BrewPrefixAsync(brew, formula, log, ct).ConfigureAwait(false) is not { } prefix)
            {
                continue;
            }

            cppFlags.Add($"-I{Path.Combine(prefix, "include")}");
            ldFlags.Add($"-L{Path.Combine(prefix, "lib")}");
            pkgConfigPaths.Add(Path.Combine(prefix, "lib", "pkgconfig"));
        }

        variables["PKG_CONFIG_PATH"] = JoinPaths(pkgConfigPaths, Environment.GetEnvironmentVariable("PKG_CONFIG_PATH"));

        if (Executables.Which("xcrun") is { } xcrun)
        {
            PythonResult sdk = await log.RunAllowingFailureAsync(
                "xcrun --show-sdk-path", xcrun, ["--show-sdk-path"], ct: ct).ConfigureAwait(false);
            if (sdk.ExitCode == 0 && sdk.StandardOutput.Trim() is { Length: > 0 } sdkPath)
            {
                variables["SDKROOT"] = sdkPath;
            }
        }

        return new ToolchainEnvironment(variables, cppFlags, ldFlags, OpenSslPrefix: openssl);
    }

    private static async Task<ToolchainEnvironment> PrepareLinuxAsync(
        BuildOptions options, PythonVersion version, SourceContext context, BuildLog log, CancellationToken ct)
    {
        List<string> missing = ProbeLinux();
        if (missing.Count == 0)
        {
            return ToolchainEnvironment.Empty;
        }

        context.Logger.LogInformation("Build dependencies missing: {Missing}", string.Join(", ", missing));

        if (!options.ProvisionDependencies)
        {
            throw new PythonException(
                PythonErrorKind.InstallFailed,
                $"""
                 CPython cannot be built here: {string.Join(", ", missing)} {(missing.Count == 1 ? "is" : "are")} missing.
                 Run:

                     {DependencyProvisioner.ManualInstallCommand()}

                 Or set ProvisionDependencies = true on SourceBuildSource to install them automatically
                 (root-free, into <root>/tools/, via conda-forge).
                 """);
        }

        // Root-free first. The system package manager is only a fallback, and only with elevation allowed.
        try
        {
            string prefix = await DependencyProvisioner
                .EnsureCondaForgePrefixAsync(version, context, log, ct).ConfigureAwait(false);
            return CondaForgeEnvironment(prefix);
        }
        catch (PythonException ex) when (options.AllowElevation && Executables.Which("sudo") is not null)
        {
            context.Logger.LogWarning(
                ex, "conda-forge provisioning failed; falling back to the system package manager");
            await DependencyProvisioner.SystemInstallAsync(log, ct).ConfigureAwait(false);

            List<string> stillMissing = ProbeLinux();
            if (stillMissing.Count > 0)
            {
                throw new PythonException(
                    PythonErrorKind.InstallFailed,
                    $"Build dependencies are still missing after provisioning: {string.Join(", ", stillMissing)}.");
            }

            return ToolchainEnvironment.Empty;
        }
    }

    /// <summary>
    /// The conda-forge prefix has to be on the runtime search path of the interpreter it produces, so the
    /// rpath here is absolute — deleting <c>&lt;root&gt;/tools/build-deps-*</c> breaks the built interpreter.
    /// </summary>
    internal static ToolchainEnvironment CondaForgeEnvironment(string prefix)
    {
        string bin = Path.Combine(prefix, "bin");
        string lib = Path.Combine(prefix, "lib");
        string include = Path.Combine(prefix, "include");

        Dictionary<string, string> variables = new(StringComparer.Ordinal)
        {
            ["PATH"] = JoinPaths([bin], Environment.GetEnvironmentVariable("PATH")),
            ["PKG_CONFIG_PATH"] = JoinPaths([Path.Combine(lib, "pkgconfig")], Environment.GetEnvironmentVariable("PKG_CONFIG_PATH")),
            ["LD_LIBRARY_PATH"] = JoinPaths([lib], Environment.GetEnvironmentVariable("LD_LIBRARY_PATH")),
        };

        // conda-forge compilers are triplet-prefixed; fall back to the ambient one if the layout changes.
        string? compiler = Directory.Exists(bin)
            ? Directory.EnumerateFiles(bin, "*-linux-gnu-gcc").FirstOrDefault()
            : null;
        if (compiler is not null)
        {
            variables["CC"] = compiler;
            if (File.Exists(compiler[..^3] + "g++"))
            {
                variables["CXX"] = compiler[..^3] + "g++";
            }
        }

        return new ToolchainEnvironment(
            variables,
            [$"-I{include}"],
            [$"-L{lib}", $"-Wl,-rpath,{lib}"],
            OpenSslPrefix: prefix,
            DependencyPrefix: prefix);
    }

    /// <summary>Human-readable names of the Linux build prerequisites that are absent.</summary>
    private static List<string> ProbeLinux()
    {
        List<string> missing = [];
        if (Executables.WhichAny("cc", "gcc", "clang") is null)
        {
            missing.Add("a C compiler (cc/gcc/clang)");
        }

        if (Executables.Which("make") is null)
        {
            missing.Add("make");
        }

        if (!HasOpenSslHeaders())
        {
            missing.Add("the OpenSSL development headers");
        }

        return missing;
    }

    private static bool HasOpenSslHeaders() =>
        new[] { "/usr/include", "/usr/local/include", "/usr/include/x86_64-linux-gnu", "/usr/include/aarch64-linux-gnu" }
            .Any(directory => File.Exists(Path.Combine(directory, "openssl", "ssl.h")));

    /// <summary>
    /// The Xcode Command Line Tools cannot be installed unattended — <c>xcode-select --install</c> opens a
    /// GUI dialog and <c>softwareupdate</c> needs root — so on macOS they are always a hard prerequisite.
    /// </summary>
    private static async Task RequireCommandLineToolsAsync(BuildLog log, CancellationToken ct)
    {
        const string message = """
                               The Xcode Command Line Tools are required to compile CPython on macOS and cannot be
                               installed automatically. Run:

                                   xcode-select --install
                               """;

        if (Executables.Which("xcode-select") is not { } xcodeSelect)
        {
            throw new PythonException(PythonErrorKind.ToolMissing, message);
        }

        PythonResult result = await log.RunAllowingFailureAsync(
            "xcode-select -p", xcodeSelect, ["-p"], ct: ct).ConfigureAwait(false);
        if (result.ExitCode != 0)
        {
            throw new PythonException(PythonErrorKind.ToolMissing, message);
        }
    }

    private static async Task<string?> FindMacOpenSslAsync(string? brew, BuildLog log, CancellationToken ct)
    {
        if (await BrewPrefixAsync(brew, "openssl@3", log, ct).ConfigureAwait(false) is { } fromBrew)
        {
            return fromBrew;
        }

        return MacOpenSslPrefixes.FirstOrDefault(prefix => File.Exists(Path.Combine(prefix, "include", "openssl", "ssl.h")));
    }

    private static async Task<string?> BrewPrefixAsync(string? brew, string formula, BuildLog log, CancellationToken ct)
    {
        if (brew is null)
        {
            return null;
        }

        PythonResult result = await log.RunAllowingFailureAsync(
            $"brew --prefix {formula}", brew, ["--prefix", formula], ct: ct).ConfigureAwait(false);
        string prefix = result.StandardOutput.Trim();
        return result.ExitCode == 0 && Directory.Exists(prefix) ? prefix : null;
    }

    private static string JoinPaths(IEnumerable<string> first, string? existing)
    {
        IEnumerable<string> all = existing is { Length: > 0 } ? [.. first, existing] : first;
        return string.Join(Path.PathSeparator, all);
    }
}
