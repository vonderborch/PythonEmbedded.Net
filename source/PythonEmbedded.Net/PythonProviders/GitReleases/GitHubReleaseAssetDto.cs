namespace PythonEmbedded.Net.PythonProviders.GitReleases;

/// <summary>
/// Data transfer object for GitHub release asset information.
/// </summary>
internal record GitHubReleaseAssetDto
{
    public long Id { get; set; }
    
    public string Name { get; set; } = string.Empty;
    
    public string BrowserDownloadUrl { get; set; } = string.Empty;
    
    public DateTimeOffset? UpdatedAt { get; set; }
}
