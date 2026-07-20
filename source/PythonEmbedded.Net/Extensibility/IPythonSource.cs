namespace PythonEmbedded.Net;

/// <summary>
/// Where Python installations come from. Implementations materialize a full install tree on request
/// (bundled archives, astral downloads, user directories, source builds, ...).
/// </summary>
public interface IPythonSource
{
    /// <summary>Short identifier used in install directory names and diagnostics, e.g. <c>"astral"</c>.</summary>
    string Name { get; }

    /// <summary>
    /// Returns null if this source cannot satisfy <paramref name="request"/>; otherwise materializes
    /// a complete installation into <paramref name="targetDirectory"/> and returns its metadata.
    /// </summary>
    Task<PythonInstallInfo?> TryInstallAsync(
        PythonVersionRequest request, string targetDirectory, SourceContext context, CancellationToken ct);
}
