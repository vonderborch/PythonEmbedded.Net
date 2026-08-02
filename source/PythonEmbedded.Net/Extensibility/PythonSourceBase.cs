using PythonEmbedded.Net.Models;

namespace PythonEmbedded.Net.Extensibility;

/// <summary>
/// Optional base for <see cref="IPythonSource"/> implementations. The built-in sources (bundled
/// archives, astral downloads) share little beyond the contract itself, so this exists mainly for
/// symmetry with the other two interfaces and as a stable place for future shared helpers.
/// Implementing <see cref="IPythonSource"/> directly remains supported for callers who want to
/// implement multiple of these interfaces in one class.
/// </summary>
public abstract class PythonSourceBase : IPythonSource
{
    /// <inheritdoc />
    public abstract string Name { get; }

    /// <inheritdoc />
    public abstract Task<PythonInstallInfo?> TryInstallAsync(
        PythonVersionRequest request, string targetDirectory, SourceContext context,
        IProgress<InstallProgress>? progress, CancellationToken ct);
}
