namespace PythonEmbedded.Net.Exceptions;

/// <summary>
/// Exception thrown when a metadata file is malformed or inaccessible.
/// </summary>
public class MetadataCorruptedException : PythonInstallationException
{
    /// <summary>
    /// Gets or sets the path to the corrupted metadata file.
    /// </summary>
    public string? MetadataFilePath { get; set; }

    /// <summary>
    /// Initializes a new instance of the <see cref="MetadataCorruptedException"/> class.
    /// </summary>
    public MetadataCorruptedException()
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="MetadataCorruptedException"/> class with a specified error message.
    /// </summary>
    /// <param name="message">The message that describes the error.</param>
    public MetadataCorruptedException(string message) : base(message)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="MetadataCorruptedException"/> class with a specified error message and a reference to the inner exception that is the cause of this exception.
    /// </summary>
    /// <param name="message">The error message that explains the reason for the exception.</param>
    /// <param name="innerException">The exception that is the cause of the current exception, or a null reference if no inner exception is specified.</param>
    public MetadataCorruptedException(string message, Exception innerException) : base(message, innerException)
    {
    }
}
