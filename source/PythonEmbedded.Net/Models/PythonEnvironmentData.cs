namespace PythonEmbedded.Net.Models;

/// <summary>
/// Represents metadata for a managed Python environment, including its name and file directory path.
/// This class is used to provide structured information about Python environments managed
/// within the application.
/// </summary>
public record PythonEnvironmentData
{
    /// <summary>
    /// Gets the unique name of the managed Python environment, related to the Python version being managed.
    /// </summary>
    public required string Name { get; init; }

    /// <summary>
    /// Gets the file system path where the managed Python environment is located.
    /// This property provides the directory path used for managing the environment's binaries and metadata.
    /// </summary>
    public required string Path { get; init; }
}
