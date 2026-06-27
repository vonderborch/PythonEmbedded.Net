using System.Text.Json;
using Octokit;
using PythonEmbedded.Net.PythonProviders.GitReleases;

namespace PythonEmbedded.Net.PythonProviders.PythonBuildStandalone;

/// <summary>
/// Provides functionality to interact with the "python-build-standalone" GitHub repository
/// for obtaining Python builds. This class extends the <c>GitReleasesProvider</c>.
/// </summary>
/// <remarks>
/// This provider is designed to work with the repository hosted at:
/// "https://github.com/astral-sh/python-build-standalone". It leverages the configurations
/// defined in the <c>PythonBuildStandaloneConstants</c> class for repository metadata,
/// such as owner, repository name, API URLs, and JSON serialization options.
/// The class provides an abstracted way to access Python builds via GitHub's API, with
/// optional customization for GitHub client settings (headers, timeouts, etc.).
/// </remarks>
public class PythonBuildStandaloneProvider : GitReleasesProvider
{
    /// <summary>
    /// Provides functionality to interact with the "python-build-standalone" GitHub repository
    /// for accessing Python builds. This class extends <c>GitReleasesProvider</c>.
    /// </summary>
    /// <param name="githubClient">An optional <see cref="GitHubClient"/> instance for making HTTP requests. If not provided, a default client is created.</param>
    /// <param name="httpClientHeaders">An optional dictionary of headers to be included in HTTP requests. If not provided, default headers are used.</param>
    /// <param name="httpClientTimeout">An optional <see cref="TimeSpan"/> representing the timeout duration for HTTP client operations. If not provided, a default timeout of 30 seconds is used.</param>
    public PythonBuildStandaloneProvider(GitHubClient? githubClient = null,
        Dictionary<string, string>? httpClientHeaders = null, TimeSpan? httpClientTimeout = null) : base(
        PythonBuildStandaloneConstants.RepositoryOwner, PythonBuildStandaloneConstants.RepositoryName, PythonBuildStandaloneConstants.GitHubApiBaseUrl, PythonBuildStandaloneConstants.RepositoryApiUrl, PythonBuildStandaloneConstants.JsonOptions, githubClient, httpClientHeaders, httpClientTimeout)
    {
    }
}
