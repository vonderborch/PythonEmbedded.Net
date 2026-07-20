using PythonEmbedded.Net.Extensibility;

namespace PythonEmbedded.Net.Models;

/// <summary>Metadata describing a materialized Python installation, returned by an <see cref="IPythonSource"/>.</summary>
/// <param name="Version">The concrete version that was installed.</param>
/// <param name="SourceName">The <see cref="IPythonSource.Name"/> that produced it.</param>
/// <param name="Triple">The platform triple of the build.</param>
/// <param name="InstalledAt">When the installation was materialized.</param>
/// <param name="Checksum">SHA-256 of the source archive, if known.</param>
/// <param name="RelativePythonPath">
/// Path of the python executable relative to the install directory, if the source knows it
/// (e.g. <c>python/bin/python3</c>). When null, standard locations are probed.
/// </param>
public sealed record PythonInstallInfo(
    PythonVersion Version,
    string SourceName,
    string Triple,
    DateTimeOffset InstalledAt,
    string? Checksum = null,
    string? RelativePythonPath = null);
