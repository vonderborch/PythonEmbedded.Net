namespace PythonEmbedded.Net.Exceptions;

/// <summary>
/// Exception thrown when package installation fails.
/// </summary>
public class PackageInstallationException : Exception
{
    /// <summary>
    /// Gets or sets the package specification that failed to install.
    /// </summary>
    public string? PackageSpecification { get; set; }

    /// <summary>
    /// Gets or sets the installation output, if available.
    /// </summary>
    public string? InstallationOutput { get; set; }

    /// <summary>
    /// Initializes a new instance of the <see cref="PackageInstallationException"/> class.
    /// </summary>
    public PackageInstallationException()
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="PackageInstallationException"/> class with a specified error message.
    /// </summary>
    /// <param name="message">The message that describes the error.</param>
    public PackageInstallationException(string message) : base(message)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="PackageInstallationException"/> class with a specified error message and a reference to the inner exception that is the cause of this exception.
    /// </summary>
    /// <param name="message">The error message that explains the reason for the exception.</param>
    /// <param name="innerException">The exception that is the cause of the current exception, or a null reference if no inner exception is specified.</param>
    public PackageInstallationException(string message, Exception innerException) : base(message, innerException)
    {
    }
}



