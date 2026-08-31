using PythonEmbedded.Net.Exceptions;
using PythonEmbedded.Net.Models;

namespace PythonEmbedded.Net.Sources.SourceBuild.Internals;

/// <summary>Locates — and, when allowed, installs — the MSVC C++ toolset that <c>PCbuild\build.bat</c> needs.</summary>
internal static class WindowsToolchain
{
    /// <summary>The command that installs the C++ build tools, quoted in every error this class throws.</summary>
    public const string BuildToolsCommand =
        "winget install --id Microsoft.VisualStudio.2022.BuildTools --override " +
        "\"--quiet --wait --norestart --add Microsoft.VisualStudio.Workload.VCTools --includeRecommended\"";

    /// <summary>The vswhere query for an installation that actually carries the x64 C++ compiler.</summary>
    private static readonly string[] VsWhereArguments =
    [
        "-latest", "-products", "*",
        "-requires", "Microsoft.VisualStudio.Component.VC.Tools.x86.x64",
        "-property", "installationPath",
    ];

    /// <summary>Returns the Visual Studio installation path, provisioning the build tools if permitted.</summary>
    public static async Task<string> EnsureAsync(BuildOptions options, BuildLog log, CancellationToken ct)
    {
        if (await FindAsync(log, ct).ConfigureAwait(false) is { } found)
        {
            return found;
        }

        if (!options.ProvisionDependencies || !options.AllowElevation)
        {
            throw new PythonException(
                PythonErrorKind.ToolMissing,
                $"""
                 The Visual Studio C++ build tools are required to compile CPython on Windows and were not found.
                 Run (in an elevated prompt):

                     {BuildToolsCommand}

                 Or set ProvisionDependencies = true and AllowElevation = true on SourceBuildSource to install
                 them automatically — this triggers a UAC prompt.
                 """);
        }

        await DependencyProvisioner.WingetInstallBuildToolsAsync(log, ct).ConfigureAwait(false);

        return await FindAsync(log, ct).ConfigureAwait(false)
               ?? throw new PythonException(
                   PythonErrorKind.ToolMissing,
                   "The C++ build tools were installed but vswhere still does not report a usable installation. " +
                   "A reboot is sometimes needed after installing them.");
    }

    private static async Task<string?> FindAsync(BuildLog log, CancellationToken ct)
    {
        string vswhere = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86),
            "Microsoft Visual Studio", "Installer", "vswhere.exe");
        if (!File.Exists(vswhere))
        {
            return null;
        }

        PythonResult result = await log.RunAllowingFailureAsync("vswhere", vswhere, VsWhereArguments, ct: ct)
            .ConfigureAwait(false);
        if (result.ExitCode != 0)
        {
            return null;
        }

        string path = result.StandardOutput
            .Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .FirstOrDefault(string.Empty);
        return Directory.Exists(path) ? path : null;
    }
}
