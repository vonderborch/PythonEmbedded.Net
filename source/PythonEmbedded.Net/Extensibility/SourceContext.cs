using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using PythonEmbedded.Net.Exceptions;
using PythonEmbedded.Net.Models;

namespace PythonEmbedded.Net.Extensibility;

/// <summary>
/// Shared plumbing handed to <see cref="IPythonSource"/> implementations: HTTP access,
/// download/metadata caching, and platform information.
/// </summary>
public sealed class SourceContext
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    internal SourceContext(
        HttpClient http, string? gitHubToken, string cacheDirectory,
        PlatformTriple platform, ILogger logger, bool offline, TimeSpan? releaseCacheTtl = null)
    {
        Http = http;
        GitHubToken = gitHubToken;
        CacheDirectory = cacheDirectory;
        Platform = platform;
        Logger = logger;
        Offline = offline;
        ReleaseCacheTtl = releaseCacheTtl ?? TimeSpan.FromHours(24);
    }

    /// <summary>The HTTP client to use for all network access.</summary>
    public HttpClient Http { get; }

    /// <summary>Token for GitHub API requests, if configured.</summary>
    public string? GitHubToken { get; }

    /// <summary>Root of the cache directory (downloads, cached metadata).</summary>
    public string CacheDirectory { get; }

    /// <summary>The platform triple of the current machine.</summary>
    public PlatformTriple Platform { get; }

    /// <summary>Logger for diagnostics.</summary>
    public ILogger Logger { get; }

    /// <summary>When true, sources must not touch the network.</summary>
    public bool Offline { get; }

    /// <summary>How long cached release metadata stays fresh (from <see cref="PythonOptions.ReleaseCacheTtl"/>).</summary>
    public TimeSpan ReleaseCacheTtl { get; }

    /// <summary>
    /// Downloads <paramref name="uri"/> into the download cache (skipped when already cached and the
    /// checksum matches) and returns the local file path. <paramref name="sha256"/> is verified when provided.
    /// </summary>
    public async Task<string> DownloadAsync(
        Uri uri, string? sha256 = null, CancellationToken ct = default, IProgress<InstallProgress>? progress = null)
    {
        string downloads = Path.Combine(CacheDirectory, "downloads");
        Directory.CreateDirectory(downloads);
        string fileName = Path.GetFileName(uri.LocalPath);
        string target = Path.Combine(downloads, fileName);

        if (File.Exists(target))
        {
            if (sha256 is null || await ChecksumMatchesAsync(target, sha256, ct).ConfigureAwait(false))
            {
                Logger.LogDebug("Using cached download {File}", target);
                return target;
            }

            File.Delete(target);
        }

        if (Offline)
        {
            throw new PythonException(
                PythonErrorKind.Offline,
                $"'{fileName}' is not in the download cache and Offline mode is enabled.");
        }

        Logger.LogInformation("Downloading {Uri}", uri);
        string temp = target + "." + Guid.NewGuid().ToString("N") + ".partial";
        try
        {
            using (HttpRequestMessage request = new(HttpMethod.Get, uri))
            using (HttpResponseMessage response = await Http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, ct).ConfigureAwait(false))
            {
                if (!response.IsSuccessStatusCode)
                {
                    throw new PythonException(
                        PythonErrorKind.DownloadFailed,
                        $"Download of {uri} failed with HTTP {(int)response.StatusCode} {response.ReasonPhrase}.");
                }

                await using FileStream file = File.Create(temp);
                if (progress is null)
                {
                    await response.Content.CopyToAsync(file, ct).ConfigureAwait(false);
                }
                else
                {
                    long? total = response.Content.Headers.ContentLength;
                    await using Stream source = await response.Content.ReadAsStreamAsync(ct).ConfigureAwait(false);
                    byte[] buffer = new byte[81920];
                    long completed = 0;
                    int read;
                    while ((read = await source.ReadAsync(buffer, ct).ConfigureAwait(false)) > 0)
                    {
                        await file.WriteAsync(buffer.AsMemory(0, read), ct).ConfigureAwait(false);
                        completed += read;
                        progress.Report(new InstallProgress(InstallPhase.Downloading, completed, total));
                    }
                }
            }

            if (sha256 is not null && !await ChecksumMatchesAsync(temp, sha256, ct).ConfigureAwait(false))
            {
                throw new PythonException(
                    PythonErrorKind.DownloadFailed,
                    $"Checksum mismatch for {uri}: expected sha256 {sha256}.");
            }

            File.Move(temp, target, overwrite: true);
            return target;
        }
        catch (HttpRequestException ex)
        {
            throw new PythonException(PythonErrorKind.DownloadFailed, $"Download of {uri} failed: {ex.Message}", ex);
        }
        finally
        {
            if (File.Exists(temp))
            {
                File.Delete(temp);
            }
        }
    }

    /// <summary>
    /// Extracts a <c>.tar.gz</c> or <c>.zip</c> archive into <paramref name="targetDirectory"/>, preserving
    /// symlinks and Unix permissions. Sources that download archives should use this rather than reimplementing
    /// extraction; it works around tar bugs in .NET 8 that affect real python-build-standalone and CPython archives.
    /// </summary>
    /// <exception cref="PythonException"><see cref="PythonErrorKind.InstallFailed"/> when the archive is an unsupported format or cannot be extracted.</exception>
    public Task ExtractAsync(
        string archivePath, string targetDirectory, CancellationToken ct = default,
        IProgress<InstallProgress>? progress = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(archivePath);
        ArgumentException.ThrowIfNullOrWhiteSpace(targetDirectory);
        return Internals.ArchiveExtractor.ExtractAsync(archivePath, targetDirectory, ct, progress);
    }

    /// <summary>
    /// Fetches JSON from <paramref name="uri"/> with disk caching: within <paramref name="ttl"/> the cached copy
    /// is returned without network access; past it, an ETag-conditional request refreshes the cache.
    /// In offline mode any cached copy is used regardless of age.
    /// </summary>
    public async Task<T?> GetCachedJsonAsync<T>(string key, Uri uri, TimeSpan ttl, CancellationToken ct = default)
    {
        string body = await GetCachedTextAsync(key, uri, ttl, ct).ConfigureAwait(false);
        return JsonSerializer.Deserialize<T>(body, JsonOptions);
    }

    /// <summary>
    /// Fetches <paramref name="uri"/> as text with the same disk caching, ETag revalidation, and offline
    /// semantics as <see cref="GetCachedJsonAsync{T}"/>. For payloads that aren't JSON — checksum manifests,
    /// directory indexes, and the like.
    /// </summary>
    public async Task<string> GetCachedTextAsync(string key, Uri uri, TimeSpan ttl, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        string dir = Path.Combine(CacheDirectory, "http");
        Directory.CreateDirectory(dir);
        string cachePath = Path.Combine(dir, key + ".json");

        CacheEnvelope? cached = null;
        if (File.Exists(cachePath))
        {
            try
            {
                cached = JsonSerializer.Deserialize<CacheEnvelope>(await File.ReadAllTextAsync(cachePath, ct).ConfigureAwait(false), JsonOptions);
            }
            catch (JsonException)
            {
                // Corrupt cache entry; refetch below.
            }
        }

        if (cached is not null && (Offline || DateTimeOffset.UtcNow - cached.FetchedAt < ttl))
        {
            return cached.Body;
        }

        if (Offline)
        {
            throw new PythonException(
                PythonErrorKind.Offline,
                $"No cached data for '{key}' and Offline mode is enabled.");
        }

        using HttpRequestMessage request = new(HttpMethod.Get, uri);
        if (cached?.ETag is not null)
        {
            request.Headers.TryAddWithoutValidation("If-None-Match", cached.ETag);
        }

        if (GitHubToken is not null && uri.Host.EndsWith("github.com", StringComparison.OrdinalIgnoreCase))
        {
            request.Headers.TryAddWithoutValidation("Authorization", $"Bearer {GitHubToken}");
        }

        try
        {
            using HttpResponseMessage response = await Http.SendAsync(request, ct).ConfigureAwait(false);
            if (response.StatusCode == System.Net.HttpStatusCode.NotModified && cached is not null)
            {
                await WriteEnvelopeAsync(cachePath, cached with { FetchedAt = DateTimeOffset.UtcNow }, ct).ConfigureAwait(false);
                return cached.Body;
            }

            if (!response.IsSuccessStatusCode)
            {
                if (cached is not null)
                {
                    Logger.LogWarning("Fetch of {Uri} failed with HTTP {Status}; using stale cache", uri, (int)response.StatusCode);
                    return cached.Body;
                }

                throw new PythonException(
                    PythonErrorKind.DownloadFailed,
                    $"Fetch of {uri} failed with HTTP {(int)response.StatusCode} {response.ReasonPhrase}.");
            }

            string body = await response.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
            string? etag = response.Headers.ETag?.Tag;
            await WriteEnvelopeAsync(cachePath, new CacheEnvelope(etag, DateTimeOffset.UtcNow, body), ct).ConfigureAwait(false);
            return body;
        }
        catch (HttpRequestException ex)
        {
            if (cached is not null)
            {
                Logger.LogWarning(ex, "Fetch of {Uri} failed; using stale cache", uri);
                return cached.Body;
            }

            throw new PythonException(PythonErrorKind.DownloadFailed, $"Fetch of {uri} failed: {ex.Message}", ex);
        }
    }

    internal static async Task<bool> ChecksumMatchesAsync(string filePath, string sha256, CancellationToken ct)
    {
        await using FileStream stream = File.OpenRead(filePath);
        byte[] hash = await SHA256.HashDataAsync(stream, ct).ConfigureAwait(false);
        return Convert.ToHexString(hash).Equals(sha256, StringComparison.OrdinalIgnoreCase);
    }

    private static Task WriteEnvelopeAsync(string path, CacheEnvelope envelope, CancellationToken ct)
        => File.WriteAllTextAsync(path, JsonSerializer.Serialize(envelope, JsonOptions), ct);

    private sealed record CacheEnvelope(string? ETag, DateTimeOffset FetchedAt, string Body);
}
