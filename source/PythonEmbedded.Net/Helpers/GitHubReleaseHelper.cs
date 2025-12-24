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
        DateTime? buildDate,
        PlatformInfo platform,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(pythonVersion))
            throw new ArgumentException("Python version cannot be null or empty.", nameof(pythonVersion));
        if (platform == null)
            throw new ArgumentNullException(nameof(platform));
        if (string.IsNullOrWhiteSpace(platform.TargetTriple))
            throw new ArgumentException("Platform target triple is required.", nameof(platform));

        return await FindReleaseAssetWithOctokitAsync(client, pythonVersion, buildDate, platform, cancellationToken).ConfigureAwait(false);
    }

    private static async Task<ReleaseAsset?> FindReleaseAssetWithOctokitAsync(
        GitHubClient client,
        string pythonVersion,
        DateTime? buildDate,
        PlatformInfo platform,
        CancellationToken cancellationToken)
    {
        // Check if this is a partial version (e.g., "3.10" without patch)
        // A partial version is one that doesn't have 3 parts (major.minor.patch)
        var versionParts = pythonVersion.Split('.');
        var isPartialVersion = versionParts.Length < 3;
        
        // Parse version to get major and minor
        var (major, minor, patch) = VersionParser.ParseVersion(pythonVersion);

        // Find the appropriate release
        // Use HTTP for date-based searches (more efficient, avoids fetching all releases with Octokit)
        // Use Octokit for latest release (more efficient)
        Release? release = null;
        
        if (buildDate.HasValue)
        {
            // Use HTTP for date-based filtering - more efficient than Octokit pagination
            var httpRelease = await GitHubHttpHelper.FindReleaseOnOrAfterDateAsync(
                buildDate.Value, 
                maxPages: 10, 
                cancellationToken).ConfigureAwait(false);
            
            if (httpRelease == null)
            {
                throw new InstanceNotFoundException(
                    $"No matching release found for Python {pythonVersion} and build date {buildDate.Value:yyyy-MM-dd}")
                {
                    PythonVersion = pythonVersion,
                    BuildDate = buildDate
                };
            }
            
            // Convert HTTP release to Octokit release by fetching it
            try
            {
                release = await client.Repository.Release.Get(RepositoryOwner, RepositoryName, httpRelease.TagName).ConfigureAwait(false);
            }
            catch (NotFoundException)
            {
                throw new InstanceNotFoundException(
                    $"Release found via HTTP but not accessible via Octokit: {httpRelease.TagName}")
                {
                    PythonVersion = pythonVersion,
                    BuildDate = buildDate
                };
            }
        }
        else
        {
            // Get latest release using Octokit (efficient)
            release = await client.Repository.Release.GetLatest(RepositoryOwner, RepositoryName).ConfigureAwait(false);
        }

        if (release is null)
        {
            throw new InstanceNotFoundException(
                $"No matching release found for Python {pythonVersion} and build date {buildDate?.ToString("yyyy-MM-dd") ?? "latest"}")
            {
                PythonVersion = pythonVersion,
                BuildDate = buildDate
            };
        }

        // Collect all matching assets from this release
        var preferredAssets = new List<ReleaseAsset>();
        var fallbackAssets = new List<ReleaseAsset>();

        foreach (var asset in release.Assets)
        {
            // If we have a partial version (e.g., "3.10"), match any patch version (e.g., "3.10.19")
            // Otherwise, match exact version
            if (IsMatchingAsset(asset, pythonVersion, platform.TargetTriple, isPartialVersion, major, minor))
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

        // If we have a partial version and no matches in this release, we may need to search other releases
        // But for now, we'll just use the latest release. If needed, we can enhance this later.
        if (preferredAssets.Any())
        {
            // For partial versions, prefer the latest patch version
            if (isPartialVersion)
            {
                return preferredAssets
                    .OrderByDescending(a => 
                    {
                        var versionStr = ExtractVersionFromAssetName(a.Name);
                        return VersionParser.ParseVersion(versionStr);
                    }, Comparer<(int Major, int Minor, int Patch)>.Create((v1, v2) => 
                    {
                        var majorCmp = v1.Major.CompareTo(v2.Major);
                        if (majorCmp != 0) return majorCmp;
                        var minorCmp = v1.Minor.CompareTo(v2.Minor);
                        if (minorCmp != 0) return minorCmp;
                        return v1.Patch.CompareTo(v2.Patch);
                    }))
                    .ThenByDescending(a => a.UpdatedAt)
                    .First();
            }
            return preferredAssets.OrderByDescending(a => a.UpdatedAt).First();
        }

        if (fallbackAssets.Any())
        {
            // For partial versions, prefer the latest patch version
            if (isPartialVersion)
            {
                return fallbackAssets
                    .OrderByDescending(a => 
                    {
                        var versionStr = ExtractVersionFromAssetName(a.Name);
                        return VersionParser.ParseVersion(versionStr);
                    }, Comparer<(int Major, int Minor, int Patch)>.Create((v1, v2) => 
                    {
                        var majorCmp = v1.Major.CompareTo(v2.Major);
                        if (majorCmp != 0) return majorCmp;
                        var minorCmp = v1.Minor.CompareTo(v2.Minor);
                        if (minorCmp != 0) return minorCmp;
                        return v1.Patch.CompareTo(v2.Patch);
                    }))
                    .ThenByDescending(a => a.UpdatedAt)
                    .First();
            }
            return fallbackAssets.OrderByDescending(a => a.UpdatedAt).First();
        }

        throw new InstanceNotFoundException(
            $"No matching asset found for Python {pythonVersion}, build date {buildDate?.ToString("yyyy-MM-dd") ?? "latest"}, and platform {platform.TargetTriple}")
        {
            PythonVersion = pythonVersion,
            BuildDate = buildDate
        };
    }


    /// <summary>
    /// Extracts version string from asset name for comparison.
    /// </summary>
    private static string ExtractVersionFromAssetName(string assetName)
    {
        var match = System.Text.RegularExpressions.Regex.Match(
            assetName.ToLowerInvariant(),
            @"(?:cpython|python)-(\d+\.\d+\.\d+)");
        
        return match.Success ? match.Groups[1].Value : "0.0.0";
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

    private static bool IsMatchingAsset(ReleaseAsset asset, string pythonVersion, string targetTriple, bool isPartialVersion, int major, int minor)
    {
        var name = asset.Name.ToLowerInvariant();
        
        // Extract version from asset name
        var versionMatch = System.Text.RegularExpressions.Regex.Match(
            name,
            @"(?:cpython|python)-(\d+)\.(\d+)\.(\d+)");
        
        if (!versionMatch.Success)
            return false;

        var assetMajor = int.Parse(versionMatch.Groups[1].Value);
        var assetMinor = int.Parse(versionMatch.Groups[2].Value);
        var assetPatch = int.Parse(versionMatch.Groups[3].Value);

        // For partial versions (e.g., "3.10"), match any patch version (e.g., "3.10.19")
        // For full versions, match exactly
        bool versionMatches;
        if (isPartialVersion)
        {
            versionMatches = assetMajor == major && assetMinor == minor;
        }
        else
        {
            var (requestedMajor, requestedMinor, requestedPatch) = VersionParser.ParseVersion(pythonVersion);
            versionMatches = assetMajor == requestedMajor && 
                            assetMinor == requestedMinor && 
                            assetPatch == requestedPatch;
        }

        if (!versionMatches)
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
        // Try Octokit first
        try
        {
            return await ListAvailableVersionsWithOctokitAsync(client, releaseTag, cancellationToken).ConfigureAwait(false);
        }
        catch (NotFoundException)
        {
            // NotFoundException is expected in some cases, rethrow it
            throw new InstanceNotFoundException(
                $"Release not found: {releaseTag ?? "latest"}");
        }
        catch (RateLimitExceededException ex)
        {
            // Rate limit exceeded
            throw new PythonInstallationException(
                $"GitHub API rate limit exceeded. Please wait until {ex.Reset} or provide an authenticated GitHub client.",
                ex);
        }
        catch (ApiException ex)
        {
            // API exceptions
            throw new PythonInstallationException(
                $"GitHub API error: {ex.Message}",
                ex);
        }
    }

    private static async Task<IReadOnlyList<string>> ListAvailableVersionsWithOctokitAsync(
        GitHubClient client,
        string? releaseTag,
        CancellationToken cancellationToken)
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
