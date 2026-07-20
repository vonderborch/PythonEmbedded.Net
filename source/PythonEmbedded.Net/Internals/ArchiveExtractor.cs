using System.Formats.Tar;
using System.IO.Compression;

namespace PythonEmbedded.Net;

/// <summary>Extracts python-build-standalone archives (.tar.gz or .zip) preserving Unix permissions.</summary>
internal static class ArchiveExtractor
{
    public static async Task ExtractAsync(string archivePath, string targetDirectory, CancellationToken ct)
    {
        Directory.CreateDirectory(targetDirectory);
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
                await TarFile.ExtractToDirectoryAsync(gzip, targetDirectory, overwriteFiles: true, ct).ConfigureAwait(false);
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
}
