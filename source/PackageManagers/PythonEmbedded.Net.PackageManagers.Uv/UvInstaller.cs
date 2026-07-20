using System.Text.Json;
using PythonEmbedded.Net.Exceptions;
using PythonEmbedded.Net.Extensibility;
using PythonEmbedded.Net.Models;

namespace PythonEmbedded.Net.PackageManagers.Uv;

/// <summary>
/// An <see cref="IPackageInstaller"/> backed by uv: <c>uv venv</c> for environment creation and
/// <c>uv pip</c> for package operations — typically an order of magnitude faster than pip.
/// uv itself is provisioned runtime-locally (pip-installed into the base interpreter) on first use;
/// system installs are never used. Activate with:
/// <code>PythonEnvironment.Configure(o => o.Installer = new UvInstaller());</code>
/// </summary>
public sealed class UvInstaller : PackageInstallerBase
{
    /// <inheritdoc />
    public override string Name => "uv";

    /// <summary>
    /// Seed new environments with pip (<c>uv venv --seed</c>) so tools expecting <c>python -m pip</c>
    /// keep working. Default true.
    /// </summary>
    public bool Seed { get; init; } = true;

    /// <summary>The uv version to provision, e.g. <c>"0.5.11"</c>. Default <c>null</c> (latest).</summary>
    public string? Version { get; init; }

    /// <inheritdoc />
    public override async Task CreateEnvironmentAsync(PythonInstallation install, string envDirectory, CancellationToken ct)
    {
        string uv = await EnsureUvAsync(install, ct).ConfigureAwait(false);

        List<string> args = ["venv", "--python", install.PythonExecutable];
        if (Seed)
        {
            args.Add("--seed");
        }

        args.Add(envDirectory);

        await RunOrThrowAsync(uv, args, PythonErrorKind.EnvironmentFailed, "uv venv", ct: ct).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public override async Task InstallAsync(PythonVirtualEnvironment env, PackageRequest request, CancellationToken ct)
    {
        List<string> args = ["pip", "install", "--python", env.PythonExecutable];
        if (request.IndexUrl is not null)
        {
            args.AddRange(["--index-url", request.IndexUrl]);
        }

        if (request.RequirementsFile is not null)
        {
            args.AddRange(["-r", request.RequirementsFile]);
        }

        args.AddRange(request.ExtraArgs);
        args.AddRange(request.Packages);

        string uv = await EnsureUvAsync(env.Installation, ct).ConfigureAwait(false);
        await RunOrThrowAsync(uv, args, PythonErrorKind.PackageOperationFailed, "uv pip install", ct: ct).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public override async Task UninstallAsync(PythonVirtualEnvironment env, string package, CancellationToken ct)
    {
        string uv = await EnsureUvAsync(env.Installation, ct).ConfigureAwait(false);
        await RunOrThrowAsync(
            uv,
            ["pip", "uninstall", "--python", env.PythonExecutable, package],
            PythonErrorKind.PackageOperationFailed,
            $"uv pip uninstall of '{package}'",
            ct: ct).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public override async Task<IReadOnlyList<InstalledPackage>> ListAsync(PythonVirtualEnvironment env, CancellationToken ct)
    {
        string uv = await EnsureUvAsync(env.Installation, ct).ConfigureAwait(false);
        PythonResult result = await RunOrThrowAsync(
            uv,
            ["pip", "list", "--python", env.PythonExecutable, "--format", "json"],
            PythonErrorKind.PackageOperationFailed,
            "uv pip list",
            ct: ct).ConfigureAwait(false);

        return JsonSerializer.Deserialize<List<InstalledPackage>>(result.StandardOutput, JsonOptions) ?? [];
    }

    private Task<string> EnsureUvAsync(PythonInstallation install, CancellationToken ct)
        => Tools.EnsureAsync(install, "uv", ProvisionAsync, ct, version: Version);

    /// <summary>
    /// pip-installs uv. With no <see cref="Version"/> pin it installs into the base interpreter,
    /// landing the binary next to it. A pinned version is installed into a private venv under the
    /// tools directory instead, so multiple pinned versions can coexist without clobbering each other.
    /// </summary>
    private Task ProvisionAsync(ToolContext context, CancellationToken ct)
        => Version is null
            ? ProvisionToolViaPipAsync(context, "uv", ct)
            : ProvisionPinnedToolAsync(context, "uv", Version, ct);
}
