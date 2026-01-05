using System.Text.Json.Serialization;

namespace PythonEmbedded.Net.Models;

/// <summary>
/// Represents metadata associated with a Python virtual environment.
/// </summary>
public class VirtualEnvironmentMetadata
{
    /// <summary>
    /// Gets or sets the name of the virtual environment.
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the actual path for external virtual environments.
    /// When set, the venv files are located at this path instead of the default location.
    /// </summary>
    public string? ExternalPath { get; set; }

    /// <summary>
    /// Gets or sets the date and time when the virtual environment was created.
    /// </summary>
    public DateTime CreatedDate { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Gets whether this virtual environment is stored at an external (non-default) location.
    /// </summary>
    [JsonIgnore]
    public bool IsExternal => !string.IsNullOrEmpty(ExternalPath);

    /// <summary>
    /// Gets the resolved path to the virtual environment directory.
    /// Returns ExternalPath if set, otherwise returns null (caller should use default path).
    /// </summary>
    /// <param name="defaultPath">The default path to use if not external.</param>
    /// <returns>The resolved path to the virtual environment.</returns>
    public string GetResolvedPath(string defaultPath)
    {
        return IsExternal ? ExternalPath! : defaultPath;
    }
}

