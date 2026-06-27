namespace PythonEmbedded.Net.PythonProviders.GitReleases;

/// <summary>
/// Data transfer object for GitHub release information.
/// </summary>
internal record GitHubReleaseDto
{
    public string TagName { get; set; } = string.Empty;
    
    public string? Name { get; set; }
    
    public DateTimeOffset? PublishedAt { get; set; }
    
    public List<GitHubReleaseAssetDto> Assets { get; set; } = new();
}
