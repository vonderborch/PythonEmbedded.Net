using Octokit;
using PythonEmbedded.Net.Exceptions;
using PythonEmbedded.Net.Models;

namespace PythonEmbedded.Net.Helpers;

/// <summary>
/// Helper class for interacting with GitHub Releases API to find and download Python distributions.
/// Downloads are sourced from python-build-standalone (https://github.com/astral-sh/python-build-standalone).
/// We are not associated with astral-sh, but thank them for their fantastic work.
/// </summary>
internal static class GitHubReleaseHelper
{
    private const string RepositoryOwner = "astral-sh";
    private const string RepositoryName = "python-build-standalone";

    /// <summary>
    /// Finds a release asset for the specified Python version, build date, and platform.
    /// </summary>
    public static async Task<ReleaseAsset?> FindReleaseAssetAsync(
        GitHubClient client,
        string pythonVersion,
        string? buildDate,
        PlatformInfo platform,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(pythonVersion))
            throw new ArgumentException("Python version cannot be null or empty.", nameof(pythonVersion));
        if (platform == null)
            throw new ArgumentNullException(nameof(platform));
        if (string.IsNullOrWhiteSpace(platform.TargetTriple))
            throw new ArgumentException("Platform target triple is required.", nameof(platform));

        try
        {
            // Get all releases
            var releases = await client.Repository.Release.GetAll(RepositoryOwner, RepositoryName).ConfigureAwait(false);
            
            // Filter releases by build date if specified
            var matchingReleases = releases.Where(r => IsMatchingRelease(r, pythonVersion, buildDate)).ToList();

            if (!matchingReleases.Any())
            {
                throw new InstanceNotFoundException(
                    $"No matching release found for Python {pythonVersion} and build date {buildDate ?? "latest"}")
                {
                    PythonVersion = pythonVersion,
                    BuildDate = buildDate
                };
            }

            // Prefer install-only archives, fallback to full archives
            var preferredAssets = new List<ReleaseAsset>();
            var fallbackAssets = new List<ReleaseAsset>();

            foreach (var release in matchingReleases.OrderByDescending(r => r.PublishedAt))
            {
                var assets = release.Assets;
                foreach (var asset in assets)
                {
                    if (IsMatchingAsset(asset, pythonVersion, platform.TargetTriple))
                    {
                        if (IsInstallOnlyArchive(asset.Name))
                        {
                            preferredAssets.Add(asset);
                        }
                        else if (IsFullArchive(asset.Name))
                        {
                            fallbackAssets.Add(asset);
                        }
                    }
                }

                if (preferredAssets.Any())
                {
                    return preferredAssets.OrderByDescending(a => a.UpdatedAt).First();
                }
            }

            if (fallbackAssets.Any())
            {
                return fallbackAssets.OrderByDescending(a => a.UpdatedAt).First();
            }

            throw new InstanceNotFoundException(
                $"No matching asset found for Python {pythonVersion}, build date {buildDate ?? "latest"}, and platform {platform.TargetTriple}")
            {
                PythonVersion = pythonVersion,
                BuildDate = buildDate
            };
        }
        catch (NotFoundException)
        {
            throw new InstanceNotFoundException(
                $"Release not found for Python {pythonVersion} and build date {buildDate ?? "latest"}")
            {
                PythonVersion = pythonVersion,
                BuildDate = buildDate
            };
        }
        catch (RateLimitExceededException ex)
        {
            throw new PythonInstallationException(
                $"GitHub API rate limit exceeded. Please wait until {ex.Reset} or provide an authenticated GitHub client.",
                ex);
        }
        catch (ApiException ex)
        {
            throw new PythonInstallationException(
                $"GitHub API error: {ex.Message}",
                ex);
        }
    }

    /// <summary>
    /// Downloads a release asset to a specified path.
    /// </summary>
    public static async Task<string> DownloadAssetAsync(
        GitHubClient client,
        ReleaseAsset asset,
        string destinationPath,
        IProgress<long>? progress = null,
        CancellationToken cancellationToken = default)
    {
        if (asset == null)
            throw new ArgumentNullException(nameof(asset));
        if (string.IsNullOrWhiteSpace(destinationPath))
            throw new ArgumentException("Destination path cannot be null or empty.", nameof(destinationPath));

        try
        {
            var downloadUrl = asset.BrowserDownloadUrl;
            
            // Use HttpClient to download the file
            using var httpClient = new HttpClient();
            httpClient.Timeout = TimeSpan.FromMinutes(30); // Large files may take time

            using var response = await httpClient.GetAsync(downloadUrl, HttpCompletionOption.ResponseHeadersRead, cancellationToken).ConfigureAwait(false);
            response.EnsureSuccessStatusCode();

            var totalBytes = response.Content.Headers.ContentLength ?? -1L;
            var fileName = Path.GetFileName(asset.Name);
            var filePath = Path.Combine(destinationPath, fileName);

            Directory.CreateDirectory(destinationPath);

            await using var fileStream = new FileStream(filePath, System.IO.FileMode.Create, FileAccess.Write, FileShare.None, 8192, true);
            await using var contentStream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);

            var buffer = new byte[8192];
            long totalBytesRead = 0;
            int bytesRead;

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
    }

    private static bool IsMatchingRelease(Release release, string pythonVersion, string? buildDate)
    {
        // Releases are typically tagged with build dates (e.g., YYYYMMDD format)
        // or contain Python version in the tag/name
        var tag = release.TagName.ToLowerInvariant();
        var name = release.Name?.ToLowerInvariant() ?? string.Empty;
        
        // Check if Python version is in the tag or name
        var versionInRelease = tag.Contains(pythonVersion.Replace(".", "")) || 
                               name.Contains(pythonVersion);

        if (!versionInRelease)
            return false;

        // If build date is specified, try to match it
        if (!string.IsNullOrWhiteSpace(buildDate))
        {
            // Build date format can be YYYY-MM-DD or YYYYMMDD
            var normalizedBuildDate = buildDate.Replace("-", "");
            return tag.Contains(normalizedBuildDate) || name.Contains(normalizedBuildDate);
        }

        return true;
    }

    private static bool IsMatchingAsset(ReleaseAsset asset, string pythonVersion, string targetTriple)
    {
        var name = asset.Name.ToLowerInvariant();
        
        // Check if it contains the Python version (e.g., cpython-3.12.0)
        var versionInName = name.Contains($"cpython-{pythonVersion}") || 
                           name.Contains($"python-{pythonVersion}");

        if (!versionInName)
            return false;

        // Check if it contains the target triple
        return name.Contains(targetTriple.ToLowerInvariant());
    }

    private static bool IsInstallOnlyArchive(string fileName)
    {
        // Install-only archives typically have "install" in the name
        return fileName.ToLowerInvariant().Contains("install") && 
               !fileName.ToLowerInvariant().Contains("full");
    }

    private static bool IsFullArchive(string fileName)
    {
        // Full archives typically don't have "install" in the name, or explicitly say "full"
        return fileName.ToLowerInvariant().Contains("full") ||
               (!fileName.ToLowerInvariant().Contains("install") && 
                (fileName.EndsWith(".tar.zst", StringComparison.OrdinalIgnoreCase) ||
                 fileName.EndsWith(".zip", StringComparison.OrdinalIgnoreCase)));
    }

    /// <summary>
    /// Lists all available Python versions from GitHub releases.
    /// </summary>
    /// <param name="client">The GitHub client.</param>
    /// <param name="releaseTag">Optional release tag to query. If null, queries the latest release.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A list of available Python versions with their build dates and platforms.</returns>
    public static async Task<IReadOnlyList<string>> ListAvailableVersionsAsync(
        GitHubClient client,
        string? releaseTag = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            Release release;
            
            if (!string.IsNullOrWhiteSpace(releaseTag))
            {
                // Get specific release
                release = await client.Repository.Release.Get(RepositoryOwner, RepositoryName, releaseTag).ConfigureAwait(false);
            }
            else
            {
                // Get latest releases
                release = await client.Repository.Release.GetLatest(RepositoryOwner, RepositoryName).ConfigureAwait(false);
            }

            var versions = ExtractVersionsFromRelease(release);

            return versions
                .OrderDescending()
                .ToList().AsReadOnly();
        }
        catch (NotFoundException)
        {
            throw new InstanceNotFoundException(
                $"Release not found: {releaseTag ?? "latest"}");
        }
        catch (RateLimitExceededException ex)
        {
            throw new PythonInstallationException(
                $"GitHub API rate limit exceeded. Please wait until {ex.Reset} or provide an authenticated GitHub client.",
                ex);
        }
        catch (ApiException ex)
        {
            throw new PythonInstallationException(
                $"GitHub API error: {ex.Message}",
                ex);
        }
    }

    private static List<string> ExtractVersionsFromRelease(Release release)
    {
        // Try to extract Python version and build date from release tag/name
        // Format examples: "20240115" (build date), "cpython-3.12.0+20240115", etc.
        var tag = release.TagName;
        var name = release.Name ?? string.Empty;

        // Try to extract version from assets
        var pythonVersions = new HashSet<string>();

        foreach (var asset in release.Assets)
        {
            var assetName = asset.Name.ToLowerInvariant();
            
            // Extract Python version from asset name (e.g., "cpython-3.12.0-...")
            var versionMatch = System.Text.RegularExpressions.Regex.Match(
                assetName, 
                @"(?:cpython|python)-(\d+\.\d+\.\d+)");
            
            if (versionMatch.Success)
            {
                pythonVersions.Add(versionMatch.Groups[1].Value);
            }
        }

        if (!pythonVersions.Any())
            return new();

        return pythonVersions.ToList();
    }
}
