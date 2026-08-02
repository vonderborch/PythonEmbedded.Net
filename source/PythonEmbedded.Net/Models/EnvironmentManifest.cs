namespace PythonEmbedded.Net.Models;

/// <summary>
/// A portable record of an environment's package set, written by <see cref="PythonVirtualEnvironment.ExportManifestAsync"/>
/// and consumed by <see cref="PythonInstallation.ImportEnvironmentAsync"/>. Captures the installed package list rather
/// than a binary directory snapshot, since venv directories bake in absolute paths that don't survive relocation.
/// </summary>
/// <param name="InstallerName">The <see cref="Extensibility.IPackageInstaller.Name"/> that produced the exported environment.</param>
/// <param name="PythonVersion">The Python version string of the exporting installation.</param>
/// <param name="Packages">The exported environment's installed packages.</param>
/// <param name="ExportedAt">When the manifest was written.</param>
public sealed record EnvironmentManifest(
    string InstallerName,
    string PythonVersion,
    IReadOnlyList<InstalledPackage> Packages,
    DateTimeOffset ExportedAt);
