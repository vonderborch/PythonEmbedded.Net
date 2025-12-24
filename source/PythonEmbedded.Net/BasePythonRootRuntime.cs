using System.Runtime.InteropServices;
using Microsoft.Extensions.Logging;
using PythonEmbedded.Net.Exceptions;
using PythonEmbedded.Net.Helpers;

namespace PythonEmbedded.Net;

/// <summary>
/// Base class for Python root runtime implementations that can manage virtual environments.
/// </summary>
public abstract class BasePythonRootRuntime : BasePythonRuntime
{
    /// <summary>
    /// Gets the base directory for virtual environments managed by this root runtime.
    /// </summary>
    protected abstract string VirtualEnvironmentsDirectory { get; }

    /// <summary>
    /// Gets or creates a virtual environment with the specified name.
    /// </summary>
    /// <param name="name">The name of the virtual environment.</param>
    /// <param name="recreateIfExists">Whether to recreate the virtual environment if it already exists.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The virtual environment runtime.</returns>
    public async Task<BasePythonVirtualRuntime> GetOrCreateVirtualEnvironmentAsync(
        string name,
        bool recreateIfExists = false,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Virtual environment name cannot be null or empty.", nameof(name));

        ValidateInstallation();

        string venvPath = Path.Combine(VirtualEnvironmentsDirectory, name);
        bool exists = Directory.Exists(venvPath) && IsValidVirtualEnvironment(venvPath);

        if (exists && recreateIfExists)
        {
            this.Logger?.LogInformation("Recreating virtual environment: {Name}", name);
            Directory.Delete(venvPath, true);
            exists = false;
        }

        if (!exists)
        {
            this.Logger?.LogInformation("Creating virtual environment: {Name} at {Path}", name, venvPath);
            await CreateVirtualEnvironmentAsync(venvPath, cancellationToken);
        }
        else
        {
            this.Logger?.LogInformation("Using existing virtual environment: {Name} at {Path}", name, venvPath);
        }

        return CreateVirtualRuntimeInstance(venvPath);
    }

    /// <summary>
    /// Gets or creates a virtual environment with the specified name (synchronous version).
    /// </summary>
    /// <param name="name">The name of the virtual environment.</param>
    /// <param name="recreateIfExists">Whether to recreate the virtual environment if it already exists.</param>
    /// <returns>The virtual environment runtime.</returns>
    public BasePythonVirtualRuntime GetOrCreateVirtualEnvironment(
        string name,
        bool recreateIfExists = false)
    {
        Task<BasePythonVirtualRuntime> task = GetOrCreateVirtualEnvironmentAsync(name, recreateIfExists);
        task.Wait();
        return task.Result;
    }

    /// <summary>
    /// Deletes a virtual environment with the specified name.
    /// </summary>
    /// <param name="name">The name of the virtual environment to delete.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>True if the virtual environment was deleted; false if it didn't exist.</returns>
    public async Task<bool> DeleteVirtualEnvironmentAsync(
        string name,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Virtual environment name cannot be null or empty.", nameof(name));

        string venvPath = Path.Combine(VirtualEnvironmentsDirectory, name);

        if (!Directory.Exists(venvPath))
        {
            this.Logger?.LogWarning("Virtual environment not found: {Name} at {Path}", name, venvPath);
            return false;
        }

        if (!IsValidVirtualEnvironment(venvPath))
        {
            this.Logger?.LogWarning("Directory exists but is not a valid virtual environment: {Path}", venvPath);
            return false;
        }

        this.Logger?.LogInformation("Deleting virtual environment: {Name} at {Path}", name, venvPath);

        try
        {
            // On Windows, files might be locked, so we need to retry
            int attempts = 0;
            const int maxAttempts = 5;
            while (attempts < maxAttempts)
            {
                try
                {
                    Directory.Delete(venvPath, true);
                    this.Logger?.LogInformation("Successfully deleted virtual environment: {Name}", name);
                    return true;
                }
                catch (IOException) when (attempts < maxAttempts - 1)
                {
                    attempts++;
                    await Task.Delay(100 * attempts, cancellationToken).ConfigureAwait(false);
                }
            }
        }
        catch (Exception ex)
        {
            this.Logger?.LogError(ex, "Failed to delete virtual environment: {Name}", name);
            throw new VirtualEnvironmentNotFoundException(
                $"Failed to delete virtual environment '{name}': {ex.Message}",
                ex)
            {
                VirtualEnvironmentName = name
            };
        }

        return false;
    }

    /// <summary>
    /// Deletes a virtual environment with the specified name (synchronous version).
    /// </summary>
    /// <param name="name">The name of the virtual environment to delete.</param>
    /// <returns>True if the virtual environment was deleted; false if it didn't exist.</returns>
    public bool DeleteVirtualEnvironment(string name)
    {
        Task<bool> task = DeleteVirtualEnvironmentAsync(name);
        task.Wait();
        return task.Result;
    }

    /// <summary>
    /// Lists all virtual environments managed by this root runtime.
    /// </summary>
    /// <returns>A list of virtual environment names.</returns>
    public IReadOnlyList<string> ListVirtualEnvironments()
    {
        if (!Directory.Exists(VirtualEnvironmentsDirectory))
        {
            return Array.Empty<string>();
        }

        var virtualEnvironments = new List<string>();
        var directories = Directory.GetDirectories(VirtualEnvironmentsDirectory);

        foreach (var directory in directories)
        {
            string name = Path.GetFileName(directory);
            if (IsValidVirtualEnvironment(directory))
            {
                virtualEnvironments.Add(name);
            }
        }

        return virtualEnvironments.AsReadOnly();
    }

    /// <summary>
    /// Creates a virtual runtime instance for the specified virtual environment path.
    /// </summary>
    /// <param name="venvPath">The path to the virtual environment directory.</param>
    /// <returns>A virtual runtime instance.</returns>
    protected abstract BasePythonVirtualRuntime CreateVirtualRuntimeInstance(string venvPath);

    /// <summary>
    /// Creates a virtual environment at the specified path.
    /// </summary>
    protected virtual async Task CreateVirtualEnvironmentAsync(
        string venvPath,
        CancellationToken cancellationToken = default)
    {
        // Ensure the parent directory exists
        string? parentDir = Path.GetDirectoryName(venvPath);
        if (!string.IsNullOrEmpty(parentDir) && !Directory.Exists(parentDir))
        {
            Directory.CreateDirectory(parentDir);
        }

        // Use the built-in venv module (available in Python 3.3+)
        // This is preferred over virtualenv as it's part of the standard library
        var result = await ExecuteProcessAsync(
            ["-m", "venv", venvPath],
            null,
            null,
            null,
            cancellationToken, null, null).ConfigureAwait(false);

        if (result.ExitCode != 0)
        {
            throw new PythonInstallationException(
                $"Failed to create virtual environment at '{venvPath}'. Exit code: {result.ExitCode}. Error: {result.StandardError}");
        }
    }

    /// <summary>
    /// Checks if a directory is a valid virtual environment.
    /// </summary>
    protected virtual bool IsValidVirtualEnvironment(string directory)
    {
        if (string.IsNullOrWhiteSpace(directory) || !Directory.Exists(directory))
            return false;

        // Check for Python executable in the virtual environment
        var pythonExe = RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
            ? Path.Combine(directory, "Scripts", "python.exe")
            : Path.Combine(directory, "bin", "python3");

        return File.Exists(pythonExe);
    }

    /// <summary>
    /// Gets the disk usage of a virtual environment in bytes.
    /// </summary>
    /// <param name="name">The name of the virtual environment.</param>
    /// <returns>The size in bytes.</returns>
    public long GetVirtualEnvironmentSize(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Virtual environment name cannot be null or empty.", nameof(name));

        var venvPath = Path.Combine(VirtualEnvironmentsDirectory, name);
        if (!Directory.Exists(venvPath))
            throw new DirectoryNotFoundException($"Virtual environment not found: {name}");

        return CalculateDirectorySize(venvPath);
    }

    /// <summary>
    /// Gets detailed information about a virtual environment.
    /// </summary>
    /// <param name="name">The name of the virtual environment.</param>
    /// <returns>A dictionary containing information about the virtual environment.</returns>
    public Dictionary<string, object> GetVirtualEnvironmentInfo(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Virtual environment name cannot be null or empty.", nameof(name));

        var venvPath = Path.Combine(VirtualEnvironmentsDirectory, name);
        if (!Directory.Exists(venvPath))
            throw new DirectoryNotFoundException($"Virtual environment not found: {name}");

        var info = new Dictionary<string, object>
        {
            ["Name"] = name,
            ["Path"] = venvPath,
            ["SizeBytes"] = CalculateDirectorySize(venvPath),
            ["Exists"] = true,
            ["Created"] = Directory.GetCreationTime(venvPath),
            ["Modified"] = Directory.GetLastWriteTime(venvPath)
        };

        // Try to get Python version from the venv
        var pythonExe = RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
            ? Path.Combine(venvPath, "Scripts", "python.exe")
            : Path.Combine(venvPath, "bin", "python3");

        if (File.Exists(pythonExe))
        {
            try
            {
                var venvRuntime = GetOrCreateVirtualEnvironmentAsync(name).GetAwaiter().GetResult();
                var versionInfo = venvRuntime.GetPythonVersionInfo();
                info["PythonVersion"] = versionInfo;
            }
            catch
            {
                // Ignore errors getting version info
            }
        }

        return info;
    }

    /// <summary>
    /// Calculates the total size of a directory and its contents in bytes.
    /// </summary>
    protected static long CalculateDirectorySize(string directoryPath)
    {
        if (!Directory.Exists(directoryPath))
            return 0;

        long size = 0;
        var directoryInfo = new DirectoryInfo(directoryPath);

        try
        {
            // Add file sizes
            foreach (var file in directoryInfo.GetFiles("*", SearchOption.TopDirectoryOnly))
            {
                try
                {
                    size += file.Length;
                }
                catch
                {
                    // Ignore files that can't be accessed
                }
            }

            // Recursively add subdirectory sizes
            foreach (var directory in directoryInfo.GetDirectories("*", SearchOption.TopDirectoryOnly))
            {
                try
                {
                    size += CalculateDirectorySize(directory.FullName);
                }
                catch
                {
                    // Ignore directories that can't be accessed
                }
            }
        }
        catch
        {
            // Return partial size if there are access issues
        }

        return size;
    }

    /// <summary>
    /// Clones a virtual environment to create a new one with the same packages and configuration.
    /// </summary>
    /// <param name="sourceName">The name of the source virtual environment.</param>
    /// <param name="targetName">The name of the target virtual environment.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The cloned virtual environment runtime.</returns>
    public async Task<BasePythonVirtualRuntime> CloneVirtualEnvironmentAsync(
        string sourceName,
        string targetName,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(sourceName))
            throw new ArgumentException("Source virtual environment name cannot be null or empty.", nameof(sourceName));
        if (string.IsNullOrWhiteSpace(targetName))
            throw new ArgumentException("Target virtual environment name cannot be null or empty.", nameof(targetName));

        var sourcePath = Path.Combine(VirtualEnvironmentsDirectory, sourceName);
        if (!Directory.Exists(sourcePath))
            throw new DirectoryNotFoundException($"Source virtual environment not found: {sourceName}");

        var targetPath = Path.Combine(VirtualEnvironmentsDirectory, targetName);
        if (Directory.Exists(targetPath))
            throw new InvalidOperationException($"Target virtual environment already exists: {targetName}");

        this.Logger?.LogInformation("Cloning virtual environment {Source} to {Target}", sourceName, targetName);

        try
        {
            // Create target directory
            Directory.CreateDirectory(targetPath);

            // Copy all files and directories from source to target
            await CopyDirectoryAsync(sourcePath, targetPath, cancellationToken).ConfigureAwait(false);

            this.Logger?.LogInformation("Successfully cloned virtual environment {Source} to {Target}", sourceName, targetName);

            return await GetOrCreateVirtualEnvironmentAsync(targetName, cancellationToken: cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            // Clean up target directory if creation failed
            if (Directory.Exists(targetPath))
            {
                try
                {
                    Directory.Delete(targetPath, true);
                }
                catch
                {
                    // Ignore cleanup errors
                }
            }

            this.Logger?.LogError(ex, "Failed to clone virtual environment {Source} to {Target}", sourceName, targetName);
            throw;
        }
    }

    /// <summary>
    /// Exports a virtual environment to an archive file.
    /// </summary>
    /// <param name="name">The name of the virtual environment to export.</param>
    /// <param name="outputPath">The path where to save the archive file.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The path to the created archive file.</returns>
    public async Task<string> ExportVirtualEnvironmentAsync(
        string name,
        string outputPath,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Virtual environment name cannot be null or empty.", nameof(name));
        if (string.IsNullOrWhiteSpace(outputPath))
            throw new ArgumentException("Output path cannot be null or empty.", nameof(outputPath));

        var venvPath = Path.Combine(VirtualEnvironmentsDirectory, name);
        if (!Directory.Exists(venvPath))
            throw new DirectoryNotFoundException($"Virtual environment not found: {name}");

        this.Logger?.LogInformation("Exporting virtual environment {Name} to {Path}", name, outputPath);

        // Ensure output directory exists
        var outputDir = Path.GetDirectoryName(outputPath);
        if (!string.IsNullOrEmpty(outputDir) && !Directory.Exists(outputDir))
        {
            Directory.CreateDirectory(outputDir);
        }

        // Determine archive format from extension
        var extension = Path.GetExtension(outputPath).ToLowerInvariant();
        if (extension == ".zip")
        {
            System.IO.Compression.ZipFile.CreateFromDirectory(venvPath, outputPath);
        }
        else if (extension == ".tar" || 
                 extension == ".tar.gz" || 
                 extension == ".tar.bz" ||
                 extension == ".tar.bz2" ||
                 extension.EndsWith(".tar.zst", StringComparison.OrdinalIgnoreCase))
        {
            // For tar archives, we'll use ArchiveHelper's extraction logic in reverse
            // Since ArchiveHelper uses external tools, we'll use a simpler approach with System.IO.Compression
            // For full tar support, would need a library like SharpCompress
            throw new NotSupportedException($"Archive format '{extension}' is not supported for export. Please use .zip");
        }
        else
        {
            // Default to zip if no extension
            var zipPath = string.IsNullOrEmpty(Path.GetExtension(outputPath)) ? outputPath + ".zip" : outputPath;
            System.IO.Compression.ZipFile.CreateFromDirectory(venvPath, zipPath);
            outputPath = zipPath;
        }

        this.Logger?.LogInformation("Successfully exported virtual environment {Name} to {Path}", name, outputPath);
        return outputPath;
    }

    /// <summary>
    /// Imports a virtual environment from an archive file.
    /// </summary>
    /// <param name="name">The name for the imported virtual environment.</param>
    /// <param name="archivePath">The path to the archive file to import.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The imported virtual environment runtime.</returns>
    public async Task<BasePythonVirtualRuntime> ImportVirtualEnvironmentAsync(
        string name,
        string archivePath,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Virtual environment name cannot be null or empty.", nameof(name));
        if (string.IsNullOrWhiteSpace(archivePath))
            throw new ArgumentException("Archive path cannot be null or empty.", nameof(archivePath));
        if (!File.Exists(archivePath))
            throw new FileNotFoundException($"Archive file not found: {archivePath}", archivePath);

        var targetPath = Path.Combine(VirtualEnvironmentsDirectory, name);
        if (Directory.Exists(targetPath))
            throw new InvalidOperationException($"Virtual environment already exists: {name}");

        this.Logger?.LogInformation("Importing virtual environment {Name} from {Path}", name, archivePath);

        try
        {
            // Extract archive to target directory
            var extension = Path.GetExtension(archivePath).ToLowerInvariant();
            if (extension == ".zip")
            {
                System.IO.Compression.ZipFile.ExtractToDirectory(archivePath, targetPath);
            }
            else
            {
                // Use ArchiveHelper for other formats (tar, tar.gz, tar.zst)
                await ArchiveHelper.ExtractAsync(archivePath, targetPath, cancellationToken).ConfigureAwait(false);
            }

            this.Logger?.LogInformation("Successfully imported virtual environment {Name} from {Path}", name, archivePath);

            return await GetOrCreateVirtualEnvironmentAsync(name, recreateIfExists: false, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            // Clean up target directory if import failed
            if (Directory.Exists(targetPath))
            {
                try
                {
                    Directory.Delete(targetPath, true);
                }
                catch
                {
                    // Ignore cleanup errors
                }
            }

            this.Logger?.LogError(ex, "Failed to import virtual environment {Name} from {Path}", name, archivePath);
            throw;
        }
    }

    /// <summary>
    /// Copies a directory and its contents recursively.
    /// </summary>
    private async Task CopyDirectoryAsync(string sourceDir, string targetDir, CancellationToken cancellationToken)
    {
        Directory.CreateDirectory(targetDir);

        foreach (var file in Directory.GetFiles(sourceDir))
        {
            cancellationToken.ThrowIfCancellationRequested();
            var targetFile = Path.Combine(targetDir, Path.GetFileName(file));
            File.Copy(file, targetFile, overwrite: true);
        }

        foreach (var directory in Directory.GetDirectories(sourceDir))
        {
            cancellationToken.ThrowIfCancellationRequested();
            var targetSubDir = Path.Combine(targetDir, Path.GetFileName(directory));
            await CopyDirectoryAsync(directory, targetSubDir, cancellationToken).ConfigureAwait(false);
        }
    }
}
