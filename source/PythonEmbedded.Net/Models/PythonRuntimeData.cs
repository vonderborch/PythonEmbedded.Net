using System.Text.Json.Serialization;
using PythonEmbedded.Net.Helpers;

namespace PythonEmbedded.Net.Models;

/// <summary>
/// Represents runtime metadata for a Python environment.
/// Provides details about the Python interpreter, its version, build date, backend,
/// and associated managed environments.
/// </summary>
public class PythonRuntimeData
{
    /// <summary>
    /// Stores metadata for managed Python environments associated with the runtime.
    /// This dictionary maps environment names to their corresponding details, providing
    /// a structured way to manage and access Python environment information.
    /// </summary>
    private Dictionary<string, PythonEnvironmentData> _environments;
    
    /// <summary>
    /// Represents runtime metadata information for a Python environment.
    /// This includes details such as the Python version, build date,
    /// backend interpreter, and associated managed environments.
    /// </summary>
    /// <param name="pythonVersion">The version of the Python interpreter.</param>
    /// <param name="buildDate">The date on which the runtime was compiled.</param>
    /// <param name="backend">The name of the backend interpreter used by the runtime.</param>
    /// <param name="environments">The managed environments associated with the runtime.</param>
    [JsonConstructor]
    internal PythonRuntimeData(Version pythonVersion, string buildDate, string backend,
        Dictionary<string, PythonEnvironmentData> environments)
    {
        PythonVersion = pythonVersion;
        BuildDate = buildDate;
        Backend = backend;
        _environments = new Dictionary<string, PythonEnvironmentData>(environments);
    }

    /// <summary>
    /// Gets the version of the Python runtime environment.
    /// This property provides information about the specific Python interpreter version
    /// being used, including major, minor, and patch version details.
    /// </summary>
    [JsonPropertyName("pythonVersion")]
    public Version PythonVersion { get; private set; }

    /// <summary>
    /// Provides a formatted string representation of the Python version.
    /// Converts the version to a string format where dots are replaced with underscores,
    /// offering a compatible format for scenarios requiring non-standard version formatting.
    /// </summary>
    [JsonIgnore]
    public string PythonVersionString => PythonVersion.ToString().Replace(".", "_");

    /// <summary>
    /// Gets the build date of the Python runtime environment.
    /// This property provides the date on which the runtime was constructed or compiled,
    /// enabling the application to access information about the runtime's creation timestamp.
    /// </summary>
    [JsonPropertyName("buildDate")]
    public string BuildDate { get; private set; }

    /// <summary>
    /// Gets the identifier for the backend Python interpreter associated with the runtime.
    /// This property specifies which backend interpreter the runtime is using, enabling the application
    /// to determine the specific Python implementation or package manager it is leveraging.
    /// </summary>
    [JsonPropertyName("backend")]
    public string Backend { get; private set; }

    /// <summary>
    /// Gets the collection of managed Python environments associated with the runtime.
    /// The key represents the environment name, and the value contains metadata information
    /// about the managed Python environment, including its name and file path.
    /// This property is used to track and manage multiple Python environments for the application.
    /// </summary>
    [JsonPropertyName("environments")]
    public Dictionary<string, PythonEnvironmentData> Environments => new(_environments);

    /// <summary>
    /// Represents the directory path where the runtime metadata and associated files
    /// are stored. This property is used to determine and manage the file system
    /// location of the Python runtime information for persistent storage and retrieval.
    /// </summary>
    [JsonIgnore]
    public string Directory { get; internal set; } = "";

    /// <summary>
    /// Gets or sets the file path associated with the Python runtime metadata.
    /// This property holds the full path to the runtime data file on the filesystem,
    /// which is typically used for loading or saving runtime metadata information.
    /// </summary>
    [JsonIgnore]
    public string FilePath { get; internal set; } = "";

    /// <summary>
    /// Represents the file system path to the directory containing the Python executable
    /// associated with the runtime. This path is constructed based on the runtime's directory
    /// and predefined constants for the executable directory name.
    /// </summary>
    [JsonIgnore]
    public string PythonExecutableDirectoryPath { get; internal set; }

    /// <summary>
    /// Specifies the directory path used to store and organize Python environment configurations
    /// associated with the runtime. This path serves as the root location for managing
    /// environment-related files and directories.
    /// </summary>
    [JsonIgnore]
    public string EnvironmentsDirectory { get; internal set; }

    /// <summary>
    /// Updates the build date of the Python runtime metadata.
    /// This method modifies the runtime's build date to the newly specified value
    /// and saves the updated data to the associated file path.
    /// </summary>
    /// <param name="newBuildDate">The new build date to set for the runtime.</param>
    public void UpdateBuildDate(string newBuildDate)
    {
        BuildDate = newBuildDate;
        Save();
    }

    /// <summary>
    /// Adds a new managed Python environment to the runtime metadata.
    /// This method updates the collection of environments by associating the specified
    /// environment name with its corresponding metadata. The change is immediately persisted
    /// to the storage file.
    /// </summary>
    /// <param name="newEnvironmentName">The name of the new environment to be added.</param>
    /// <param name="environmentData">The metadata associated with the new environment.</param>
    public void AddManagedEnvironments(string newEnvironmentName, PythonEnvironmentData environmentData)
    {
        Environments.Add(newEnvironmentName, environmentData);
        Save();
    }

    /// <summary>
    /// Removes a managed Python environment from the runtime's environment list.
    /// If the specified environment is successfully removed, the changes are persisted.
    /// </summary>
    /// <param name="environmentName">The name of the managed environment to be removed.</param>
    public void RemoveManagedEnvironments(string environmentName)
    {
        bool removed = Environments.Remove(environmentName);
        if (removed)
        {
            Save();
        }
    }

    /// <summary>
    /// Loads an existing Python runtime metadata file from the specified directory if it exists;
    /// otherwise, creates a new metadata object with the given parameters and initializes default directories and paths.
    /// </summary>
    /// <param name="directory">The directory where the runtime metadata file is stored or will be created.</param>
    /// <param name="pythonVersion">The version of the Python interpreter used in the runtime.</param>
    /// <param name="buildDate">The build date of the Python runtime environment.</param>
    /// <param name="backend">The name of the backend interpreter associated with the runtime.</param>
    /// <returns>A <see cref="PythonRuntimeData"/> instance representing the loaded or newly created runtime metadata.</returns>
    internal static PythonRuntimeData LoadOrCreate(string directory, Version pythonVersion, string buildDate,
        string backend)
    {
        var filePath = Path.Combine(directory, Constants.MetadataFileName);

        PythonRuntimeData data = File.Exists(filePath) ? JsonHelpers.DeserializeFromFile<PythonRuntimeData>(filePath)! : new PythonRuntimeData(pythonVersion, buildDate, backend, new Dictionary<string, PythonEnvironmentData>());
        data.Directory = directory;
        data.FilePath = filePath;
        data.PythonExecutableDirectoryPath = Path.Combine(directory, Constants.ExecutableDirectoryName, Constants.ExecutableDirectoryName);
        data.EnvironmentsDirectory = Path.Combine(directory, Constants.EnvironmentsDirectoryName);
        
        return data;
    }

    /// <summary>
    /// Saves the current Python runtime metadata to the associated file path.
    /// This method persists the runtime data to the file specified in the
    /// <see cref="FilePath"/> property. If the file path is not set, the
    /// save operation will not proceed.
    /// </summary>
    public void Save()
    {
        JsonHelpers.SerializeToFile(FilePath, this);
    }
}
