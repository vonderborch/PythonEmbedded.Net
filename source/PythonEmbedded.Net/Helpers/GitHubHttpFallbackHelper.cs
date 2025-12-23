using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using PythonEmbedded.Net.Exceptions;
using PythonEmbedded.Net.Models;

namespace PythonEmbedded.Net.Helpers;

/// <summary>
/// HTTP-based fallback helper for GitHub API calls when Octokit is unavailable.
/// Uses direct HTTP calls to GitHub's REST API.
/// </summary>
internal static class GitHubHttpFallbackHelper
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
    /// Gets all releases from the repository using HTTP.
    /// </summary>
    public static async Task<List<GitHubReleaseDto>> GetAllReleasesAsync(
        HttpClient? httpClient = null,
        CancellationToken cancellationToken = default)
    {
        var client = httpClient ?? CreateHttpClient();
        var url = $"{GitHubApiBaseUrl}/repos/{RepositoryOwner}/{RepositoryName}/releases?per_page=100";
        var releases = new List<GitHubReleaseDto>();
        var page = 1;

        try
        {
            while (true)
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
                
                // GitHub API returns up to 100 items per page
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
            if (httpClient == null)
            {
                client.Dispose();
            }
        }

        return releases;
    }

    /// <summary>
    /// Gets the latest release from the repository using HTTP.
    /// </summary>
    public static async Task<GitHubReleaseDto> GetLatestReleaseAsync(
        HttpClient? httpClient = null,
        CancellationToken cancellationToken = default)
    {
        var client = httpClient ?? CreateHttpClient();
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
            if (httpClient == null)
            {
                client.Dispose();
            }
        }
    }

    /// <summary>
    /// Gets a specific release by tag using HTTP.
    /// </summary>
    public static async Task<GitHubReleaseDto> GetReleaseByTagAsync(
        string tag,
        HttpClient? httpClient = null,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(tag))
        {
            throw new ArgumentException("Tag cannot be null or empty.", nameof(tag));
        }

        var client = httpClient ?? CreateHttpClient();
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
            if (httpClient == null)
            {
                client.Dispose();
            }
        }
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

