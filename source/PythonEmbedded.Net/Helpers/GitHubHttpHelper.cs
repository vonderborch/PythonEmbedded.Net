using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using PythonEmbedded.Net.Exceptions;
using PythonEmbedded.Net.Models;

namespace PythonEmbedded.Net.Helpers;

/// <summary>
/// HTTP-based helper for GitHub API calls that are more efficient via direct HTTP
/// (e.g., date-based filtering that would require fetching many releases with Octokit).
/// Uses direct HTTP calls to GitHub's REST API for operations that would timeout with Octokit.
/// </summary>
internal static class GitHubHttpHelper
{
    private const string RepositoryOwner = "astral-sh";
    private const string RepositoryName = "python-build-standalone";
    private const string GitHubApiBaseUrl = "https://api.github.com";
    
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    /// <summary>
    /// Gets releases from the repository using HTTP with pagination.
    /// More efficient than Octokit for operations requiring many releases.
    /// </summary>
    /// <param name="maxPages">Maximum number of pages to fetch (default: 10, ~1000 releases).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>List of releases, ordered by published date (newest first).</returns>
    public static async Task<List<GitHubReleaseDto>> GetReleasesAsync(
        int maxPages = 10,
        CancellationToken cancellationToken = default)
    {
        var client = CreateHttpClient();
        var url = $"{GitHubApiBaseUrl}/repos/{RepositoryOwner}/{RepositoryName}/releases?per_page=100";
        var releases = new List<GitHubReleaseDto>();
        var page = 1;

        try
        {
            while (page <= maxPages)
            {
                var pageUrl = $"{url}&page={page}";
                var response = await client.GetAsync(pageUrl, cancellationToken).ConfigureAwait(false);
                
                if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
                {
                    break;
                }
                
                response.EnsureSuccessStatusCode();
                
                var pageReleases = await response.Content.ReadFromJsonAsync<List<GitHubReleaseDto>>(
                    JsonOptions, cancellationToken).ConfigureAwait(false);
                
                if (pageReleases == null || !pageReleases.Any())
                {
                    break;
                }
                
                releases.AddRange(pageReleases);
                
                // If we got fewer than 100 results, we're on the last page
                if (pageReleases.Count < 100)
                {
                    break;
                }
                
                page++;
            }
        }
        catch (HttpRequestException ex)
        {
            throw new PythonInstallationException(
                $"Failed to fetch releases from GitHub API: {ex.Message}",
                ex);
        }
        finally
        {
            client.Dispose();
        }

        return releases;
    }

    /// <summary>
    /// Gets the latest release from the repository using HTTP.
    /// </summary>
    public static async Task<GitHubReleaseDto> GetLatestReleaseAsync(
        CancellationToken cancellationToken = default)
    {
        var client = CreateHttpClient();
        var url = $"{GitHubApiBaseUrl}/repos/{RepositoryOwner}/{RepositoryName}/releases/latest";

        try
        {
            var response = await client.GetAsync(url, cancellationToken).ConfigureAwait(false);
            
            if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
            {
                throw new InstanceNotFoundException("Latest release not found");
            }
            
            response.EnsureSuccessStatusCode();
            
            var release = await response.Content.ReadFromJsonAsync<GitHubReleaseDto>(
                JsonOptions, cancellationToken).ConfigureAwait(false);
            
            if (release == null)
            {
                throw new PythonInstallationException("Failed to deserialize release data");
            }

            return release;
        }
        catch (HttpRequestException ex)
        {
            throw new PythonInstallationException(
                $"Failed to fetch latest release from GitHub API: {ex.Message}",
                ex);
        }
        finally
        {
            client.Dispose();
        }
    }

    /// <summary>
    /// Gets a specific release by tag using HTTP.
    /// </summary>
    public static async Task<GitHubReleaseDto> GetReleaseByTagAsync(
        string tag,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(tag))
        {
            throw new ArgumentException("Tag cannot be null or empty.", nameof(tag));
        }

        var client = CreateHttpClient();
        var url = $"{GitHubApiBaseUrl}/repos/{RepositoryOwner}/{RepositoryName}/releases/tags/{Uri.EscapeDataString(tag)}";

        try
        {
            var response = await client.GetAsync(url, cancellationToken).ConfigureAwait(false);
            
            if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
            {
                throw new InstanceNotFoundException($"Release with tag '{tag}' not found");
            }
            
            response.EnsureSuccessStatusCode();
            
            var release = await response.Content.ReadFromJsonAsync<GitHubReleaseDto>(
                JsonOptions, cancellationToken).ConfigureAwait(false);
            
            if (release == null)
            {
                throw new PythonInstallationException("Failed to deserialize release data");
            }

            return release;
        }
        catch (HttpRequestException ex)
        {
            throw new PythonInstallationException(
                $"Failed to fetch release from GitHub API: {ex.Message}",
                ex);
        }
        finally
        {
            client.Dispose();
        }
    }

    /// <summary>
    /// Finds the first release on or after the specified date using HTTP.
    /// More efficient than Octokit for this operation as it can stop early.
    /// </summary>
    public static async Task<GitHubReleaseDto?> FindReleaseOnOrAfterDateAsync(
        DateTime targetDate,
        int maxPages = 10,
        CancellationToken cancellationToken = default)
    {
        var client = CreateHttpClient();
        var url = $"{GitHubApiBaseUrl}/repos/{RepositoryOwner}/{RepositoryName}/releases?per_page=100";
        var page = 1;
        GitHubReleaseDto? earliestMatchingRelease = null;
        DateTime? earliestDate = null;

        try
        {
            while (page <= maxPages)
            {
                var pageUrl = $"{url}&page={page}";
                var response = await client.GetAsync(pageUrl, cancellationToken).ConfigureAwait(false);
                
                if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
                {
                    break;
                }
                
                response.EnsureSuccessStatusCode();
                
                var releases = await response.Content.ReadFromJsonAsync<List<GitHubReleaseDto>>(
                    JsonOptions, cancellationToken).ConfigureAwait(false);
                
                if (releases == null || !releases.Any())
                {
                    break;
                }

                // Find releases on or after the target date
                foreach (var release in releases)
                {
                    var releaseDate = release.PublishedAt?.Date ?? DateTime.MinValue;
                    if (releaseDate >= targetDate.Date)
                    {
                        if (earliestMatchingRelease == null || releaseDate < earliestDate)
                        {
                            earliestMatchingRelease = release;
                            earliestDate = releaseDate;
                        }
                    }
                }

                // If we found a match and releases are getting too old, we can stop early
                // (releases are ordered newest first, so once we pass the target date, we won't find earlier matches)
                var oldestInPage = releases.Min(r => r.PublishedAt?.Date ?? DateTime.MaxValue);
                if (oldestInPage < targetDate.Date && earliestMatchingRelease != null)
                {
                    // We've gone past the target date and found a match, stop searching
                    break;
                }

                // If we got fewer than 100 results, we're on the last page
                if (releases.Count < 100)
                {
                    break;
                }
                
                page++;
            }
        }
        catch (HttpRequestException ex)
        {
            throw new PythonInstallationException(
                $"Failed to find release by date from GitHub API: {ex.Message}",
                ex);
        }
        finally
        {
            client.Dispose();
        }

        return earliestMatchingRelease;
    }

    private static HttpClient CreateHttpClient()
    {
        var client = new HttpClient();
        client.DefaultRequestHeaders.Add("User-Agent", "PythonEmbedded.Net/1.0");
        client.DefaultRequestHeaders.Add("Accept", "application/vnd.github.v3+json");
        client.Timeout = TimeSpan.FromMinutes(5);
        return client;
    }
}

