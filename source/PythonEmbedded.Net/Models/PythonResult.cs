using PythonEmbedded.Net.Exceptions;

namespace PythonEmbedded.Net.Models;

/// <summary>The outcome of a buffered Python run.</summary>
public sealed record PythonResult(int ExitCode, string StandardOutput, string StandardError, TimeSpan Duration)
{
    /// <summary>Whether the process exited with code 0.</summary>
    public bool Success => ExitCode == 0;

    /// <summary>Returns this result, or throws <see cref="PythonProcessException"/> if the exit code is nonzero.</summary>
    public PythonResult EnsureSuccess()
    {
        if (!Success)
        {
            throw new PythonProcessException(
                $"Python exited with code {ExitCode}.{(string.IsNullOrWhiteSpace(StandardError) ? "" : $" stderr: {StandardError.Trim()}")}",
                this);
        }

        return this;
    }
}
