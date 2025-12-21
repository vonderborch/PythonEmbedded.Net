using System.Diagnostics;

namespace PythonEmbedded.Net.Services;

/// <summary>
/// Interface for executing external processes.
/// </summary>
public interface IProcessExecutor
{
    /// <summary>
    /// Executes a process with the specified arguments and handles stdin/stdout/stderr.
    /// </summary>
    Task<ProcessExecutionResult> ExecuteAsync(
        ProcessStartInfo startInfo,
        Func<string?>? stdinHandler = null,
        Action<string>? stdoutHandler = null,
        Action<string>? stderrHandler = null,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Represents the result of executing a process.
/// </summary>
public record ProcessExecutionResult(
    int ExitCode,
    string StandardOutput = "",
    string StandardError = "");

