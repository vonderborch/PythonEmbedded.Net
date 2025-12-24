namespace PythonEmbedded.Net.Models;

/// <summary>
/// Represents metadata for managing Python runtime instances, providing functionality for loading and aggregating
/// metadata from a specified file path. This class handles multiple <see cref="InstanceMetadata"/> objects,
/// which represent individual Python runtime metadata.
/// </summary>
public class ManagerMetadata
{
    /// <summary>
    /// Represents a collection of <see cref="InstanceMetadata"/> objects associated with Python runtime instances.
    /// </summary>
    public List<InstanceMetadata> Instances;

    /// <summary>
    /// Represents metadata for managing instances, providing capabilities to load and aggregate metadata
    /// for multiple runtime instances from a specified file path.
    /// </summary>
    internal ManagerMetadata(string filePath)
    {
        // Load the subdirectories
        this.Instances = new();
        foreach (string directory in Directory.GetDirectories(filePath))
        {
            InstanceMetadata? instance = InstanceMetadata.Load(directory);
            if (instance != null)
            {
                Instances.Add(instance);
            }
        }
    }

    /// <summary>
    /// Retrieves an instance of <see cref="InstanceMetadata"/> matching the specified Python version
    /// and optionally a specific build date. If no build date is provided, it attempts to find the
    /// latest build for the given Python version.
    /// </summary>
    /// <param name="pythonVersion">The version of Python to locate in the managed instances.</param>
    /// <param name="buildDate">
    /// An optional parameter specifying the build date of the Python runtime to locate.
    /// If not provided, the method attempts to find the instance marked as the latest build.
    /// </param>
    /// <returns>
    /// An <see cref="InstanceMetadata"/> object representing the matching Python runtime instance, or
    /// <c>null</c> if no instance matches the criteria.
    /// </returns>
    public InstanceMetadata? FindInstance(string pythonVersion, DateTime? buildDate = null)
    {
        return this.Instances.FirstOrDefault(i => i.PythonVersion == pythonVersion && ((buildDate == null && i.WasLatestBuild) || (buildDate.HasValue && i.BuildDate.Date == buildDate.Value.Date)));
    }

    /// <summary>
    /// Removes a specified Python runtime instance from the collection of instances and deletes its associated
    /// directory from the file system, if it exists.
    /// </summary>
    /// <param name="instance">The <see cref="InstanceMetadata"/> object representing the runtime instance to remove.</param>
    /// <returns>
    /// True if the instance was successfully removed and its directory deleted, otherwise false if the instance
    /// was null or its directory did not exist.
    /// </returns>
    public bool RemoveInstance(InstanceMetadata? instance)
    {
        if (instance is null)
        {
            return false;
        }

        if (Path.Exists(instance.Directory))
        {
            Directory.Delete(instance.Directory, true);
            this.Instances.Remove(instance);
            return true;
        }

        return false;
    }

    /// <summary>
    /// Removes a Python runtime instance based on the specified Python version and optional build date.
    /// If the instance is found and removed successfully, the method returns true.
    /// </summary>
    /// <param name="pythonVersion">The Python version of the instance to be removed.</param>
    /// <param name="buildDate">The optional build date of the instance. If null, the latest build instance is considered.</param>
    /// <returns>Returns true if the instance was found and removed successfully; otherwise, false.</returns>
    public bool RemoveInstance(string pythonVersion, DateTime? buildDate = null)
    {
        InstanceMetadata? instance = FindInstance(pythonVersion, buildDate);
        if (instance is not null)
        {
            return RemoveInstance(instance);
        }

        return false;
    }

    /// <summary>
    /// Retrieves a read-only list of <see cref="InstanceMetadata"/> objects representing Python runtime instances
    /// managed by this instance of <see cref="ManagerMetadata"/>.
    /// </summary>
    /// <returns>A read-only list of Python runtime instance metadata.</returns>
    public IReadOnlyList<InstanceMetadata> GetInstances()
    {
        return this.Instances.AsReadOnly();
    }
}
