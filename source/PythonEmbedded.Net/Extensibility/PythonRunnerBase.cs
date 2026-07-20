using PythonEmbedded.Net.Models;

namespace PythonEmbedded.Net.Extensibility;

/// <summary>
/// Optional base for <see cref="IPythonRunner"/> implementations. The subprocess and in-process
/// runners share no logic beyond the contract itself, so this exists mainly for symmetry with the
/// other two interfaces and as a stable place for future shared helpers. Implementing
/// <see cref="IPythonRunner"/> directly remains supported for callers who want to implement multiple
/// of these interfaces in one class.
/// </summary>
public abstract class PythonRunnerBase : IPythonRunner
{
    /// <inheritdoc />
    public abstract Task<PythonResult> RunAsync(PythonVirtualEnvironment env, PythonInvocation invocation, CancellationToken ct);
}
