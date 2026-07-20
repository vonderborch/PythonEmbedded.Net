using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using PythonEmbedded.Net.Exceptions;
using PythonEmbedded.Net.Extensibility;
using PythonEmbedded.Net.Internals;
using PythonEmbedded.Net.Models;

namespace PythonEmbedded.Net.Sources;

/// <summary>
/// Downloads Python from astral-sh/python-build-standalone GitHub releases.
/// The latest release tag is resolved via the GitHub API (ETag-cached); the tag's
/// <c>SHA256SUMS</c> asset supplies the full file list and checksums, so the
/// (paginated, rate-limited) asset API is never needed and download URLs are predictable.
/// </summary>
internal sealed class AstralSource : PythonSourceBase
{
    private const string Repository = "astral-sh/python-build-standalone";

    public override string Name => "astral";

    public override async Task<PythonInstallInfo?> TryInstallAsync(
        PythonVersionRequest request, string targetDirectory, SourceContext context, CancellationToken ct)
    {
        string? tag;
        IReadOnlyDictionary<string, string> checksums;
        try
        {
            tag = await ResolveLatestTagAsync(context, ct).ConfigureAwait(false);
            if (tag is null)
            {
                return null;
            }

            checksums = await GetChecksumsAsync(tag, context, ct).ConfigureAwait(false);
        }
        catch (PythonException ex) when (ex.Kind == PythonErrorKind.Offline)
        {
            // Offline with nothing cached: let other sources (or a clear VersionNotFound) speak.
            context.Logger.LogDebug("astral source unavailable offline without cached metadata");
            return null;
        }

        Internals.ArchiveName? archive = Internals.ArchiveName.SelectBest(checksums.Keys, request, context.Platform);
        if (archive is null)
        {
            context.Logger.LogDebug(
                "astral release {Tag} has no install_only archive for '{Request}' on {Platform}",
                tag, request.Raw, context.Platform);
            return null;
        }

        Uri downloadUri = new($"https://github.com/{Repository}/releases/download/{tag}/{archive.FileName}");
        string archivePath = await context.DownloadAsync(downloadUri, checksums[archive.FileName], ct).ConfigureAwait(false);
        await ArchiveExtractor.ExtractAsync(archivePath, targetDirectory, ct).ConfigureAwait(false);

        return new PythonInstallInfo(
            archive.Version, Name, archive.Triple, DateTimeOffset.UtcNow, checksums[archive.FileName]);
    }

    private static async Task<string?> ResolveLatestTagAsync(SourceContext context, CancellationToken ct)
    {
        ReleaseDto? release = await context.GetCachedJsonAsync<ReleaseDto>(
            "astral-latest-release",
            new Uri($"https://api.github.com/repos/{Repository}/releases/latest"),
            context.ReleaseCacheTtl,
            ct).ConfigureAwait(false);
        return release?.TagName;
    }

    /// <summary>Fetches and parses the tag's SHA256SUMS (immutable, so cached forever): file name → sha256.</summary>
    private static async Task<IReadOnlyDictionary<string, string>> GetChecksumsAsync(
        string tag, SourceContext context, CancellationToken ct)
    {
        string cacheDir = Path.Combine(context.CacheDirectory, "astral");
        Directory.CreateDirectory(cacheDir);
        string cachePath = Path.Combine(cacheDir, $"SHA256SUMS-{tag}");

        if (!File.Exists(cachePath))
        {
            if (context.Offline)
            {
                throw new PythonException(
                    PythonErrorKind.Offline, $"SHA256SUMS for tag {tag} is not cached and Offline mode is enabled.");
            }

            Uri uri = new($"https://github.com/{Repository}/releases/download/{tag}/SHA256SUMS");
            using HttpResponseMessage response = await context.Http.GetAsync(uri, ct).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
            {
                throw new PythonException(
                    PythonErrorKind.DownloadFailed,
                    $"Fetch of {uri} failed with HTTP {(int)response.StatusCode} {response.ReasonPhrase}.");
            }

            string temp = cachePath + ".partial";
            await File.WriteAllTextAsync(temp, await response.Content.ReadAsStringAsync(ct).ConfigureAwait(false), ct).ConfigureAwait(false);
            File.Move(temp, cachePath, overwrite: true);
        }

        Dictionary<string, string> checksums = new(StringComparer.Ordinal);
        foreach (string line in await File.ReadAllLinesAsync(cachePath, ct).ConfigureAwait(false))
        {
            // Format: "<sha256hex>  <filename>"
            int split = line.IndexOf(' ');
            if (split == 64)
            {
                checksums[line[split..].Trim()] = line[..split];
            }
        }

        return checksums;
    }

    private sealed record ReleaseDto([property: JsonPropertyName("tag_name")] string TagName);
}
