using System.Text.Json.Serialization;

namespace PythonEmbedded.Net.Models;

/// <summary>
/// Data transfer object for GitHub release asset information.
/// Used for HTTP-based operations that are more efficient than Octokit.
/// </summary>
public class GitHubReleaseAssetDto
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