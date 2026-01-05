using System.Runtime.CompilerServices;
using System.Text.Json.Serialization;
using PythonEmbedded.Net.Helpers;

namespace PythonEmbedded.Net.Models;

/// <summary>
/// Represents metadata associated with a Python runtime instance, including its version, build information, and installation details.
/// </summary>
public class InstanceMetadata
{
    /// <summary>
    /// Gets or sets the version of Python associated with the instance.
    /// </summary>
    public string PythonVersion { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the date indicating when the build was created.
    /// </summary>
    public DateTime BuildDate { get; set; } = DateTime.MinValue;

    /// <summary>
    /// Gets or sets a value indicating whether the current build was the most recent successful build.
    /// </summary>
    public bool WasLatestBuild { get; set; } = false;

    /// <summary>
    /// Gets or sets the date and time when the installation was completed.
    /// </summary>
    public DateTime InstallationDate { get; set; } = DateTime.Now;

    /// <summary>
    /// Gets or sets the collection of virtual environments managed by this instance.
    /// </summary>
    public List<VirtualEnvironmentMetadata> VirtualEnvironments { get; set; } = new();

    /// <summary>
    /// Gets the directory path where the metadata file is stored.
    /// </summary>
    [JsonIgnore]
    public string Directory { get; internal set; } = string.Empty;

    /// <summary>
    /// Gets the virtual environment metadata for the specified name.
    /// </summary>
    /// <param name="name">The name of the virtual environment.</param>
    /// <returns>The metadata if found, null otherwise.</returns>
    public VirtualEnvironmentMetadata? GetVirtualEnvironment(string name)
    {
        return VirtualEnvironments.FirstOrDefault(v => 
            string.Equals(v.Name, name, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// Adds or updates a virtual environment metadata entry.
    /// </summary>
    /// <param name="venvMetadata">The virtual environment metadata to add or update.</param>
    public void SetVirtualEnvironment(VirtualEnvironmentMetadata venvMetadata)
    {
        var existing = GetVirtualEnvironment(venvMetadata.Name);
        if (existing != null)
        {
            VirtualEnvironments.Remove(existing);
        }
        VirtualEnvironments.Add(venvMetadata);
    }

    /// <summary>
    /// Removes a virtual environment metadata entry.
    /// </summary>
    /// <param name="name">The name of the virtual environment to remove.</param>
    /// <returns>True if the entry was removed, false if it wasn't found.</returns>
    public bool RemoveVirtualEnvironment(string name)
    {
        var existing = GetVirtualEnvironment(name);
        if (existing != null)
        {
            VirtualEnvironments.Remove(existing);
            return true;
        }
        return false;
    }

    /// <summary>
    /// Checks whether the instance metadata file exists in the specified directory path.
    /// </summary>
    /// <param name="directory">The directory to check for the presence of the instance metadata file.</param>
    /// <returns>True if the metadata file exists; otherwise, false.</returns>
    public static bool Exists(string directory)
    {
        string path = GetPath(directory);
        return Path.Exists(path);
    }

    /// <summary>
    /// Loads the metadata for an instance from the specified directory path.
    /// </summary>
    /// <param name="directory">The directory containing the instance metadata file.</param>
    /// <returns>The <see cref="InstanceMetadata"/> object if successfully loaded; otherwise, null.</returns>
    public static InstanceMetadata? Load(string directory)
    {
        if (Exists(directory))
        {
            string path = GetPath(directory);
            InstanceMetadata? metadata = JsonHelpers.DeserializeFromFile<InstanceMetadata>(path);
            if (metadata is not null)
            {
                metadata.Directory = directory;
            }
            return metadata;
        }

        return null;
    }

    /// <summary>
    /// Saves the metadata for the current instance to a specified directory path.
    /// </summary>
    /// <param name="directory">The directory where the instance metadata file will be saved.</param>
    public void Save(string directory)
    {
        string path = GetPath(directory);
        JsonHelpers.SerializeToFile(path, this);
    }

    /// <summary>
    /// Combines the specified directory path with the instance metadata file name to generate the full file path.
    /// </summary>
    /// <param name="directory">The directory where the instance metadata file is located.</param>
    /// <returns>The full file path to the instance metadata file.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static string GetPath(string directory)
    {
        string path = Path.Combine(directory, Constants.InstanceMetadataFileName);
        return path;
    }
}
