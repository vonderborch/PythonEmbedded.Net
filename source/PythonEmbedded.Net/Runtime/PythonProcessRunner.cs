using System.Diagnostics;
using System.Text;

namespace PythonEmbedded.Net.Runtime;

/// <summary>
/// Represents a runner responsible for executing Python processes.
/// </summary>
public sealed class PythonProcessRunner : IPythonProcessRunner
{
    /// <summary>
    /// Executes a Python process with the specified parameters asynchronously and returns the result.
    /// </summary>
    /// <param name="executablePath">
    /// The full path to the Python executable to be run.
    /// </param>
    /// <param name="arguments">
    /// The collection of command-line arguments to pass to the Python process.
    /// </param>
    /// <param name="workingDirectory">
    /// The working directory in which the process will run.
    /// </param>
    /// <param name="environmentVariables">
    /// An optional dictionary of environment variables to set for the process. If not provided, the default environment variables are used.
    /// </param>
    /// <param name="cancellationToken">
    /// A cancellation token that can be used to cancel the operation.
    /// </param>
    /// <returns>
    /// A task that represents the asynchronous operation. The task result is a <see cref="PythonExecutionResult"/> containing the exit code, standard output, and standard error of the process.
    /// </returns>
    /// <exception cref="InvalidOperationException">
    /// Thrown if the process fails to start.
    /// </exception>
    public async Task<PythonExecutionResult> ExecuteAsync(
        string executablePath,
        IEnumerable<string> arguments,
        string workingDirectory,
        IReadOnlyDictionary<string, string>? environmentVariables = null,
        CancellationToken cancellationToken = default)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = executablePath,
            WorkingDirectory = workingDirectory,
            UseShellExecute = false,
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true,
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8
        };

        foreach (var argument in arguments)
        {
            startInfo.ArgumentList.Add(argument);
        }

        if (environmentVariables is not null)
        {
            foreach (var (key, value) in environmentVariables)
            {
                startInfo.Environment[key] = value;
            }
        }

        using var process = Process.Start(startInfo);
        if (process is null)
        {
            throw new InvalidOperationException($"Failed to start process: {executablePath}");
        }

        process.StandardInput.Close();

        var stdoutTask = process.StandardOutput.ReadToEndAsync(cancellationToken);
        var stderrTask = process.StandardError.ReadToEndAsync(cancellationToken);

        await process.WaitForExitAsync(cancellationToken).ConfigureAwait(false);

        return new PythonExecutionResult(
            process.ExitCode,
            await stdoutTask.ConfigureAwait(false),
            await stderrTask.ConfigureAwait(false));
    }
}
