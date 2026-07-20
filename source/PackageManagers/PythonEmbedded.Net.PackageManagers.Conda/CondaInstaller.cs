using System.Runtime.InteropServices;
using System.Text.Json;
using System.Text.Json.Serialization;
using PythonEmbedded.Net.Exceptions;
using PythonEmbedded.Net.Extensibility;
using PythonEmbedded.Net.Models;

namespace PythonEmbedded.Net.PackageManagers.Conda;

/// <summary>
/// An <see cref="IPackageInstaller"/> for the conda ecosystem, driven by micromamba — a single
/// static binary downloaded runtime-locally on first use (real conda is never touched).
/// Environments are conda environments (self-contained, python from conda-forge pinned to the
/// installation's major.minor), so conda-only packages (CUDA toolkits, geospatial stacks, ...)
/// work. Activate with:
/// <code>PythonEnvironment.Configure(o => o.Installer = new CondaInstaller());</code>
/// </summary>
public sealed class CondaInstaller : PackageInstallerBase
{
    /// <inheritdoc />
    public override string Name => "conda";

    /// <summary>Channels searched for packages, in priority order. Default: conda-forge only.</summary>
    public string[] Channels { get; init; } = ["conda-forge"];

    /// <summary>
    /// The micromamba release to provision, e.g. <c>"2.1.1-0"</c>. Default <c>"latest"</c>.
    /// </summary>
    public string MicromambaVersion { get; init; } = "latest";

    /// <inheritdoc />
    public override async Task CreateEnvironmentAsync(PythonInstallation install, string envDirectory, CancellationToken ct)
    {
        List<string> args = ["create", "--yes", "--prefix", envDirectory];
        AddChannels(args);
        args.Add($"python={install.Version.Major}.{install.Version.Minor}");

        await RunMicromambaOrThrowAsync(install, args, PythonErrorKind.EnvironmentFailed, "micromamba create", ct)
            .ConfigureAwait(false);
    }

    /// <inheritdoc />
    public override async Task InstallAsync(PythonVirtualEnvironment env, PackageRequest request, CancellationToken ct)
    {
        List<string> args = ["install", "--yes", "--prefix", env.Directory];
        AddChannels(args);

        if (request.ProjectDirectory is not null)
        {
            string environmentYml = Path.Combine(request.ProjectDirectory, "environment.yml");
            if (!File.Exists(environmentYml))
            {
                throw new PythonException(
                    PythonErrorKind.PackageOperationFailed,
                    $"No environment.yml found in '{request.ProjectDirectory}' (the conda installer installs projects from environment.yml).");
            }

            args.AddRange(["--file", environmentYml]);
        }

        if (request.RequirementsFile is not null)
        {
            args.AddRange(["--file", request.RequirementsFile]);
        }

        args.AddRange(request.ExtraArgs);
        args.AddRange(request.Packages);

        await RunMicromambaOrThrowAsync(env.Installation, args, PythonErrorKind.PackageOperationFailed, "micromamba install", ct)
            .ConfigureAwait(false);
    }

    /// <inheritdoc />
    public override Task UninstallAsync(PythonVirtualEnvironment env, string package, CancellationToken ct)
        => RunMicromambaOrThrowAsync(
            env.Installation,
            ["remove", "--yes", "--prefix", env.Directory, package],
            PythonErrorKind.PackageOperationFailed,
            $"micromamba remove of '{package}'",
            ct);

    /// <inheritdoc />
    public override async Task<IReadOnlyList<InstalledPackage>> ListAsync(PythonVirtualEnvironment env, CancellationToken ct)
    {
        PythonResult result = await RunMicromambaOrThrowAsync(
            env.Installation, ["list", "--prefix", env.Directory, "--json"], PythonErrorKind.PackageOperationFailed, "micromamba list", ct)
            .ConfigureAwait(false);

        List<CondaPackage> packages = JsonSerializer.Deserialize<List<CondaPackage>>(result.StandardOutput, JsonOptions) ?? [];
        return packages.Select(p => new InstalledPackage(p.Name, p.Version)).ToList();
    }

    private void AddChannels(List<string> args)
    {
        foreach (string channel in Channels)
        {
            args.AddRange(["--channel", channel]);
        }
    }

    private async Task<PythonResult> RunMicromambaOrThrowAsync(
        PythonInstallation install, List<string> args, PythonErrorKind kind, string what, CancellationToken ct)
    {
        string? version = MicromambaVersion == "latest" ? null : MicromambaVersion;
        string micromamba = await Tools.EnsureAsync(install, "micromamba", ProvisionAsync, ct, version: version).ConfigureAwait(false);

        // Keep micromamba's package cache and state runtime-local, next to the binary.
        string toolsDirectory = Path.GetDirectoryName(Path.GetDirectoryName(micromamba)!)!;
        Dictionary<string, string> environment = new()
        {
            ["MAMBA_ROOT_PREFIX"] = Path.Combine(toolsDirectory, "mamba-root"),
        };

        return await RunOrThrowAsync(micromamba, args, kind, what, environment: environment, ct: ct).ConfigureAwait(false);
    }

    private async Task ProvisionAsync(ToolContext context, CancellationToken ct)
    {
        string asset = (OperatingSystem.IsWindows(), OperatingSystem.IsMacOS(), RuntimeInformation.OSArchitecture) switch
        {
            (true, _, Architecture.X64) => "micromamba-win-64.exe",
            (true, _, Architecture.Arm64) => "micromamba-win-arm64.exe",
            (_, true, Architecture.X64) => "micromamba-osx-64",
            (_, true, Architecture.Arm64) => "micromamba-osx-arm64",
            (_, _, Architecture.X64) => "micromamba-linux-64",
            (_, _, Architecture.Arm64) => "micromamba-linux-aarch64",
            _ => throw new PythonException(
                PythonErrorKind.UnsupportedPlatform, "No micromamba build for this platform."),
        };

        bool pinned = MicromambaVersion != "latest";
        string segment = pinned ? $"download/{MicromambaVersion}" : "latest/download";
        Uri uri = new($"https://github.com/mamba-org/micromamba-releases/releases/{segment}/{asset}");
        string downloaded = await context.Sources.DownloadAsync(uri, ct: ct).ConfigureAwait(false);

        string targetDirectory = Path.Combine(
            context.ToolsDirectory, pinned ? $"micromamba-{MicromambaVersion}" : "micromamba");
        Directory.CreateDirectory(targetDirectory);
        string target = Path.Combine(targetDirectory, OperatingSystem.IsWindows() ? "micromamba.exe" : "micromamba");
        File.Copy(downloaded, target, overwrite: true);
        if (!OperatingSystem.IsWindows())
        {
            File.SetUnixFileMode(target,
                UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute
                | UnixFileMode.GroupRead | UnixFileMode.GroupExecute
                | UnixFileMode.OtherRead | UnixFileMode.OtherExecute);
        }
    }

    private sealed record CondaPackage(
        [property: JsonPropertyName("name")] string Name,
        [property: JsonPropertyName("version")] string Version);
}
