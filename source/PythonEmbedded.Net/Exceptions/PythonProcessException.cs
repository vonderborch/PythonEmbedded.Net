using PythonEmbedded.Net.Models;

namespace PythonEmbedded.Net.Exceptions;

/// <summary>Thrown when a Python process exits with a nonzero code (or is killed on timeout); carries the full output.</summary>
public sealed class PythonProcessException : PythonException
{
    /// <summary>Creates the exception.</summary>
    public PythonProcessException(string message, PythonResult result, PythonErrorKind kind = PythonErrorKind.ExecutionFailed)
        : base(kind, message)
    {
        Result = result;
    }

    /// <summary>The exit code, stdout, and stderr of the failed process.</summary>
    public PythonResult Result { get; }
}
