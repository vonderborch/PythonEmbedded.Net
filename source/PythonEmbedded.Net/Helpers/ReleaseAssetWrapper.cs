using Octokit;
using PythonEmbedded.Net.Models;

namespace PythonEmbedded.Net.Helpers;

/// <summary>
/// Wrapper class for release assets that can work with both Octokit and HTTP fallback.
/// </summary>
internal class ReleaseAssetWrapper
{
    private readonly ReleaseAsset? _octokitAsset;
    private readonly GitHubReleaseAssetDto? _dtoAsset;

    public ReleaseAssetWrapper(ReleaseAsset octokitAsset)
    {
        _octokitAsset = octokitAsset ?? throw new ArgumentNullException(nameof(octokitAsset));
    }

    public ReleaseAssetWrapper(GitHubReleaseAssetDto dtoAsset)
    {
        _dtoAsset = dtoAsset ?? throw new ArgumentNullException(nameof(dtoAsset));
    }

    public long Id => _octokitAsset?.Id ?? _dtoAsset!.Id;
    public string Name => _octokitAsset?.Name ?? _dtoAsset!.Name;
    public string BrowserDownloadUrl => _octokitAsset?.BrowserDownloadUrl ?? _dtoAsset!.BrowserDownloadUrl;
    public DateTimeOffset? UpdatedAt => _octokitAsset?.UpdatedAt ?? _dtoAsset!.UpdatedAt;

    /// <summary>
    /// Converts to Octokit ReleaseAsset if available, otherwise returns null.
    /// </summary>
    public ReleaseAsset? ToOctokitAsset() => _octokitAsset;
}

