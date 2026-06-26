namespace PythonEmbedded.Net.Runtime;

/// <summary>
/// Represents the result of executing a Python script or command in a subprocess.
/// Encapsulates the exit code, standard output, and standard error produced during execution.
/// </summary>
/// <param name="ExitCode">The exit code returned by the Python process.</param>
/// <param name="StandardOutput">The standard output produced by the Python process.</param>
/// <param name="StandardError">The standard error produced by the Python process.</param>
public record PythonExecutionResult(int ExitCode, string StandardOutput = "", string StandardError = "");
