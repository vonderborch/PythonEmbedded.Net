namespace PythonEmbedded.Net.Runtime;

/// <summary>
/// Defines functionality for running Python processes within an embedded runtime context.
/// Provides methods to execute Python scripts or commands with configurable options
/// such as working directory, arguments, and environment variables.
/// </summary>
public interface IPythonProcessRunner
{
    /// <summary>
    /// Executes a Python process asynchronously with the specified parameters.
    /// </summary>
    /// <param name="executablePath">The path to the Python executable to be used.</param>
    /// <param name="arguments">A collection of command-line arguments to pass to the Python executable.</param>
    /// <param name="workingDirectory">The working directory in which the process should be executed.</param>
    /// <param name="environmentVariables">A dictionary of environment variables to set for the process, or null to use the default environment variables.</param>
    /// <param name="cancellationToken">A token that can be used to signal cancellation of the operation.</param>
    /// <returns>A task representing the asynchronous operation. The task result contains the execution result, including the exit code, standard output, and standard error of the Python process.</returns>
    Task<PythonExecutionResult> ExecuteAsync(
        string executablePath,
        IEnumerable<string> arguments,
        string workingDirectory,
        IReadOnlyDictionary<string, string>? environmentVariables = null,
        CancellationToken cancellationToken = default);
}
