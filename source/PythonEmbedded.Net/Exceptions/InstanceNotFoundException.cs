namespace PythonEmbedded.Net.Exceptions;

/// <summary>
/// Exception thrown when a requested Python instance is not found.
/// </summary>
public class InstanceNotFoundException : PythonInstallationException
{
    /// <summary>
    /// Gets or sets the Python version that was requested.
    /// </summary>
    public string? PythonVersion { get; set; }

    /// <summary>
    /// Gets or sets the build date that was requested.
    /// </summary>
    public DateTime? BuildDate { get; set; }

    /// <summary>
    /// Initializes a new instance of the <see cref="InstanceNotFoundException"/> class.
    /// </summary>
    public InstanceNotFoundException()
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="InstanceNotFoundException"/> class with a specified error message.
    /// </summary>
    /// <param name="message">The message that describes the error.</param>
    public InstanceNotFoundException(string message) : base(message)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="InstanceNotFoundException"/> class with a specified error message and a reference to the inner exception that is the cause of this exception.
    /// </summary>
    /// <param name="message">The error message that explains the reason for the exception.</param>
    /// <param name="innerException">The exception that is the cause of the current exception, or a null reference if no inner exception is specified.</param>
    public InstanceNotFoundException(string message, Exception innerException) : base(message, innerException)
    {
    }
}



