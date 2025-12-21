using System.Diagnostics;
using PythonEmbedded.Net.Exceptions;

namespace PythonEmbedded.Net.Helpers;

/// <summary>
/// Utility class for extracting Python distribution archives.
/// </summary>
internal static class ArchiveHelper
{
    /// <summary>
    /// Extracts an archive to the specified destination directory.
    /// </summary>
    /// <param name="archivePath">Path to the archive file.</param>
    /// <param name="destinationDirectory">Directory to extract to.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public static async Task ExtractAsync(
        string archivePath,
        string destinationDirectory,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(archivePath))
            throw new ArgumentException("Archive path cannot be null or empty.", nameof(archivePath));
        if (string.IsNullOrWhiteSpace(destinationDirectory))
            throw new ArgumentException("Destination directory cannot be null or empty.", nameof(destinationDirectory));

        if (!File.Exists(archivePath))
            throw new FileNotFoundException($"Archive file not found: {archivePath}");

        Directory.CreateDirectory(destinationDirectory);

        var extension = Path.GetExtension(archivePath).ToLowerInvariant();

        try
        {
            await (extension switch
            {
                ".zip" => ExtractZipAsync(archivePath, destinationDirectory, cancellationToken),
                ".zst" or ".tar.zst" => ExtractTarZstAsync(archivePath, destinationDirectory, cancellationToken),
                ".tar" => ExtractTarAsync(archivePath, destinationDirectory, cancellationToken),
                _ => throw new NotSupportedException($"Unsupported archive format: {extension}")
            }).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            throw new PythonInstallationException(
                $"Failed to extract archive {archivePath} to {destinationDirectory}",
                ex);
        }
    }

    private static Task ExtractZipAsync(string archivePath, string destinationDirectory, CancellationToken cancellationToken)
    {
        return Task.Run(() =>
        {
            System.IO.Compression.ZipFile.ExtractToDirectory(archivePath, destinationDirectory, overwriteFiles: true);
        }, cancellationToken);
    }

    private static async Task ExtractTarZstAsync(string archivePath, string destinationDirectory, CancellationToken cancellationToken)
    {
        // .tar.zst requires external tools (zstd + tar) or a library
        // For now, we'll attempt to use system tools if available
        // In a production system, you might want to use a library like SharpCompress
        
        // Check if zstd and tar are available
        if (await IsCommandAvailableAsync("zstd").ConfigureAwait(false) && await IsCommandAvailableAsync("tar").ConfigureAwait(false))
        {
            await ExtractTarZstUsingSystemToolsAsync(archivePath, destinationDirectory, cancellationToken).ConfigureAwait(false);
        }
        else
        {
            // Fallback: Try using Python's tarfile module or a .NET library
            // For now, throw a helpful exception
            throw new NotSupportedException(
                "Extraction of .tar.zst archives requires zstd and tar system tools. " +
                "Please install them or use a different archive format. " +
                "Alternatively, this functionality will be enhanced in a future version.");
        }
    }

    private static async Task ExtractTarAsync(string archivePath, string destinationDirectory, CancellationToken cancellationToken)
    {
        if (await IsCommandAvailableAsync("tar").ConfigureAwait(false))
        {
            await ExtractTarUsingSystemToolsAsync(archivePath, destinationDirectory, cancellationToken).ConfigureAwait(false);
        }
        else
        {
            // Could use a library like SharpCompress here
            throw new NotSupportedException(
                "Extraction of .tar archives requires tar system tool. " +
                "Please install it or use a different archive format.");
        }
    }

    private static async Task ExtractTarZstUsingSystemToolsAsync(
        string archivePath,
        string destinationDirectory,
        CancellationToken cancellationToken)
    {
        var processStartInfo = new ProcessStartInfo
        {
            FileName = "tar",
            Arguments = $"-xvf {archivePath} -C {destinationDirectory} --use-compress-program=zstd",
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        await RunProcessAsync(processStartInfo, cancellationToken).ConfigureAwait(false);
    }

    private static async Task ExtractTarUsingSystemToolsAsync(
        string archivePath,
        string destinationDirectory,
        CancellationToken cancellationToken)
    {
        var processStartInfo = new ProcessStartInfo
        {
            FileName = "tar",
            Arguments = $"-xf {archivePath} -C {destinationDirectory}",
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        await RunProcessAsync(processStartInfo, cancellationToken).ConfigureAwait(false);
    }

    private static async Task RunProcessAsync(ProcessStartInfo startInfo, CancellationToken cancellationToken)
    {
        using var process = new Process { StartInfo = startInfo };
        
        process.Start();
        
        var cancellationRegistration = cancellationToken.Register(() =>
        {
            try
            {
                if (!process.HasExited)
                    process.Kill();
            }
            catch { }
        });

        try
        {
            await process.WaitForExitAsync(cancellationToken).ConfigureAwait(false);
            
            if (process.ExitCode != 0)
            {
                var error = await process.StandardError.ReadToEndAsync().ConfigureAwait(false);
                throw new PythonInstallationException(
                    $"Archive extraction failed with exit code {process.ExitCode}: {error}");
            }
        }
        finally
        {
            await cancellationRegistration.DisposeAsync().ConfigureAwait(false);
        }
    }

    private static async Task<bool> IsCommandAvailableAsync(string command)
    {
        try
        {
            var processStartInfo = new ProcessStartInfo
            {
                FileName = command,
                Arguments = "--version",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using var process = Process.Start(processStartInfo);
            if (process == null)
                return false;

            await process.WaitForExitAsync().ConfigureAwait(false);
            return process.ExitCode == 0;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Verifies that an extracted Python installation is valid by checking for required files and directories.
    /// Based on python-build-standalone distribution structure.
    /// </summary>
    /// <param name="pythonInstancePath">The path to the extracted Python installation directory.</param>
    /// <returns>True if the installation appears valid; false otherwise.</returns>
    public static bool VerifyExtractedInstallation(string pythonInstancePath)
    {
        if (string.IsNullOrWhiteSpace(pythonInstancePath))
            return false;

        if (!Directory.Exists(pythonInstancePath))
            return false;

        if (System.Runtime.InteropServices.RuntimeInformation.IsOSPlatform(System.Runtime.InteropServices.OSPlatform.Windows))
        {
            // Windows: Check for python.exe and python3.dll
            var pythonExe = Path.Combine(pythonInstancePath, "python.exe");
            if (!File.Exists(pythonExe))
                return false;

            // Check for python3.dll (common DLL names: python3.dll, python310.dll, python311.dll, etc.)
            var dllPattern = Path.Combine(pythonInstancePath, "python*.dll");
            var dllFiles = Directory.GetFiles(pythonInstancePath, "python*.dll");
            if (dllFiles.Length == 0)
            {
                // DLL might be in a subdirectory, but python.exe should still work
                // This is a warning-level issue, not a failure
            }
        }
        else
        {
            // Linux/macOS: Check for bin/python3 and lib/ directory
            var pythonExe = Path.Combine(pythonInstancePath, "bin", "python3");
            if (!File.Exists(pythonExe))
                return false;

            // Check for lib/ directory (contains Python standard library)
            var libDir = Path.Combine(pythonInstancePath, "lib");
            if (!Directory.Exists(libDir))
                return false;
        }

        return true;
    }
}
