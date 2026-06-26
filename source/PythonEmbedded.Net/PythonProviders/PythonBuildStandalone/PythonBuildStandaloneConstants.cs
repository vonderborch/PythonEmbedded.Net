using System.Text.Json;
using System.Text.Json.Serialization;

namespace PythonEmbedded.Net.PythonProviders.PythonBuildStandalone;

public static class PythonBuildStandaloneConstants
{
    public const string RepositoryOwner = "astral-sh";
    public const string RepositoryName = "python-build-standalone";
    public const string GitHubApiBaseUrl = "https://api.github.com";
    public const string RepositoryApiUrl = $"{GitHubApiBaseUrl}/repos/{RepositoryOwner}/{RepositoryName}";
    
    public static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };
}
