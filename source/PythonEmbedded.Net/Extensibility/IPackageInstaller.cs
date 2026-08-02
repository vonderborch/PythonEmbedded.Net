using PythonEmbedded.Net.Models;

namespace PythonEmbedded.Net.Extensibility;

/// <summary>
/// How virtual environments are created and packages managed (pip by default; uv/conda/poetry via
/// satellite packages).
/// </summary>
public interface IPackageInstaller
{
    /// <summary>Short identifier recorded in environment metadata, e.g. <c>"pip"</c>.</summary>
    string Name { get; }

    /// <summary>Creates a virtual environment for <paramref name="install"/> at <paramref name="envDirectory"/> (in place; never moved afterwards).</summary>
    Task CreateEnvironmentAsync(PythonInstallation install, string envDirectory, CancellationToken ct);

    /// <summary>Installs packages into <paramref name="env"/>.</summary>
    Task InstallAsync(PythonVirtualEnvironment env, PackageRequest request, CancellationToken ct);

    /// <summary>Uninstalls a package from <paramref name="env"/>.</summary>
    Task UninstallAsync(PythonVirtualEnvironment env, string package, CancellationToken ct);

    /// <summary>Lists the packages installed in <paramref name="env"/>.</summary>
    Task<IReadOnlyList<InstalledPackage>> ListAsync(PythonVirtualEnvironment env, CancellationToken ct);

    /// <summary>
    /// Installs <paramref name="requirementsFile"/> into <paramref name="env"/> and returns whether the
    /// installed package set actually changed (false when everything was already satisfied).
    /// </summary>
    Task<bool> EnsureRequirementsAsync(PythonVirtualEnvironment env, string requirementsFile, CancellationToken ct);

    /// <summary>Lists packages installed in <paramref name="env"/> for which a newer version is available.</summary>
    Task<IReadOnlyList<OutdatedPackage>> ListOutdatedAsync(PythonVirtualEnvironment env, CancellationToken ct);
}
