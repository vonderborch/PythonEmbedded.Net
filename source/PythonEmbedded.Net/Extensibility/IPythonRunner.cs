using PythonEmbedded.Net.Models;

namespace PythonEmbedded.Net.Extensibility;

/// <summary>
/// How Python code executes. The default runs a buffered subprocess; satellites can run in-process
/// (pythonnet) or elsewhere.
/// </summary>
public interface IPythonRunner
{
    /// <summary>Executes <paramref name="invocation"/> in <paramref name="env"/> and returns the buffered result (never throws on nonzero exit — the caller applies <see cref="RunOptions.ThrowOnError"/>).</summary>
    Task<PythonResult> RunAsync(PythonVirtualEnvironment env, PythonInvocation invocation, CancellationToken ct);
}
