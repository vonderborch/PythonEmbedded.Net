using System.Runtime.InteropServices;
using Microsoft.Extensions.Logging;
using PythonEmbedded.Net.Exceptions;
using PythonEmbedded.Net.Extensibility;
using PythonEmbedded.Net.Models;

namespace PythonEmbedded.Net.Sources.SourceBuild.Internals;

/// <summary>
/// Installs the libraries and compilers CPython needs, when <see cref="SourceBuildSource.ProvisionDependencies"/>
/// allows it. Ordered by how much authority each route demands: brew and conda-forge run entirely as the
/// current user, while system package managers and winget need <see cref="SourceBuildSource.AllowElevation"/>.
/// </summary>
internal static class DependencyProvisioner
{
    /// <summary>Homebrew formulae that cover CPython's optional modules on macOS.</summary>
    public static readonly string[] BrewFormulae = ["openssl@3", "xz", "sqlite"];

    /// <summary>
    /// conda-forge packages for a root-free Linux toolchain. The compilers are included because a
    /// distro without <c>libssl-dev</c> often lacks a compiler too, and this route cannot use sudo.
    /// </summary>
    public static string[] CondaForgePackages =>
        RuntimeInformation.OSArchitecture == Architecture.Arm64
            ? ["gcc_linux-aarch64", "gxx_linux-aarch64", ..CommonCondaForgePackages]
            : ["gcc_linux-64", "gxx_linux-64", ..CommonCondaForgePackages];

    private static readonly string[] CommonCondaForgePackages =
        ["openssl", "xz", "sqlite", "libffi", "readline", "ncurses", "bzip2", "tk", "make", "pkg-config"];

    /// <summary>Runs <c>brew install</c> for the formulae CPython needs. Homebrew never needs root.</summary>
    public static async Task BrewInstallAsync(string brew, BuildLog log, CancellationToken ct)
    {
        await log.RunAsync("brew install", brew, ["install", .. BrewFormulae], ct: ct).ConfigureAwait(false);
    }

    /// <summary>
    /// Materializes a conda-forge prefix under <c>&lt;root&gt;/tools/build-deps-&lt;version&gt;/</c> and returns it.
    /// It lives in <c>tools/</c>, not <c>cache/</c>, deliberately: the interpreter this build produces links
    /// against these libraries for the rest of its life, so the prefix must outlive any cache sweep.
    /// </summary>
    public static async Task<string> EnsureCondaForgePrefixAsync(
        PythonVersion version, SourceContext context, BuildLog log, CancellationToken ct)
    {
        string toolsDirectory = ToolsDirectory(context);
        string prefix = Path.Combine(toolsDirectory, $"build-deps-{version.Major}.{version.Minor}");
        if (Directory.Exists(Path.Combine(prefix, "lib")))
        {
            context.Logger.LogDebug("Reusing build dependency prefix {Prefix}", prefix);
            return prefix;
        }

        string micromamba = await EnsureMicromambaAsync(context, ct).ConfigureAwait(false);
        await log.RunAsync(
            "micromamba create",
            micromamba,
            ["create", "--yes", "--prefix", prefix, "--channel", "conda-forge", .. CondaForgePackages],
            environment: new Dictionary<string, string> { ["MAMBA_ROOT_PREFIX"] = Path.Combine(toolsDirectory, "mamba-root") },
            ct: ct).ConfigureAwait(false);

        return prefix;
    }

    /// <summary>
    /// Installs build dependencies through the distro's package manager. Requires
    /// <see cref="SourceBuildSource.AllowElevation"/>; <c>sudo -n</c> is used so a password prompt fails
    /// fast with the copy-pasteable command rather than hanging on a terminal nobody is watching.
    /// </summary>
    public static async Task SystemInstallAsync(BuildLog log, CancellationToken ct)
    {
        SystemPackageManager? manager = DetectSystemPackageManager();
        if (manager is null)
        {
            throw new PythonException(
                PythonErrorKind.InstallFailed,
                "No supported system package manager (apt-get, dnf, pacman, zypper, apk) was found on PATH.");
        }

        PythonResult result = await log.RunAllowingFailureAsync(
            $"{manager.Name} install", "sudo", ["-n", manager.Executable, .. manager.InstallArguments], ct: ct)
            .ConfigureAwait(false);

        if (result.ExitCode != 0)
        {
            throw new PythonException(
                PythonErrorKind.InstallFailed,
                $"""
                 Installing build dependencies with {manager.Name} failed (exit code {result.ExitCode}).
                 'sudo -n' does not prompt for a password, so run this yourself and retry:

                     {manager.CommandLine}

                 Full build log: {log.FilePath}
                 """);
        }
    }

    /// <summary>Installs the Visual Studio C++ build tools via winget. Requires elevation (UAC).</summary>
    public static async Task WingetInstallBuildToolsAsync(BuildLog log, CancellationToken ct)
    {
        string winget = Executables.Which("winget")
            ?? throw new PythonException(
                PythonErrorKind.ToolMissing,
                $"winget is not available, so the C++ build tools cannot be installed automatically. Run:{Environment.NewLine}{Environment.NewLine}    {WindowsToolchain.BuildToolsCommand}");

        await log.RunAsync(
            "winget install",
            winget,
            [
                "install", "--id", "Microsoft.VisualStudio.2022.BuildTools", "--exact",
                "--accept-package-agreements", "--accept-source-agreements",
                "--override",
                "--quiet --wait --norestart --add Microsoft.VisualStudio.Workload.VCTools --includeRecommended",
            ],
            ct: ct).ConfigureAwait(false);
    }

    /// <summary>The command a user should run themselves on this machine, named in every provisioning error.</summary>
    public static string ManualInstallCommand()
    {
        if (OperatingSystem.IsMacOS())
        {
            return $"brew install {string.Join(' ', BrewFormulae)}";
        }

        if (OperatingSystem.IsWindows())
        {
            return WindowsToolchain.BuildToolsCommand;
        }

        return DetectSystemPackageManager()?.CommandLine
               ?? "install a C compiler plus the OpenSSL, zlib, bzip2, readline, sqlite3, libffi, lzma and tk development headers";
    }

    /// <summary>The distro package manager on PATH, with the exact package set for CPython.</summary>
    public static SystemPackageManager? DetectSystemPackageManager()
    {
        foreach (SystemPackageManager manager in KnownPackageManagers)
        {
            if (Executables.Which(manager.Executable) is not null)
            {
                return manager;
            }
        }

        return null;
    }

    private static readonly SystemPackageManager[] KnownPackageManagers =
    [
        new("apt", "apt-get",
        [
            "install", "-y", "build-essential", "pkg-config", "libssl-dev", "zlib1g-dev", "libbz2-dev",
            "libreadline-dev", "libsqlite3-dev", "libffi-dev", "liblzma-dev", "tk-dev", "uuid-dev", "libgdbm-dev",
        ]),
        new("dnf", "dnf",
        [
            "install", "-y", "gcc", "gcc-c++", "make", "pkgconf-pkg-config", "openssl-devel", "zlib-devel",
            "bzip2-devel", "readline-devel", "sqlite-devel", "libffi-devel", "xz-devel", "tk-devel", "gdbm-devel",
        ]),
        new("pacman", "pacman",
        [
            "-S", "--needed", "--noconfirm", "base-devel", "openssl", "zlib", "bzip2", "readline",
            "sqlite", "libffi", "xz", "tk", "gdbm",
        ]),
        new("zypper", "zypper",
        [
            "install", "-y", "gcc", "gcc-c++", "make", "pkg-config", "libopenssl-devel", "zlib-devel",
            "libbz2-devel", "readline-devel", "sqlite3-devel", "libffi-devel", "xz-devel", "tk-devel", "gdbm-devel",
        ]),
        new("apk", "apk",
        [
            "add", "build-base", "pkgconf", "openssl-dev", "zlib-dev", "bzip2-dev", "readline-dev",
            "sqlite-dev", "libffi-dev", "xz-dev", "tk-dev", "gdbm-dev",
        ]),
    ];

    /// <summary>Downloads micromamba into <c>&lt;root&gt;/tools/</c>, mirroring what the conda satellite does.</summary>
    private static async Task<string> EnsureMicromambaAsync(SourceContext context, CancellationToken ct)
    {
        if (Environment.GetEnvironmentVariable("PYEMBED_TOOL_MICROMAMBA") is { Length: > 0 } overridePath)
        {
            return File.Exists(overridePath)
                ? overridePath
                : throw new PythonException(
                    PythonErrorKind.ToolMissing, $"PYEMBED_TOOL_MICROMAMBA points to '{overridePath}', which does not exist.");
        }

        string directory = Path.Combine(ToolsDirectory(context), "micromamba");
        string target = Path.Combine(directory, "micromamba");
        if (File.Exists(target))
        {
            return target;
        }

        // Tools.EnsureAsync needs a PythonInstallation, and during a source build there isn't one yet —
        // this is the same download it performs, minus the install-scoped resolution.
        string asset = RuntimeInformation.OSArchitecture == Architecture.Arm64
            ? "micromamba-linux-aarch64"
            : "micromamba-linux-64";
        string downloaded = await context.DownloadAsync(
            new Uri($"https://github.com/mamba-org/micromamba-releases/releases/latest/download/{asset}"),
            ct: ct).ConfigureAwait(false);

        Directory.CreateDirectory(directory);
        File.Copy(downloaded, target, overwrite: true);
        if (!OperatingSystem.IsWindows())
        {
            File.SetUnixFileMode(
                target,
                UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute
                | UnixFileMode.GroupRead | UnixFileMode.GroupExecute
                | UnixFileMode.OtherRead | UnixFileMode.OtherExecute);
        }

        return target;
    }

    /// <summary><c>&lt;root&gt;/tools</c>, derived from the cache directory the source context exposes.</summary>
    private static string ToolsDirectory(SourceContext context)
        => Path.Combine(Path.GetDirectoryName(context.CacheDirectory.TrimEnd(Path.DirectorySeparatorChar))!, "tools");

    /// <summary>One distro package manager and the exact command that installs CPython's build dependencies.</summary>
    /// <param name="Name">Short name used in messages.</param>
    /// <param name="Executable">The binary probed on PATH.</param>
    /// <param name="InstallArguments">Arguments, including the package list.</param>
    public sealed record SystemPackageManager(string Name, string Executable, string[] InstallArguments)
    {
        /// <summary>The full command line, for error messages the user can copy.</summary>
        public string CommandLine => $"sudo {Executable} {string.Join(' ', InstallArguments)}";
    }
}
