namespace PythonEmbedded.Net.Exceptions;

/// <summary>
/// Exception thrown when a requirements file is invalid or cannot be processed.
/// </summary>
public class RequirementsFileException : PackageInstallationException
{
    /// <summary>
    /// Gets or sets the path to the requirements file that caused the error.
    /// </summary>
    public string? RequirementsFilePath { get; set; }

    /// <summary>
    /// Initializes a new instance of the <see cref="RequirementsFileException"/> class.
    /// </summary>
    public RequirementsFileException()
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="RequirementsFileException"/> class with a specified error message.
    /// </summary>
    /// <param name="message">The message that describes the error.</param>
    public RequirementsFileException(string message) : base(message)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="RequirementsFileException"/> class with a specified error message and a reference to the inner exception that is the cause of this exception.
    /// </summary>
    /// <param name="message">The error message that explains the reason for the exception.</param>
    /// <param name="innerException">The exception that is the cause of the current exception, or a null reference if no inner exception is specified.</param>
    public RequirementsFileException(string message, Exception innerException) : base(message, innerException)
    {
    }
}




