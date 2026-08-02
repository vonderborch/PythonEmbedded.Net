using PythonEmbedded.Net.Exceptions;
using PythonEmbedded.Net.Extensibility;
using PythonEmbedded.Net.Models;

namespace PythonEmbedded.Net.PackageManagers.Poetry;

/// <summary>
/// An <see cref="IPackageInstaller"/> that installs a pyproject.toml project's dependencies with
/// Poetry (<see cref="PackageRequest.ProjectDirectory"/>); everything else (ad-hoc installs,
/// uninstall, list) passes through to the environment's pip, which matches how Poetry itself
/// treats environments. Poetry is provisioned runtime-locally (pip-installed into the base
/// interpreter) on first use. Activate with:
/// <code>PythonEnvironment.Configure(o => o.Installer = new PoetryInstaller());</code>
/// </summary>
public sealed class PoetryInstaller : PackageInstallerBase
{
    /// <inheritdoc />
    public override string Name => "poetry";

    /// <summary>Include the project's dev/group dependencies when installing a project. Default false.</summary>
    public bool WithDevDependencies { get; init; }

    /// <summary>The Poetry version to provision, e.g. <c>"1.8.3"</c>. Default <c>null</c> (latest).</summary>
    public string? Version { get; init; }

    /// <inheritdoc />
    public override Task CreateEnvironmentAsync(PythonInstallation install, string envDirectory, CancellationToken ct)
        => CreateVenvAsync(install, envDirectory, ct);

    /// <inheritdoc />
    public override async Task InstallAsync(PythonVirtualEnvironment env, PackageRequest request, CancellationToken ct)
    {
        if (request.ProjectDirectory is null)
        {
            await PipInstallAsync(env, request, ct).ConfigureAwait(false);
            return;
        }

        string poetry = await Tools.EnsureAsync(env.Installation, "poetry", ProvisionAsync, ct, version: Version).ConfigureAwait(false);

        List<string> args = ["install"];
        if (!WithDevDependencies)
        {
            args.Add("--only");
            args.Add("main");
        }

        args.AddRange(request.ExtraArgs);

        // POETRY_VIRTUALENVS_CREATE=false + VIRTUAL_ENV make poetry install into our env.
        Dictionary<string, string> environment = new()
        {
            ["POETRY_VIRTUALENVS_CREATE"] = "false",
            ["VIRTUAL_ENV"] = env.Directory,
            ["PATH"] = Path.GetDirectoryName(env.PythonExecutable) + Path.PathSeparator
                + (Environment.GetEnvironmentVariable("PATH") ?? string.Empty),
        };

        await RunOrThrowAsync(
            poetry, args, PythonErrorKind.PackageOperationFailed, "poetry install",
            workingDirectory: request.ProjectDirectory, environment: environment, ct: ct).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public override Task UninstallAsync(PythonVirtualEnvironment env, string package, CancellationToken ct)
        => PipUninstallAsync(env, package, ct);

    /// <inheritdoc />
    public override Task<IReadOnlyList<InstalledPackage>> ListAsync(PythonVirtualEnvironment env, CancellationToken ct)
        => PipListAsync(env, ct);

    /// <inheritdoc />
    public override Task<bool> EnsureRequirementsAsync(PythonVirtualEnvironment env, string requirementsFile, CancellationToken ct)
        => PipEnsureRequirementsAsync(env, requirementsFile, ct);

    /// <inheritdoc />
    public override Task<IReadOnlyList<OutdatedPackage>> ListOutdatedAsync(PythonVirtualEnvironment env, CancellationToken ct)
        => PipListOutdatedAsync(env, ct);

    /// <summary>
    /// pip-installs poetry. With no <see cref="Version"/> pin it installs into the base interpreter,
    /// landing the binary next to it. A pinned version is installed into a private venv under the
    /// tools directory instead, so multiple pinned versions can coexist without clobbering each other.
    /// </summary>
    private Task ProvisionAsync(ToolContext context, CancellationToken ct)
        => Version is null
            ? ProvisionToolViaPipAsync(context, "poetry", ct)
            : ProvisionPinnedToolAsync(context, "poetry", Version, ct);
}
