using PythonEmbedded.Net.Helpers;
using PythonEmbedded.Net.Models;

namespace PythonEmbedded.Net.PythonProviders;

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

    /// <summary>
    /// Asynchronously fetches a Python release by handling the download, extraction,
    /// and installation process. This method ensures the release is available at the
    /// specified directory.
    /// </summary>
    /// <param name="directory">The target directory where the Python release will be installed.</param>
    /// <param name="downloadDirectory">The directory used for temporary downloads and extractions during the fetch process.</param>
    /// <param name="cancellationToken">A token to signal the cancellation of the fetch operation.</param>
    /// <param name="downloadProgress">An optional progress indicator for tracking the download progress in bytes.</param>
    /// <returns>A task representing the asynchronous fetch operation.</returns>
    public abstract Task FetchReleaseAsync(string directory, string downloadDirectory,
        CancellationToken cancellationToken, IProgress<long>? downloadProgress = null);

    /// <summary>
    /// Searches for the Python installation path within the provided extracted directory.
    /// This method verifies the directory and its subdirectories to locate the Python executable.
    /// </summary>
    /// <param name="extractedDirectory">The root directory where the Python distribution has been extracted.</param>
    /// <returns>
    /// The determined path to the Python installation, either the root directory or a subdirectory
    /// containing the required Python executable.
    /// </returns>
    protected string FindPythonInstallPath(string extractedDirectory)
    {
        // Python distributions from python-build-standalone typically extract to a subdirectory
        // Look for the Python executable to find the actual installation path
        
        var directories = Directory.GetDirectories(extractedDirectory);
        
        // Check if the extracted directory itself contains the Python executable
        if (ArchiveHelper.VerifyExtractedInstallation(extractedDirectory))
        {
            return extractedDirectory;
        }
        
        // Check subdirectories
        foreach (var subDir in directories)
        {
            if (ArchiveHelper.VerifyExtractedInstallation(subDir))
            {
                return subDir;
            }
            
            // Check nested subdirectories (some archives have multiple levels)
            var nestedDirs = Directory.GetDirectories(subDir);
            foreach (var nestedDir in nestedDirs)
            {
                if (ArchiveHelper.VerifyExtractedInstallation(nestedDir))
                {
                    return nestedDir;
                }
            }
        }
        
        // If we can't find it, return the extracted directory anyway
        // The verification should have caught this earlier, but return something\
        return extractedDirectory;
    }
}
