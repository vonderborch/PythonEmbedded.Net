using System.Text.Json.Serialization;

namespace PythonEmbedded.Net.Models;

/// <summary>
/// Data transfer object for GitHub release information.
/// Used as a fallback when Octokit is unavailable.
/// </summary>
internal class GitHubReleaseDto
{
    [JsonPropertyName("tag_name")]
    public string TagName { get; set; } = string.Empty;
    
    [JsonPropertyName("name")]
    public string? Name { get; set; }
    
    [JsonPropertyName("published_at")]
    public DateTimeOffset? PublishedAt { get; set; }
    
    [JsonPropertyName("assets")]
    public List<GitHubReleaseAssetDto> Assets { get; set; } = new();
}

/// <summary>
/// Data transfer object for GitHub release asset information.
/// Used as a fallback when Octokit is unavailable.
/// </summary>
internal class GitHubReleaseAssetDto
{
    [JsonPropertyName("id")]
    public long Id { get; set; }
    
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;
    
    [JsonPropertyName("browser_download_url")]
    public string BrowserDownloadUrl { get; set; } = string.Empty;
    
    [JsonPropertyName("updated_at")]
    public DateTimeOffset? UpdatedAt { get; set; }
}

