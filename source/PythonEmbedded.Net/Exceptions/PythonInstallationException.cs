namespace PythonEmbedded.Net.Exceptions;

/// <summary>
/// Base exception for installation-related errors in PythonEmbedded.Net.
/// </summary>
public class PythonInstallationException : Exception
{
    /// <summary>
    /// Initializes a new instance of the <see cref="PythonInstallationException"/> class.
    /// </summary>
    public PythonInstallationException()
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="PythonInstallationException"/> class with a specified error message.
    /// </summary>
    /// <param name="message">The message that describes the error.</param>
    public PythonInstallationException(string message) : base(message)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="PythonInstallationException"/> class with a specified error message and a reference to the inner exception that is the cause of this exception.
    /// </summary>
    /// <param name="message">The error message that explains the reason for the exception.</param>
    /// <param name="innerException">The exception that is the cause of the current exception, or a null reference if no inner exception is specified.</param>
    public PythonInstallationException(string message, Exception innerException) : base(message, innerException)
    {
    }
}






