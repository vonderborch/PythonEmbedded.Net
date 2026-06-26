using System.Text.Json.Serialization;

namespace PythonEmbedded.Net.Models;

/// <summary>
/// Data transfer object for GitHub release information.
/// Used for HTTP-based operations that are more efficient than Octokit (e.g., date-based filtering).
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
