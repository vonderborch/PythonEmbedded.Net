using System.Runtime.InteropServices;
using Microsoft.Extensions.Logging;
using PythonEmbedded.Net.Exceptions;
using PythonEmbedded.Net.Helpers;
using PythonEmbedded.Net.Models;

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
    /// Gets the instance metadata for this root runtime.
    /// </summary>
    protected abstract InstanceMetadata InstanceMetadata { get; }

    /// <summary>
    /// Saves the instance metadata to disk.
    /// </summary>
    protected void SaveInstanceMetadata()
    {
        InstanceMetadata.Save(InstanceMetadata.Directory);
    }

    /// <summary>
    /// Gets or creates a virtual environment with the specified name.
    /// </summary>
    /// <param name="name">The name of the virtual environment.</param>
    /// <param name="recreateIfExists">Whether to recreate the virtual environment if it already exists.</param>
    /// <param name="externalPath">Optional external path where the venv should be created. If null, uses default location.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The virtual environment runtime.</returns>
    public async Task<BasePythonVirtualRuntime> GetOrCreateVirtualEnvironmentAsync(
        string name,
        bool recreateIfExists = false,
        string? externalPath = null,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Virtual environment name cannot be null or empty.", nameof(name));

        ValidateInstallation();

        bool isExternal = !string.IsNullOrWhiteSpace(externalPath);
        string defaultPath = Path.Combine(VirtualEnvironmentsDirectory, name);
        
        // Check if a venv with this name already exists in metadata
        var existingVenvMetadata = InstanceMetadata.GetVirtualEnvironment(name);
        bool nameAlreadyExists = existingVenvMetadata != null;
        
        string actualVenvPath = isExternal 
            ? externalPath! 
            : existingVenvMetadata?.GetResolvedPath(defaultPath) ?? defaultPath;
        
        bool venvAtPathExists = Directory.Exists(actualVenvPath) && IsValidVirtualEnvironment(actualVenvPath);

        if (nameAlreadyExists && recreateIfExists)
        {
            this.Logger?.LogInformation("Recreating virtual environment: {Name}", name);
            await DeleteVirtualEnvironmentAsync(name, cancellationToken).ConfigureAwait(false);
            nameAlreadyExists = false;
            venvAtPathExists = false;
            existingVenvMetadata = null;
        }

        if (!venvAtPathExists)
        {
            // Check for duplicate name before creating
            if (nameAlreadyExists)
            {
                throw new InvalidOperationException(
                    $"A virtual environment with name '{name}' already exists. " +
                    $"Use recreateIfExists=true to replace it, or choose a different name.");
            }

            this.Logger?.LogInformation("Creating virtual environment: {Name} at {Path}", name, actualVenvPath);
            await CreateVirtualEnvironmentAsync(actualVenvPath, cancellationToken).ConfigureAwait(false);

            // Create metadata for the venv
            var venvMetadata = new VirtualEnvironmentMetadata
            {
                Name = name,
                CreatedDate = DateTime.UtcNow,
                ExternalPath = isExternal ? Path.GetFullPath(externalPath!) : null
            };
            
            InstanceMetadata.SetVirtualEnvironment(venvMetadata);
            SaveInstanceMetadata();
        }
        else
        {
            this.Logger?.LogInformation("Using existing virtual environment: {Name} at {Path}", name, actualVenvPath);
        }

        // Create the virtual runtime instance
        var venvRuntime = CreateVirtualRuntimeInstance(actualVenvPath);
        
        // Ensure uv is available in the virtual environment
        await venvRuntime.EnsureUvInstalledAsync(cancellationToken).ConfigureAwait(false);
        
        return venvRuntime;
    }

    /// <summary>
    /// Gets or creates a virtual environment with the specified name (synchronous version).
    /// </summary>
    /// <param name="name">The name of the virtual environment.</param>
    /// <param name="recreateIfExists">Whether to recreate the virtual environment if it already exists.</param>
    /// <param name="externalPath">Optional external path where the venv should be created. If null, uses default location.</param>
    /// <returns>The virtual environment runtime.</returns>
    public BasePythonVirtualRuntime GetOrCreateVirtualEnvironment(
        string name,
        bool recreateIfExists = false,
        string? externalPath = null)
    {
        Task<BasePythonVirtualRuntime> task = GetOrCreateVirtualEnvironmentAsync(name, recreateIfExists, externalPath);
        task.Wait();
        return task.Result;
    }

    /// <summary>
    /// Deletes a virtual environment with the specified name.
    /// For external venvs, this deletes both the metadata and the actual venv directory.
    /// </summary>
    /// <param name="name">The name of the virtual environment to delete.</param>
    /// <param name="deleteExternalFiles">For external venvs, whether to delete the actual files (default true).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>True if the virtual environment was deleted; false if it didn't exist.</returns>
    public async Task<bool> DeleteVirtualEnvironmentAsync(
        string name,
        CancellationToken cancellationToken = default,
        bool deleteExternalFiles = true)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Virtual environment name cannot be null or empty.", nameof(name));

        var venvMetadata = InstanceMetadata.GetVirtualEnvironment(name);
        string defaultPath = Path.Combine(VirtualEnvironmentsDirectory, name);
        string actualVenvPath = venvMetadata?.GetResolvedPath(defaultPath) ?? defaultPath;
        bool isExternal = venvMetadata?.IsExternal ?? false;

        if (venvMetadata == null && !Directory.Exists(actualVenvPath))
        {
            this.Logger?.LogWarning("Virtual environment not found: {Name}", name);
            return false;
        }

        if (!isExternal && !IsValidVirtualEnvironment(actualVenvPath) && Directory.Exists(actualVenvPath))
        {
            this.Logger?.LogWarning("Directory exists but is not a valid virtual environment: {Path}", actualVenvPath);
            return false;
        }

        this.Logger?.LogInformation("Deleting virtual environment: {Name} at {Path}", name, actualVenvPath);

        try
        {
            // Delete the actual venv (unless it's external and deleteExternalFiles is false)
            if (!isExternal || deleteExternalFiles)
            {
                if (Directory.Exists(actualVenvPath))
                {
                    await DeleteDirectoryWithRetryAsync(actualVenvPath, cancellationToken).ConfigureAwait(false);
                }
            }

            // Remove from metadata
            if (InstanceMetadata.RemoveVirtualEnvironment(name))
            {
                SaveInstanceMetadata();
            }

            this.Logger?.LogInformation("Successfully deleted virtual environment: {Name}", name);
            return true;
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
    }

    /// <summary>
    /// Deletes a virtual environment with the specified name (synchronous version).
    /// </summary>
    /// <param name="name">The name of the virtual environment to delete.</param>
    /// <param name="deleteExternalFiles">For external venvs, whether to delete the actual files (default true).</param>
    /// <returns>True if the virtual environment was deleted; false if it didn't exist.</returns>
    public bool DeleteVirtualEnvironment(string name, bool deleteExternalFiles = true)
    {
        Task<bool> task = DeleteVirtualEnvironmentAsync(name, default, deleteExternalFiles);
        task.Wait();
        return task.Result;
    }

    /// <summary>
    /// Lists all virtual environments managed by this root runtime.
    /// </summary>
    /// <returns>A list of virtual environment names.</returns>
    public IReadOnlyList<string> ListVirtualEnvironments()
    {
        var virtualEnvironments = new List<string>();

        // Get venvs from metadata
        foreach (var venvMetadata in InstanceMetadata.VirtualEnvironments)
        {
            string defaultPath = Path.Combine(VirtualEnvironmentsDirectory, venvMetadata.Name);
            string actualPath = venvMetadata.GetResolvedPath(defaultPath);
            
            if (Directory.Exists(actualPath) && IsValidVirtualEnvironment(actualPath))
            {
                virtualEnvironments.Add(venvMetadata.Name);
            }
        }

        // Also check for legacy venvs (directories without metadata entries)
        if (Directory.Exists(VirtualEnvironmentsDirectory))
        {
            foreach (var directory in Directory.GetDirectories(VirtualEnvironmentsDirectory))
            {
                string name = Path.GetFileName(directory);
                
                // Skip if already in metadata
                if (InstanceMetadata.GetVirtualEnvironment(name) != null)
                    continue;
                
                // Check if it's a valid venv without metadata entry
                if (IsValidVirtualEnvironment(directory))
                {
                    virtualEnvironments.Add(name);
                }
            }
        }

        return virtualEnvironments.Distinct().ToList().AsReadOnly();
    }

    /// <summary>
    /// Resolves the actual path to a virtual environment from its name.
    /// Checks metadata for external paths.
    /// </summary>
    /// <param name="name">The name of the virtual environment.</param>
    /// <returns>The actual path to the virtual environment.</returns>
    public string ResolveVirtualEnvironmentPath(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Virtual environment name cannot be null or empty.", nameof(name));

        string defaultPath = Path.Combine(VirtualEnvironmentsDirectory, name);
        var venvMetadata = InstanceMetadata.GetVirtualEnvironment(name);
        
        return venvMetadata?.GetResolvedPath(defaultPath) ?? defaultPath;
    }

    /// <summary>
    /// Checks if a virtual environment with the specified name exists.
    /// </summary>
    /// <param name="name">The name of the virtual environment.</param>
    /// <returns>True if a venv with this name exists (standard or external), false otherwise.</returns>
    public bool VirtualEnvironmentExists(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
            return false;

        // Check in metadata
        var venvMetadata = InstanceMetadata.GetVirtualEnvironment(name);
        if (venvMetadata != null)
        {
            string defaultPath = Path.Combine(VirtualEnvironmentsDirectory, name);
            string actualPath = venvMetadata.GetResolvedPath(defaultPath);
            return Directory.Exists(actualPath) && IsValidVirtualEnvironment(actualPath);
        }

        // Legacy support: check if directory is a valid venv without metadata
        string legacyPath = Path.Combine(VirtualEnvironmentsDirectory, name);
        return Directory.Exists(legacyPath) && IsValidVirtualEnvironment(legacyPath);
    }

    /// <summary>
    /// Gets the metadata for a virtual environment.
    /// </summary>
    /// <param name="name">The name of the virtual environment.</param>
    /// <returns>The metadata if found, null otherwise.</returns>
    public VirtualEnvironmentMetadata? GetVirtualEnvironmentMetadata(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
            return null;

        return InstanceMetadata.GetVirtualEnvironment(name);
    }

    /// <summary>
    /// Creates a virtual runtime instance for the specified virtual environment path.
    /// </summary>
    /// <param name="venvPath">The path to the virtual environment directory.</param>
    /// <returns>A virtual runtime instance.</returns>
    protected abstract BasePythonVirtualRuntime CreateVirtualRuntimeInstance(string venvPath);

    /// <summary>
    /// Creates a virtual environment at the specified path using uv (significantly faster than python -m venv).
    /// </summary>
    /// <param name="venvPath">The path where to create the virtual environment.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
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

        // Ensure uv is available (auto-install if needed)
        await EnsureUvAvailableAsync(cancellationToken).ConfigureAwait(false);

        this.Logger?.LogInformation("Creating virtual environment at: {Path}", venvPath);

        var uvArgs = new List<string> { "venv", venvPath, "--python", PythonExecutablePath };
        var result = await ExecuteUvAsync(uvArgs, cancellationToken).ConfigureAwait(false);

        if (result.ExitCode != 0)
        {
            throw new PythonInstallationException(
                $"Failed to create virtual environment at '{venvPath}'. Exit code: {result.ExitCode}. Error: {result.StandardError}");
        }

        this.Logger?.LogInformation("Successfully created virtual environment");
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

        var venvPath = ResolveVirtualEnvironmentPath(name);
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

        var venvPath = ResolveVirtualEnvironmentPath(name);
        if (!Directory.Exists(venvPath))
            throw new DirectoryNotFoundException($"Virtual environment not found: {name}");

        var venvMetadata = InstanceMetadata.GetVirtualEnvironment(name);

        var info = new Dictionary<string, object>
        {
            ["Name"] = name,
            ["Path"] = venvPath,
            ["SizeBytes"] = CalculateDirectorySize(venvPath),
            ["Exists"] = true,
            ["Created"] = venvMetadata?.CreatedDate ?? Directory.GetCreationTime(venvPath),
            ["Modified"] = Directory.GetLastWriteTime(venvPath),
            ["IsExternal"] = venvMetadata?.IsExternal ?? false
        };

        if (venvMetadata?.IsExternal == true)
        {
            info["ExternalPath"] = venvMetadata.ExternalPath!;
        }

        // Try to get Python version from the venv
        var pythonExe = RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
            ? Path.Combine(venvPath, "Scripts", "python.exe")
            : Path.Combine(venvPath, "bin", "python3");

        if (File.Exists(pythonExe))
        {
            try
            {
                var venvRuntime = CreateVirtualRuntimeInstance(venvPath);
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

        var sourcePath = ResolveVirtualEnvironmentPath(sourceName);
        if (!Directory.Exists(sourcePath))
            throw new DirectoryNotFoundException($"Source virtual environment not found: {sourceName}");

        // Check if target already exists
        if (VirtualEnvironmentExists(targetName))
            throw new InvalidOperationException($"Target virtual environment already exists: {targetName}");

        var targetPath = Path.Combine(VirtualEnvironmentsDirectory, targetName);

        this.Logger?.LogInformation("Cloning virtual environment {Source} to {Target}", sourceName, targetName);

        try
        {
            // Create target directory
            Directory.CreateDirectory(targetPath);

            // Copy all files and directories from source to target
            await CopyDirectoryAsync(sourcePath, targetPath, cancellationToken).ConfigureAwait(false);

            // Create metadata for the cloned venv
            var venvMetadata = new VirtualEnvironmentMetadata
            {
                Name = targetName,
                CreatedDate = DateTime.UtcNow
            };
            
            InstanceMetadata.SetVirtualEnvironment(venvMetadata);
            SaveInstanceMetadata();

            this.Logger?.LogInformation("Successfully cloned virtual environment {Source} to {Target}", sourceName, targetName);

            // Create the virtual runtime instance and ensure uv is available
            var venvRuntime = CreateVirtualRuntimeInstance(targetPath);
            await venvRuntime.EnsureUvInstalledAsync(cancellationToken).ConfigureAwait(false);
            
            return venvRuntime;
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

            // Clean up metadata if it was added
            InstanceMetadata.RemoveVirtualEnvironment(targetName);
            SaveInstanceMetadata();

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

        var venvPath = ResolveVirtualEnvironmentPath(name);
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

        // Check if venv already exists
        if (VirtualEnvironmentExists(name))
            throw new InvalidOperationException($"Virtual environment already exists: {name}");

        var targetPath = Path.Combine(VirtualEnvironmentsDirectory, name);

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

            // Create metadata for the imported venv
            var venvMetadata = new VirtualEnvironmentMetadata
            {
                Name = name,
                CreatedDate = DateTime.UtcNow
            };
            
            InstanceMetadata.SetVirtualEnvironment(venvMetadata);
            SaveInstanceMetadata();

            this.Logger?.LogInformation("Successfully imported virtual environment {Name} from {Path}", name, archivePath);

            // Create the virtual runtime instance and ensure uv is available
            var venvRuntime = CreateVirtualRuntimeInstance(targetPath);
            await venvRuntime.EnsureUvInstalledAsync(cancellationToken).ConfigureAwait(false);
            
            return venvRuntime;
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

            // Clean up metadata if it was added
            InstanceMetadata.RemoveVirtualEnvironment(name);
            SaveInstanceMetadata();

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

    /// <summary>
    /// Deletes a directory with retry logic for locked files.
    /// </summary>
    private async Task DeleteDirectoryWithRetryAsync(string path, CancellationToken cancellationToken)
    {
        int attempts = 0;
        const int maxAttempts = 5;

        while (attempts < maxAttempts)
        {
            try
            {
                Directory.Delete(path, true);
                return;
            }
            catch (IOException) when (attempts < maxAttempts - 1)
            {
                attempts++;
                await Task.Delay(100 * attempts, cancellationToken).ConfigureAwait(false);
            }
        }

        // Final attempt - let it throw if it fails
        Directory.Delete(path, true);
    }
}
