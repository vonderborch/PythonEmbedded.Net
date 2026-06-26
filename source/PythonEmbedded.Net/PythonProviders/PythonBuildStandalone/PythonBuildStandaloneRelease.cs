using Octokit;
using PythonEmbedded.Net.Models;

namespace PythonEmbedded.Net.PythonProviders.PythonBuildStandalone;

/// <summary>
/// Represents a standalone Python build release originating from the
/// python-build-standalone GitHub repository. This class extends the
/// functionality provided by the <see cref="PythonRelease"/> base class to
/// handle specifics of standalone Python releases.
/// </summary>
public class PythonBuildStandaloneRelease : PythonRelease
{
    /// <summary>
    /// Encapsulates the GitHub client used to interact with the GitHub API for fetching
    /// release information and metadata associated with standalone Python builds.
    /// Provides the underlying mechanism for handling GitHub repository communications
    /// specific to the python-build-standalone project.
    /// </summary>
    private GitHubClient _client;

    /// <summary>
    /// Represents the underlying GitHub release information associated with a Python build standalone release.
    /// Encapsulates metadata and assets fetched from the GitHub API for a specific release.
    /// </summary>
    private Release _release;

    /// <summary>
    /// Represents a Python build standalone release that extends the functionality
    /// of the base PythonRelease class. This class is specifically tied to
    /// releases fetched from the python-build-standalone GitHub repository.
    /// </summary>
    /// <param name="version">The version of the Python build.</param>
    /// <param name="platform">The platform for which the Python build is intended.</param>
    /// <param name="releaseBuildDate">The date when the release was built.</param>
    /// <param name="client">The GitHub client used to interact with the GitHub API.</param>
    /// <param name="release">The GitHub release information associated with the Python build standalone release.</param>
    internal PythonBuildStandaloneRelease(Version version, PlatformInfo platform, DateTime releaseBuildDate, GitHubClient client, Release release) : base(version, platform,
        releaseBuildDate)
    {
        _client = client;
        _release = release;
    }

    /// <summary>
    /// Creates a new instance of <see cref="PythonBuildStandaloneRelease"/> by fetching release
    /// information from the python-build-standalone GitHub repository.
    /// </summary>
    /// <param name="version">The version of the Python release.</param>
    /// <param name="platform">The platform for which the Python release is targeted.</param>
    /// <param name="releaseBuildDate">The date the release was built.</param>
    /// <param name="client">The GitHub client instance used to interact with the GitHub API.</param>
    /// <param name="httpReleaseTagName">The tag name of the release to fetch from the repository.</param>
    /// <param name="cancellationToken">A token used to cancel the asynchronous operation.</param>
    /// <returns>A task that represents the asynchronous operation. The task result contains
    /// an instance of <see cref="PythonBuildStandaloneRelease"/> initialized with the fetched release information.</returns>
    public static async Task<PythonBuildStandaloneRelease> Create(Version version, PlatformInfo platform,
        DateTime releaseBuildDate, GitHubClient client, string httpReleaseTagName, CancellationToken cancellationToken)
    {
        var release = await client.Repository.Release.Get(PythonBuildStandaloneConstants.RepositoryOwner, PythonBuildStandaloneConstants.RepositoryName, httpReleaseTagName).ConfigureAwait(false);
        return new PythonBuildStandaloneRelease(version, platform, releaseBuildDate, client, release);
    }

    public override async Task FetchRelease(string directory, string downloadDirectory)
    {
        var assetInfo = GetReleaseAsset();
        
        // Step 1: Download asset to temp download directory
        
        // Step 2: Extract to destination directory
        
        // Step 3: ???
        
        // Step 4: Profit!
    }

    /// <summary>
    /// Retrieves the release asset that matches the specified Python version and target platform from the current release.
    /// Determines whether the asset is a preferred style based on its naming conventions and compatibility.
    /// </summary>
    /// <returns>
    /// A tuple containing the selected release asset and a boolean indicating whether the asset is in the preferred style.
    /// </returns>
    /// <exception cref="InvalidOperationException">
    /// Thrown when no matching asset is found for the specified Python version and target platform.
    /// </exception>
    private (ReleaseAsset chooseAsset, bool isPreferredAsset) GetReleaseAsset()
    {
        // Collect all matching assets from this release
        ReleaseAsset? matchedAsset = null;
        bool isPreferredStyle = false;
        var targetTriple = Platform.TargetTriple!.ToLowerInvariant();
        
        foreach (var asset in _release.Assets)
        {
            var assetVersion = PythonProviderHelpers.GetVersionFromAssetName(asset.Name);
            if (PythonProviderHelpers.IsMatchingPlatform(asset.Name, targetTriple) && assetVersion is not null && PythonProviderHelpers.IsMatchingVersion(assetVersion, Version))
            {
                if (IsInstallOnlyArchive(asset.Name))
                {
                    matchedAsset = asset;
                    isPreferredStyle = true;
                    break;
                }
                else if (IsFullArchive(asset.Name))
                {
                    matchedAsset = asset;
                    isPreferredStyle = false;
                }
            }
        }
        
        if (matchedAsset is null)
        {
            throw new InvalidOperationException($"No matching asset found for Python version {Version} and target platform {targetTriple}.");
        }
        
        return (matchedAsset, isPreferredStyle);
    }

    /// <summary>
    /// Determines whether the provided file name corresponds to an install-only archive.
    /// Install-only archives are typically identified by the presence of "install" in the
    /// file name and the absence of "full".
    /// </summary>
    /// <param name="fileName">The name of the file to evaluate.</param>
    /// <returns>
    /// True if the file name indicates an install-only archive; otherwise, false.
    /// </returns>
    private static bool IsInstallOnlyArchive(string fileName)
    {
        // Install-only archives typically have "install" in the name
        return fileName.ToLowerInvariant().Contains("install") && 
               !fileName.ToLowerInvariant().Contains("full");
    }

    /// <summary>
    /// Determines whether the provided file name corresponds to a full archive.
    /// A full archive typically includes the term "full" or lacks the term "install,"
    /// and has an extension indicating a compressed archive format.
    /// </summary>
    /// <param name="fileName">The name of the file to evaluate as a potential full archive.</param>
    /// <returns>
    /// A boolean value indicating whether the file name corresponds to a full archive.
    /// Returns true if the file is identified as a full archive; otherwise, false.
    /// </returns>
    private static bool IsFullArchive(string fileName)
    {
        var lowerFileName = fileName.ToLowerInvariant();
        // Full archives typically don't have "install" in the name, or explicitly say "full"
        return lowerFileName.Contains("full") ||
               (!lowerFileName.Contains("install") && 
                (lowerFileName.EndsWith(".tar.zst", StringComparison.OrdinalIgnoreCase) ||
                 lowerFileName.EndsWith(".tar.gz", StringComparison.OrdinalIgnoreCase) ||
                 lowerFileName.EndsWith(".tar.bz2", StringComparison.OrdinalIgnoreCase) ||
                 lowerFileName.EndsWith(".tar.bz", StringComparison.OrdinalIgnoreCase) ||
                 lowerFileName.EndsWith(".zip", StringComparison.OrdinalIgnoreCase)));
    }
}
