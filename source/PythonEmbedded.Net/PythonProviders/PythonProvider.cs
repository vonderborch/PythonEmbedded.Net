using PythonEmbedded.Net.Exceptions;
using PythonEmbedded.Net.Models;

namespace PythonEmbedded.Net.PythonProviders;

/// <summary>
/// Represents an abstract provider for managing Python runtime releases and versioning.
/// </summary>
public abstract class PythonProvider
{
    /// <summary>
    /// Retrieves a Python release that matches the specified version, platform, and optional build date range.
    /// </summary>
    /// <param name="pythonVersion">The version of Python to retrieve the release for.</param>
    /// <param name="platform">The platform information specifying the operating system and architecture.</param>
    /// <param name="minBuildDate">The earliest build date to include in the search. This is optional.</param>
    /// <param name="maxBuildDate">The latest build date to include in the search. This is optional.</param>
    /// <param name="cancellationToken">A cancellation token to observe while waiting for the task to complete.</param>
    /// <returns>A task that represents the asynchronous operation. The task result contains the matching Python release object if found.</returns>
    /// <exception cref="InstanceNotFoundException">
    /// Thrown when no release is found for the specified criteria.
    /// </exception>
    public async Task<PythonRelease> GetReleaseAsync(Version pythonVersion, PlatformInfo platform,
        DateTime? minBuildDate = null, DateTime? maxBuildDate = null, CancellationToken cancellationToken = default)
    {
        string buildDateString = maxBuildDate?.ToString("yyyy-MM-dd") ?? Constants.DefaultBuildDateString;

        var matchingBuilds = await GetAvailablePythonBuildsAndVersionsAsync(
            platform, includeReleaseObjects: true, maxResults: 1, minBuildDate: minBuildDate, maxBuildDate: maxBuildDate, requiredVersions: [pythonVersion], cancellationToken: cancellationToken);

        if (matchingBuilds.Count == 0)
        {
            throw new InstanceNotFoundException(
                $"No matching release found for Python {pythonVersion} and build date `{buildDateString}`")
            {
                PythonVersion = pythonVersion.ToString(),
                BuildDate = maxBuildDate
            };
        }

        PythonRelease release = matchingBuilds[0].releaseAssets![0];
        return release;
    }

    /// <summary>
    /// Retrieves the build date and a list of available Python versions that match the specified criteria.
    /// </summary>
    /// <param name="platform">The platform information specifying the operating system and architecture.</param>
    /// <param name="minBuildDate">The earliest build date to include in the results. This is optional.</param>
    /// <param name="maxBuildDate">The latest build date to include in the results. This is optional.</param>
    /// <param name="requiredVersions">A list of specific Python versions to filter the results. This is optional.</param>
    /// <param name="cancellationToken">A cancellation token to observe while waiting for the task to complete.</param>
    /// <returns>A task that represents the asynchronous operation. The task result contains a tuple with the following:
    /// the build date of the matched Python versions and a list of version strings. If no matches are found, the build date will be null, and the list will be empty.</returns>
    public async Task<(DateTime? buildDate, List<string>)> GetAvailableVersionsAsync(PlatformInfo platform,
        DateTime? minBuildDate = null, DateTime? maxBuildDate = null, List<Version>? requiredVersions = null,
        CancellationToken cancellationToken = default)
    {
        var matchingBuilds = await GetAvailablePythonBuildsAndVersionsAsync(platform,
            maxResults: 1, maxBuildDate: maxBuildDate, cancellationToken: cancellationToken);
        
        if (matchingBuilds.Count == 0)
        {
            return (null, new List<string>());
        }
        
        return (matchingBuilds[0].buildDate, matchingBuilds[0].pythonVersions);
    }

    /// <summary>
    /// Retrieves a list of available Python builds and versions based on the specified criteria.
    /// </summary>
    /// <param name="platform">The platform information specifying the operating system and architecture for the search.</param>
    /// <param name="includeReleaseObjects">
    /// A boolean value indicating whether to include the release objects for each build in the results. Defaults to false.
    /// </param>
    /// <param name="maxResults">The maximum number of results to retrieve. Defaults to 10.</param>
    /// <param name="minBuildDate">The earliest build date to include in the search. This is optional.</param>
    /// <param name="maxBuildDate">The latest build date to include in the search. This is optional.</param>
    /// <param name="requiredVersions">A list of specific Python versions to filter the search results. This is optional.</param>
    /// <param name="cancellationToken">A cancellation token to observe while waiting for the task to complete.</param>
    /// <returns>
    /// A task that represents the asynchronous operation. The task result contains a list of tuples, where each tuple includes:
    /// - The build date as a `DateTime` object.
    /// - A list of Python version strings available for the corresponding build.
    /// - An optional list of `PythonRelease` objects associated with the build, if `includeReleaseObjects` is set to true.
    /// </returns>
    public abstract Task<List<(DateTime buildDate, List<string> pythonVersions, List<PythonRelease>? releaseAssets)>>
        GetAvailablePythonBuildsAndVersionsAsync(
            PlatformInfo platform, bool includeReleaseObjects = false, int maxResults = 10,
            DateTime? minBuildDate = null,
            DateTime? maxBuildDate = null, List<Version>? requiredVersions = null,
            CancellationToken cancellationToken = default);
}
