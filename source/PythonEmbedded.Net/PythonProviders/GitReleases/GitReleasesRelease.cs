using Octokit;
using PythonEmbedded.Net.Exceptions;
using PythonEmbedded.Net.Helpers;
using PythonEmbedded.Net.Models;

namespace PythonEmbedded.Net.PythonProviders.GitReleases;

/// <summary>
/// Represents a standalone Python build release originating from the
/// python-build-standalone GitHub repository. This class extends the
/// functionality provided by the <see cref="PythonRelease"/> base class to
/// handle specifics of standalone Python releases.
/// </summary>
public class GitReleasesRelease : PythonRelease
{
    /// <summary>
    /// Represents the HTTP client used to send requests and receive responses from
    /// the GitHub API for fetching metadata and release assets related to the
    /// python-build-standalone GitHub repository. Facilitates communication
    /// with external services to retrieve release information required for
    /// managing standalone Python builds.
    /// </summary>
    private readonly HttpClient _httpClient;

    /// <summary>
    /// Represents the underlying GitHub release information associated with a Python build standalone release.
    /// Encapsulates metadata and assets fetched from the GitHub API for a specific release.
    /// </summary>
    private readonly Release _release;

    /// <summary>
    /// Represents a Python build standalone release that extends the functionality
    /// of the base PythonRelease class. This class is specifically tied to
    /// releases fetched from the python-build-standalone GitHub repository.
    /// </summary>
    /// <param name="version">The version of the Python build.</param>
    /// <param name="platform">The platform for which the Python build is intended.</param>
    /// <param name="releaseBuildDate">The date when the release was built.</param>
    /// <param name="httpClient">The HTTP client instance used to interact with the GitHub API.</param>
    /// <param name="release">The GitHub release information associated with the Python build standalone release.</param>
    internal GitReleasesRelease(Version version, PlatformInfo platform, DateTime releaseBuildDate, HttpClient httpClient, Release release) : base(version, platform,
        releaseBuildDate)
    {
        _httpClient = httpClient;
        _release = release;
    }

    /// <summary>
    /// Creates a new instance of <see cref="GitReleasesRelease"/> by fetching release
    /// information from the python-build-standalone GitHub repository.
    /// </summary>
    /// <param name="httpClient">The HTTP client instance used to interact with the GitHub API.</param>
    /// <param name="version">The version of the Python release.</param>
    /// <param name="platform">The platform for which the Python release is targeted.</param>
    /// <param name="releaseBuildDate">The date the release was built.</param>
    /// <param name="client">The GitHub client instance used to interact with the GitHub API.</param>
    /// <param name="httpReleaseTagName">The tag name of the release to fetch from the repository.</param>
    /// <param name="repositoryOwner">The name of the repository's owner.</param>
    /// <param name="repositoryName">The name of the repository.</param>
    /// <param name="cancellationToken">A token used to cancel the asynchronous operation.</param>
    /// <returns>A task that represents the asynchronous operation. The task result contains
    /// an instance of <see cref="GitReleasesRelease"/> initialized with the fetched release information.</returns>
    public static async Task<GitReleasesRelease> Create(HttpClient httpClient, Version version, PlatformInfo platform,
        DateTime releaseBuildDate, GitHubClient client, string httpReleaseTagName, string repositoryOwner, string repositoryName, CancellationToken cancellationToken)
    {
        var release = await client.Repository.Release.Get(repositoryOwner, repositoryName, httpReleaseTagName).ConfigureAwait(false);
        return new GitReleasesRelease(version, platform, releaseBuildDate, httpClient, release);
    }

    /// <summary>
    /// Asynchronously fetches a Python standalone release from the specified GitHub repository,
    /// downloads and extracts it, and moves the extracted Python paths to the target directory.
    /// </summary>
    /// <param name="directory">The target directory where the extracted Python release will be moved.</param>
    /// <param name="downloadDirectory">The directory used for downloading and extracting the release.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <param name="downloadProgress">An optional progress tracker for reporting download progress in bytes.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    public override async Task FetchReleaseAsync(string directory, string downloadDirectory,
        CancellationToken cancellationToken, IProgress<long>? downloadProgress = null)
    {
        var assetInfo = GetReleaseAsset();

        // Step 1: Download asset to temp download directory
        string tempDownloadPath = Path.Combine(downloadDirectory, "download");
        await DownloadAssetAsync(assetInfo.choosenAsset, tempDownloadPath, cancellationToken);

        // Step 2: Extract to temp extract directory
        string tempExtractPath = Path.Combine(downloadDirectory, "extracted");
        await ArchiveHelper.ExtractAsync(tempDownloadPath, tempExtractPath, cancellationToken).ConfigureAwait(false);

        // Step 3: ???
        string extractedPythonPath = FindPythonInstallPath(tempExtractPath);

        // Step 4: Profit!
        Directory.Move(extractedPythonPath, directory);
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
    private (ReleaseAsset choosenAsset, bool isPreferredAsset) GetReleaseAsset()
    {
        // Collect all matching assets from this release
        ReleaseAsset? matchedAsset = null;
        bool isPreferredStyle = false;
        var targetTriple = Platform.TargetTriple!.ToLowerInvariant();
        
        foreach (var asset in _release.Assets)
        {
            var assetVersion = AssetHelpers.GetVersionFromAssetName(asset.Name);
            if (AssetHelpers.IsMatchingPlatform(asset.Name, targetTriple) && assetVersion is not null && AssetHelpers.IsMatchingVersion(assetVersion, Version))
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
    /// Downloads a specific release asset from the GitHub repository asynchronously
    /// and saves it to the specified destination path.
    /// </summary>
    /// <param name="asset">The release asset to be downloaded.</param>
    /// <param name="destinationPath">The local directory where the asset will be saved.</param>
    /// <param name="cancellationToken">The token used to cancel the operation.</param>
    /// <param name="progress">Optional progress reporter to track the download progress, reporting the total bytes downloaded.</param>
    /// <returns>Returns the full file path of the downloaded asset.</returns>
    /// <exception cref="PythonInstallationException">
    /// Thrown when the download fails due to an HTTP error, a timeout, or other underlying exceptions.
    /// </exception>
    private async Task<string> DownloadAssetAsync(ReleaseAsset asset, string destinationPath,
        CancellationToken cancellationToken, IProgress<long>? progress = null)
    {
        TimeSpan originalTimeout = _httpClient.Timeout;
        try
        {
            var downloadUrl = asset.BrowserDownloadUrl;
            _httpClient.Timeout = TimeSpan.FromMinutes(30);
            
            using var response = await _httpClient.GetAsync(downloadUrl, HttpCompletionOption.ResponseHeadersRead, cancellationToken).ConfigureAwait(false);
            response.EnsureSuccessStatusCode();
            
            var fileName = Path.GetFileName(asset.Name);
            var filePath = Path.Combine(destinationPath, fileName);
            
            Directory.CreateDirectory(destinationPath);
            
            await using var fileStream = new FileStream(filePath, System.IO.FileMode.Create, FileAccess.Write, FileShare.None, 8192, true);
            await using var contentStream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);

            var buffer = new byte[8192];
            long totalBytesRead = 0;
            int bytesRead;

            progress?.Report(0);
            while ((bytesRead = await contentStream.ReadAsync(buffer, 0, buffer.Length, cancellationToken).ConfigureAwait(false)) > 0)
            {
                await fileStream.WriteAsync(buffer, 0, bytesRead, cancellationToken).ConfigureAwait(false);
                totalBytesRead += bytesRead;
                progress?.Report(totalBytesRead);
            }

            return filePath;
        }
        catch (HttpRequestException ex)
        {
            throw new PythonInstallationException(
                $"Failed to download asset {asset.Name}: {ex.Message}",
                ex);
        }
        catch (TaskCanceledException ex) when (ex.InnerException is TimeoutException)
        {
            throw new PythonInstallationException(
                $"Download timeout for asset {asset.Name}",
                ex);
        }
        finally
        {
            _httpClient.Timeout = originalTimeout;
        }
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
