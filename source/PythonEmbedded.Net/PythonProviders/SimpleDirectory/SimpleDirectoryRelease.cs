using PythonEmbedded.Net.Helpers;
using PythonEmbedded.Net.Models;

namespace PythonEmbedded.Net.PythonProviders.SimpleDirectory;

/// <summary>
/// Represents a Python release located in a simple directory structure. This class facilitates
/// managing and deploying Python releases from a specified asset path, providing functionality
/// to handle extraction and relocation of the installation files.
/// </summary>
public class SimpleDirectoryRelease : PythonRelease
{
    /// <summary>
    /// Gets the path to the asset associated with this Python release. This property specifies
    /// the location of the archive containing the Python distribution in a simple directory structure.
    /// </summary>
    public string AssetPath { get; }

    /// <summary>
    /// Represents a Python release located in a simple directory structure. This implementation
    /// allows managing and deploying Python releases from a specified asset path, providing
    /// functionality to extract and move the installation to a desired location.
    /// </summary>
    public SimpleDirectoryRelease(string assetPath, Version version, PlatformInfo platform, DateTime releaseBuildDate) :
        base(version, platform, releaseBuildDate)
    {
        AssetPath = assetPath;
    }

    /// <summary>
    /// Extracts, prepares, and reconfigures a Python release from the specified asset path
    /// to the provided directory. The method ensures that the Python installation is moved
    /// to the destination directory after extraction.
    /// </summary>
    /// <param name="directory">The target directory where the final Python installation will reside.</param>
    /// <param name="downloadDirectory">The intermediate directory where release files will be initially extracted.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <param name="downloadProgress">
    /// An optional progress reporter to track the progress of large data operations, measured in long values.
    /// </param>
    /// <returns>A task representing the asynchronous operation.</returns>
    public override async Task FetchReleaseAsync(string directory, string downloadDirectory,
        CancellationToken cancellationToken,
        IProgress<long>? downloadProgress = null)
    {
        // Step 1: Extract to temp extract directory
        await ArchiveHelper.ExtractAsync(AssetPath, downloadDirectory, cancellationToken).ConfigureAwait(false);

        // Step 2: ???
        string extractedPythonPath = FindPythonInstallPath(downloadDirectory);

        // Step 3: Profit!
        Directory.Move(extractedPythonPath, directory);
    }
}
