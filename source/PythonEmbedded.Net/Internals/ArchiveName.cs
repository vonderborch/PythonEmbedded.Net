using System.Text.RegularExpressions;
using PythonEmbedded.Net.Models;

namespace PythonEmbedded.Net.Internals;

/// <summary>A parsed python-build-standalone <c>install_only</c> archive file name.</summary>
internal sealed partial record ArchiveName(string FileName, Models.PythonVersion Version, string Tag, string Triple)
{
    // e.g. cpython-3.13.14+20260623-aarch64-apple-darwin-install_only.tar.gz
    [GeneratedRegex(@"^cpython-(?<version>\d+\.\d+\.\d+[a-z0-9]*)\+(?<tag>\d+)-(?<triple>[a-z0-9_\-]+?)-install_only(_stripped)?\.(tar\.gz|tgz|zip)$")]
    private static partial Regex Pattern();

    public static ArchiveName? TryParse(string fileName)
    {
        Match match = Pattern().Match(fileName);
        if (!match.Success || !Models.PythonVersion.TryParse(match.Groups["version"].Value, out Models.PythonVersion version))
        {
            return null;
        }

        return new ArchiveName(fileName, version, match.Groups["tag"].Value, match.Groups["triple"].Value);
    }

    /// <summary>Picks the best archive for a request: highest version, then newest tag.</summary>
    public static ArchiveName? SelectBest(
        IEnumerable<string> fileNames, PythonVersionRequest request, PlatformTriple platform)
        => fileNames
            .Select(TryParse)
            .Where(archive => archive is not null
                              && request.Matches(archive.Version)
                              && archive.Triple == platform.Value)
            .OrderByDescending(archive => archive!.Version)
            .ThenByDescending(archive => archive!.Tag, StringComparer.Ordinal)
            .FirstOrDefault();
}
