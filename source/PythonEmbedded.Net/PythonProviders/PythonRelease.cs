using PythonEmbedded.Net.Models;

namespace PythonEmbedded.Net;

/// <summary>
/// Represents a base class for Python releases, encapsulating necessary details
/// such as the version, platform, and release build date. The class provides an
/// abstraction for specific Python release implementations and includes a method
/// to fetch release information.
/// </summary>
public abstract class PythonRelease
{
    /// <summary>
    /// Represents a base class for Python releases, encapsulating common properties such as version,
    /// platform, and release build date, and providing an abstract method for fetching release-specific data.
    /// </summary>
    /// <param name="version">The version of the Python build.</param>
    /// <param name="platform">The platform for which the Python build is intended.</param>
    /// <param name="releaseBuildDate">The date when the release was built.</param>
    public PythonRelease(Version version, PlatformInfo platform, DateTime releaseBuildDate)
    {
        Version = version;
        Platform = platform;
        ReleaseBuildDate = releaseBuildDate;
    }

    /// <summary>
    /// Gets the version information for the Python release.
    /// </summary>
    public Version Version { get; }

    /// <summary>
    /// Gets the platform information, including operating system and architecture, for the Python release.
    /// </summary>
    public PlatformInfo Platform { get; }

    /// <summary>
    /// Gets the date when the Python release build was created.
    /// </summary>
    public DateTime ReleaseBuildDate { get; }

    public abstract Task FetchRelease(string directory, string downloadDirectory);
}
