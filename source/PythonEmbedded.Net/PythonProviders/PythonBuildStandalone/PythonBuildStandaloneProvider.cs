using System.Net.Http.Json;
using Octokit;
using PythonEmbedded.Net.Exceptions;
using PythonEmbedded.Net.Models;

namespace PythonEmbedded.Net.PythonProviders.PythonBuildStandalone;

/// <summary>
/// Provides a standalone Python build provider for managing embedded Python installations.
/// </summary>
public class PythonBuildStandaloneProvider : PythonProvider
{
    /// <summary>
    /// Represents an instance of the GitHub API client used to interact with GitHub.
    /// </summary>
    private GitHubClient _githubClient;

    /// <summary>
    /// Stores default HTTP client headers used for making web requests to external services, such as GitHub API.
    /// </summary>
    private Dictionary<string, string> _httpClientHeaders;

    /// <summary>
    /// Specifies the timeout duration for HTTP client operations.
    /// </summary>
    private TimeSpan _httpClientTimeout;

    /// <summary>
    /// Provides functionality to manage and retrieve standalone Python builds specific to targeted platforms.
    /// This class offers methods to discover, validate, and prepare Python installations without relying on external system configurations.
    /// </summary>
    /// <param name="githubClient">An optional <see cref="GitHubClient"/> instance for making HTTP requests. If not provided, a default client is created.</param>
    /// <param name="httpClientHeaders">An optional dictionary of headers to be included in HTTP requests. If not provided, default headers are used.</param>
    /// <param name="httpClientTimeout">An optional <see cref="TimeSpan"/> representing the timeout duration for HTTP client operations. If not provided, a default timeout of 30 seconds is used.</param>
    public PythonBuildStandaloneProvider(GitHubClient? githubClient = null,
        Dictionary<string, string>? httpClientHeaders = null, TimeSpan? httpClientTimeout = null)
    {
        if (githubClient is null)
        {
            string productHeaderValue = $"PythonEmbedded.Net.{Environment.MachineName}";
            _githubClient = new GitHubClient(new ProductHeaderValue(productHeaderValue));
        }
        else
        {
            _githubClient = githubClient;
        }
        
        if (httpClientHeaders is null)
        {
            string userAgent = $"PythonEmbedded.Net.{Environment.MachineName}/2.0";
            _httpClientHeaders = new Dictionary<string, string>();
            _httpClientHeaders.Add("User-Agent", userAgent);
            _httpClientHeaders.Add("Accept", "application/vnd.github.v3+json");
        }
        else
        {
            _httpClientHeaders = httpClientHeaders;
        }
        
        if (httpClientTimeout is null)
        {
            _httpClientTimeout = TimeSpan.FromSeconds(30);
        }
        else
        {
            _httpClientTimeout = httpClientTimeout.Value;
        }
    }

    /// <summary>
    /// Retrieves a list of available Python builds and their corresponding versions for the specified platform.
    /// This method queries a remote GitHub API for release information, filtering results by optional criteria such as
    /// build date ranges, maximum number of results, and required Python versions.
    /// </summary>
    /// <param name="platform">The platform information, including the operating system, architecture, and target platform.</param>
    /// <param name="includeReleaseObjects">Specifies whether to include release assets in the response.</param>
    /// <param name="maxResults">The maximum number of results to return. Defaults to 10 if not specified.</param>
    /// <param name="minBuildDate">The earliest build date to consider. If null, no minimum date filter is applied.</param>
    /// <param name="maxBuildDate">The latest build date to consider. If null, no maximum date filter is applied.</param>
    /// <param name="requiredVersions">A list of specific Python versions to filter by. If null or empty, all versions are included.</param>
    /// <param name="cancellationToken">A token to signal request cancellation.</param>
    /// <returns>
    /// A task representing the asynchronous operation. The task result contains a list where each item includes the build date,
    /// a list of Python versions available for that build date, and an optional list of release assets.
    /// </returns>
    /// <exception cref="PythonInstallationException">
    /// Thrown when the query to the GitHub API fails or an error occurs while retrieving release information.
    /// </exception>
    public override async
        Task<List<(DateTime buildDate, List<string> pythonVersions, List<PythonRelease>? releaseAssets)>>
        GetAvailablePythonBuildsAndVersionsAsync(
            PlatformInfo platform, bool includeReleaseObjects = false, int maxResults = 10,
            DateTime? minBuildDate = null,
            DateTime? maxBuildDate = null, List<Version>? requiredVersions = null,
            CancellationToken cancellationToken = default)
    {
        var client = CreateHttpClient();
        var page = 1;
        List<(DateTime buildDate, List<string> pythonVersions, List<PythonRelease>? releaseAssets)> availableBuildDates = new();
        string platformTargetTriple = platform.TargetTriple?.ToLowerInvariant()!;
        
        minBuildDate ??= DateTime.MinValue;
        maxBuildDate ??= DateTime.MaxValue;
        requiredVersions ??= new List<Version>();
        
        try
        {
            while (true)
            {
                var pageUrl = $"{PythonBuildStandaloneConstants.RepositoryApiUrl}&page={page}";
                var response = await client.GetAsync(pageUrl, cancellationToken).ConfigureAwait(false);
                
                if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
                {
                    break;
                }
                
                response.EnsureSuccessStatusCode();
                
                var releases = await response.Content.ReadFromJsonAsync<List<GitHubReleaseDto>>(
                    PythonBuildStandaloneConstants.JsonOptions, cancellationToken).ConfigureAwait(false);
                
                if (releases == null || !releases.Any())
                {
                    break;
                }

                // Find releases on or before the target date
                // Implementation note: the results are returned in order, so the first item we find is the best match
                foreach (var release in releases)
                {
                    var releaseDate = release.PublishedAt?.Date ?? DateTime.MinValue;
                    if (releaseDate >= minBuildDate?.Date && releaseDate <= maxBuildDate?.Date)
                    {
                        List<string> matchingVersions = new();
                        List<PythonRelease>? matchingReleaseAssets = includeReleaseObjects ? new() : null;
                        foreach (var asset in release.Assets)
                        {
                            if (PythonProviderHelpers.IsMatchingPlatform(asset.Name, platformTargetTriple))
                            {
                                Version? pythonVersion = PythonProviderHelpers.GetVersionFromAssetName(asset.Name);
                                if (pythonVersion is not null)
                                {
                                    bool meetsMatchingVersionRequirements = requiredVersions.Count == 0;
                                    foreach (var requiredVersion in requiredVersions)
                                    {
                                        if (PythonProviderHelpers.IsMatchingVersion(pythonVersion, requiredVersion))
                                        {
                                            meetsMatchingVersionRequirements = true;
                                            break;
                                        }
                                    }
                                    
                                    if (meetsMatchingVersionRequirements)
                                    {
                                        matchingVersions.Add(pythonVersion.ToString());

                                        if (includeReleaseObjects)
                                        {
                                            PythonRelease releaseObject = await
                                                PythonBuildStandaloneRelease.Create(pythonVersion, platform, releaseDate,
                                                    _githubClient, release.TagName, cancellationToken);
                                            matchingReleaseAssets!.Add(releaseObject);
                                        }
                                    }
                                }
                            }
                        }
                        if (matchingVersions.Count > 0)
                        {
                            availableBuildDates.Add((releaseDate, matchingVersions, matchingReleaseAssets));
                            
                            // break if we have enough results
                            if (maxResults > 0 && availableBuildDates.Count >= maxResults)
                            {
                                break;
                            }
                        }
                    }
                }

                // If we got fewer than 100 results, we're on the last page and can stop
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
                $"Failed to find releases by date from GitHub API: {ex.Message}",
                ex);
        }
        finally
        {
            client.Dispose();
        }
        
        return availableBuildDates;
    }

    /// <summary>
    /// Creates and configures a new instance of the <see cref="HttpClient"/> class with predefined headers and timeout settings.
    /// </summary>
    /// <returns>
    /// An instance of <see cref="HttpClient"/> initialized with the configured headers and timeout.
    /// </returns>
    private HttpClient CreateHttpClient()
    {
        var client = new HttpClient();
        foreach (var headers in _httpClientHeaders)
        {
            client.DefaultRequestHeaders.Add(headers.Key, headers.Value);
        }
        client.Timeout = _httpClientTimeout;
        return client;
    }
}
