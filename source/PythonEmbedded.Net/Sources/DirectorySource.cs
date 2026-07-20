using Microsoft.Extensions.Logging;
using PythonEmbedded.Net.Extensibility;
using PythonEmbedded.Net.Internals;
using PythonEmbedded.Net.Models;

namespace PythonEmbedded.Net.Sources;

/// <summary>
/// Installs Python from python-build-standalone <c>install_only</c> archives found in a local directory.
/// Backs both the bundled runtime-package convention (<c>python-embedded-runtimes/</c> next to the app)
/// and user-supplied archive directories (<see cref="PythonOptions.AddDirectorySource"/>).
/// </summary>
internal sealed class DirectorySource : PythonSourceBase
{
    private readonly string _directory;

    public DirectorySource(string name, string directory)
    {
        Name = name;
        _directory = directory;
    }

    public override string Name { get; }

    public override async Task<PythonInstallInfo?> TryInstallAsync(
        PythonVersionRequest request, string targetDirectory, SourceContext context, CancellationToken ct)
    {
        if (!Directory.Exists(_directory))
        {
            return null;
        }

        Internals.ArchiveName? best = Internals.ArchiveName.SelectBest(
            Directory.EnumerateFiles(_directory).Select(Path.GetFileName)!,
            request,
            context.Platform);
        if (best is null)
        {
            return null;
        }

        string archivePath = Path.Combine(_directory, best.FileName);
        context.Logger.LogInformation("Installing Python {Version} from archive {Archive}", best.Version, archivePath);
        await ArchiveExtractor.ExtractAsync(archivePath, targetDirectory, ct).ConfigureAwait(false);

        return new PythonInstallInfo(best.Version, Name, best.Triple, DateTimeOffset.UtcNow);
    }
}
