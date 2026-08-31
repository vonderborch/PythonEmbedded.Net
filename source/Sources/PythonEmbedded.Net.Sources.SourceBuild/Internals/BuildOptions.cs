using PythonEmbedded.Net.Models;

namespace PythonEmbedded.Net.Sources.SourceBuild.Internals;

/// <summary>
/// The snapshot of <see cref="SourceBuildSource"/> settings a single build runs against. Builders take
/// this rather than the source itself so their argument composition can be unit-tested without a
/// <see cref="PythonEmbedded.Net.Extensibility.SourceContext"/> or a real toolchain.
/// </summary>
internal sealed record BuildOptions(
    string Name,
    bool Optimize,
    bool Lto,
    bool Shared,
    bool FreeThreaded,
    int JobCount,
    bool ProvisionDependencies,
    bool AllowElevation,
    IReadOnlyList<string> ConfigureArguments,
    IReadOnlyDictionary<string, string> BuildEnvironment,
    TimeSpan BuildTimeout,
    bool KeepBuildDirectory)
{
    /// <summary>
    /// A short stable hash of everything that changes the produced binaries, used to keep build
    /// directories for different option sets from colliding in the cache.
    /// </summary>
    public string Fingerprint
    {
        get
        {
            string material = string.Join(
                '\n',
                [
                    Optimize ? "opt" : "noopt",
                    Lto ? "lto" : "nolto",
                    Shared ? "shared" : "static",
                    FreeThreaded ? "ft" : "gil",
                    .. ConfigureArguments,
                    .. BuildEnvironment.OrderBy(pair => pair.Key, StringComparer.Ordinal)
                        .Select(pair => $"{pair.Key}={pair.Value}"),
                ]);

            byte[] hash = System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(material));
            return Convert.ToHexString(hash)[..8].ToLowerInvariant();
        }
    }
}

/// <summary>Everything a builder needs beyond <see cref="BuildOptions"/>: what to build and where.</summary>
/// <param name="Version">The exact version being compiled.</param>
/// <param name="SourceDirectory">The unpacked <c>Python-X.Y.Z</c> tree.</param>
/// <param name="BuildDirectory">The out-of-tree build directory.</param>
/// <param name="StagingDirectory">The directory <see cref="PythonEmbedded.Net.Extensibility.IPythonSource"/> was handed; becomes the install root.</param>
internal sealed record BuildPaths(
    PythonVersion Version,
    string SourceDirectory,
    string BuildDirectory,
    string StagingDirectory);
