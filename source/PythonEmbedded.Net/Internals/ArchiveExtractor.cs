using System.Formats.Tar;
using System.IO.Compression;
using PythonEmbedded.Net.Exceptions;
using PythonEmbedded.Net.Models;

namespace PythonEmbedded.Net.Internals;

/// <summary>Extracts python-build-standalone archives (.tar.gz or .zip) preserving Unix permissions.</summary>
internal static class ArchiveExtractor
{
    public static async Task ExtractAsync(
        string archivePath, string targetDirectory, CancellationToken ct, IProgress<InstallProgress>? progress = null)
    {
        Directory.CreateDirectory(targetDirectory);
        progress?.Report(new InstallProgress(InstallPhase.Extracting, Detail: Path.GetFileName(archivePath)));
        try
        {
            if (archivePath.EndsWith(".zip", StringComparison.OrdinalIgnoreCase))
            {
                ZipFile.ExtractToDirectory(archivePath, targetDirectory, overwriteFiles: true);
            }
            else if (archivePath.EndsWith(".tar.gz", StringComparison.OrdinalIgnoreCase)
                     || archivePath.EndsWith(".tgz", StringComparison.OrdinalIgnoreCase))
            {
                await using FileStream file = File.OpenRead(archivePath);
                await using GZipStream gzip = new(file, CompressionMode.Decompress);
                await ExtractTarAsync(gzip, targetDirectory, ct).ConfigureAwait(false);
            }
            else
            {
                throw new PythonException(
                    PythonErrorKind.InstallFailed,
                    $"Unsupported archive format: '{Path.GetFileName(archivePath)}' (expected .tar.gz or .zip).");
            }
        }
        catch (Exception ex) when (ex is not PythonException and not OperationCanceledException)
        {
            throw new PythonException(
                PythonErrorKind.InstallFailed,
                $"Failed to extract '{Path.GetFileName(archivePath)}': {ex.Message}", ex);
        }
    }

    // Hand-rolled instead of TarFile.ExtractToDirectoryAsync: .NET 8's built-in hard-link extraction
    // has a path-construction bug (fixed upstream in .NET 9+) that corrupts the destination path for
    // archives containing hard-linked entries — which python-build-standalone's license files do.
    private static async Task ExtractTarAsync(Stream stream, string targetDirectory, CancellationToken ct)
    {
        string root = Path.GetFullPath(targetDirectory);
        await using TarReader reader = new(stream);
        while (await reader.GetNextEntryAsync(copyData: true, ct).ConfigureAwait(false) is { } entry)
        {
            string destination = ResolveDestination(root, entry.Name);
            switch (entry.EntryType)
            {
                case TarEntryType.Directory:
                    Directory.CreateDirectory(destination);
                    break;
                case TarEntryType.SymbolicLink:
                    Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
                    if (File.Exists(destination) || Directory.Exists(destination))
                    {
                        File.Delete(destination);
                    }

                    File.CreateSymbolicLink(destination, entry.LinkName);
                    break;
                case TarEntryType.HardLink:
                    Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
                    File.Copy(ResolveDestination(root, entry.LinkName), destination, overwrite: true);
                    break;
                default:
                    Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
                    await entry.ExtractToFileAsync(destination, overwrite: true, ct).ConfigureAwait(false);
                    break;
            }
        }
    }

    private static string ResolveDestination(string root, string entryName)
    {
        // .NET 8's GNU-longname parsing (fixed in .NET 9+) can leave a trailing NUL terminator in the
        // returned name/link-name for paths that overflow the classic 100-char tar header field.
        int nullIndex = entryName.IndexOf('\0');
        if (nullIndex >= 0)
        {
            entryName = entryName[..nullIndex];
        }

        string destination = Path.GetFullPath(Path.Combine(root, entryName));
        if (destination != root && !destination.StartsWith(root + Path.DirectorySeparatorChar, StringComparison.Ordinal))
        {
            throw new IOException($"Tar entry '{entryName}' would extract outside of the destination directory.");
        }

        return destination;
    }
}
