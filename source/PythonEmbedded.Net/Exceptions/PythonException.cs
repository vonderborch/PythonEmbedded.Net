namespace PythonEmbedded.Net;

/// <summary>The exception thrown for all failures in PythonEmbedded.Net (except process failures, see <see cref="PythonProcessException"/>).</summary>
public class PythonException : Exception
{
    /// <summary>Creates the exception.</summary>
    public PythonException(PythonErrorKind kind, string message, Exception? innerException = null)
        : base(message, innerException)
    {
        Kind = kind;
    }

    /// <summary>What category of failure occurred.</summary>
    public PythonErrorKind Kind { get; }
}
