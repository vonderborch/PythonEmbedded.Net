using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;
using PythonEmbedded.Net.Exceptions;
using PythonEmbedded.Net.Extensibility;
using PythonEmbedded.Net.Models;

namespace PythonEmbedded.Net.Sources.SourceBuild.Internals;

/// <summary>
/// Resolves a <see cref="PythonVersionRequest"/> against python.org's source archive.
/// <para>
/// Two listings are needed, not one. <c>https://www.python.org/ftp/python/</c> enumerates release
/// directories, but those are always named for the <em>final</em> version — a pre-release lives inside
/// the directory of the version it leads to (<c>/ftp/python/3.15.0/</c> holds <c>Python-3.15.0b4.tgz</c>
/// and, until release day, no <c>Python-3.15.0.tgz</c> at all). So a matching directory is not proof
/// that a matching tarball exists, and each candidate has to be confirmed against its own listing.
/// </para>
/// </summary>
internal static partial class PythonOrgCatalog
{
    internal const string BaseUrl = "https://www.python.org/ftp/python/";

    /// <summary>How many candidate directories to open before giving up, newest first.</summary>
    private const int MaxCandidates = 5;

    [GeneratedRegex("href\\s*=\\s*\"(?<name>\\d+\\.\\d+\\.\\d+)/\"", RegexOptions.IgnoreCase)]
    private static partial Regex ReleaseDirectory();

    [GeneratedRegex("href\\s*=\\s*\"(?<file>Python-(?<version>\\d+\\.\\d+\\.\\d+[^\"]*?)\\.tgz)\"", RegexOptions.IgnoreCase)]
    private static partial Regex SourceTarball();

    /// <summary>The resolved release: its exact version and the URL of its <c>.tgz</c> source tarball.</summary>
    public sealed record Release(PythonVersion Version, Uri TarballUri);

    /// <summary>
    /// Returns the newest release satisfying <paramref name="request"/>, or null when python.org has
    /// nothing that matches. Pre-releases are only ever returned when the request names one explicitly
    /// (<c>"3.15.0b4"</c>), never for open-ended requests like <c>"latest"</c> or <c>"3.15"</c>.
    /// </summary>
    public static async Task<Release?> ResolveAsync(
        PythonVersionRequest request, SourceContext context, TimeSpan ttl, CancellationToken ct)
    {
        // A fully-pinned version names its own directory, pre-release or not: 3.15.0b4 lives under 3.15.0/.
        if (request is { Major: not null, Minor: not null, Patch: not null })
        {
            PythonVersion exact = new(request.Major.Value, request.Minor.Value, request.Patch.Value, request.Suffix);
            string directory = $"{exact.Major}.{exact.Minor}.{exact.Patch}";
            return await FindInDirectoryAsync(directory, v => v == exact, context, ttl, ct).ConfigureAwait(false);
        }

        string index = await context.GetCachedTextAsync("python-org-index", new Uri(BaseUrl), ttl, ct).ConfigureAwait(false);

        List<PythonVersion> candidates = ParseReleaseDirectories(index)
            .Where(request.Matches)
            .OrderByDescending(v => v)
            .Take(MaxCandidates)
            .ToList();

        foreach (PythonVersion candidate in candidates)
        {
            ct.ThrowIfCancellationRequested();

            // The directory exists; the final tarball may not (an unreleased X.Y.0 holding only betas).
            // Only a final release satisfies an open-ended request, so move on to the next candidate.
            Release? release = await FindInDirectoryAsync(
                candidate.ToString(), v => v == candidate, context, ttl, ct).ConfigureAwait(false);
            if (release is not null)
            {
                return release;
            }

            context.Logger.LogDebug(
                "python.org has a {Directory}/ directory but no Python-{Directory}.tgz; trying the next candidate",
                candidate, candidate);
        }

        return null;
    }

    /// <summary>Parses the top-level index into the versions it has release directories for.</summary>
    internal static IEnumerable<PythonVersion> ParseReleaseDirectories(string index) =>
        ReleaseDirectory().Matches(index)
            .Select(match => PythonVersion.TryParse(match.Groups["name"].Value, out PythonVersion v) ? v : (PythonVersion?)null)
            .Where(v => v is not null)
            .Select(v => v!.Value)
            .Distinct();

    /// <summary>Parses a release directory listing into the source tarballs it offers.</summary>
    internal static IEnumerable<(PythonVersion Version, string FileName)> ParseSourceTarballs(string listing) =>
        SourceTarball().Matches(listing)
            .Select(match => PythonVersion.TryParse(match.Groups["version"].Value, out PythonVersion v)
                ? (Version: (PythonVersion?)v, FileName: match.Groups["file"].Value)
                : (Version: null, FileName: string.Empty))
            .Where(entry => entry.Version is not null)
            .Select(entry => (entry.Version!.Value, entry.FileName));

    private static async Task<Release?> FindInDirectoryAsync(
        string directory, Func<PythonVersion, bool> accept, SourceContext context, TimeSpan ttl, CancellationToken ct)
    {
        string listing;
        try
        {
            listing = await context.GetCachedTextAsync(
                $"python-org-{directory}", new Uri($"{BaseUrl}{directory}/"), ttl, ct).ConfigureAwait(false);
        }
        catch (PythonException ex) when (ex.Kind == PythonErrorKind.DownloadFailed)
        {
            // No such release directory.
            return null;
        }

        foreach ((PythonVersion version, string fileName) in ParseSourceTarballs(listing))
        {
            if (accept(version))
            {
                return new Release(version, new Uri($"{BaseUrl}{directory}/{fileName}"));
            }
        }

        return null;
    }
}
