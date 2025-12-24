namespace PythonEmbedded.Net.Exceptions;

/// <summary>
/// Exception thrown when a specified virtual environment does not exist.
/// </summary>
public class VirtualEnvironmentNotFoundException : Exception
{
    /// <summary>
    /// Gets or sets the name of the virtual environment that was not found.
    /// </summary>
    public string? VirtualEnvironmentName { get; set; }

    /// <summary>
    /// Initializes a new instance of the <see cref="VirtualEnvironmentNotFoundException"/> class.
    /// </summary>
    public VirtualEnvironmentNotFoundException()
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="VirtualEnvironmentNotFoundException"/> class with a specified error message.
    /// </summary>
    /// <param name="message">The message that describes the error.</param>
    public VirtualEnvironmentNotFoundException(string message) : base(message)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="VirtualEnvironmentNotFoundException"/> class with a specified error message and a reference to the inner exception that is the cause of this exception.
    /// </summary>
    /// <param name="message">The error message that explains the reason for the exception.</param>
    /// <param name="innerException">The exception that is the cause of the current exception, or a null reference if no inner exception is specified.</param>
    public VirtualEnvironmentNotFoundException(string message, Exception innerException) : base(message, innerException)
    {
    }
}




