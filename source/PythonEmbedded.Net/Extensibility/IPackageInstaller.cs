namespace PythonEmbedded.Net;

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
}
