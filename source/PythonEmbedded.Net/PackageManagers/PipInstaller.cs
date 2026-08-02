using PythonEmbedded.Net.Extensibility;
using PythonEmbedded.Net.Models;

namespace PythonEmbedded.Net.PackageManagers;

/// <summary>The default <see cref="IPackageInstaller"/>: <c>python -m venv</c> + <c>python -m pip</c>.</summary>
internal sealed class PipInstaller : PackageInstallerBase
{
    public override string Name => "pip";

    public override Task CreateEnvironmentAsync(PythonInstallation install, string envDirectory, CancellationToken ct)
        => CreateVenvAsync(install, envDirectory, ct);

    public override Task InstallAsync(PythonVirtualEnvironment env, PackageRequest request, CancellationToken ct)
        => PipInstallAsync(env, request, ct);

    public override Task UninstallAsync(PythonVirtualEnvironment env, string package, CancellationToken ct)
        => PipUninstallAsync(env, package, ct);

    public override Task<IReadOnlyList<InstalledPackage>> ListAsync(PythonVirtualEnvironment env, CancellationToken ct)
        => PipListAsync(env, ct);

    public override Task<bool> EnsureRequirementsAsync(PythonVirtualEnvironment env, string requirementsFile, CancellationToken ct)
        => PipEnsureRequirementsAsync(env, requirementsFile, ct);

    public override Task<IReadOnlyList<OutdatedPackage>> ListOutdatedAsync(PythonVirtualEnvironment env, CancellationToken ct)
        => PipListOutdatedAsync(env, ct);
}
