using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Octokit;
using PythonEmbedded.Net.Exceptions;
using PythonEmbedded.Net.Helpers;
using PythonEmbedded.Net.Models;

namespace PythonEmbedded.Net;

public abstract class BasePythonManager
{
    private readonly string _rootDirectory;

    private readonly GitHubClient _gitHubClient;

    private readonly ILogger<BasePythonManager>? _logger;
    
    private readonly ILoggerFactory? _loggerFactory;

    private readonly ManagerMetadata _metadata;

    private readonly IMemoryCache? _cache;
    
    private ManagerConfiguration _configuration;

    /// <summary>
    /// Gets or sets the configuration for this manager.
    /// </summary>
    public ManagerConfiguration Configuration
    {
        get => _configuration;
        set => _configuration = value ?? throw new ArgumentNullException(nameof(value));
    }
    
    public BasePythonManager(
        string directory,
        GitHubClient githubClient,
        ILogger<BasePythonManager>? logger = null,
        ILoggerFactory? loggerFactory = null,
        IMemoryCache? cache = null,
        ManagerConfiguration? configuration = null
        )
    {
        // Argument handling
        if (string.IsNullOrWhiteSpace(directory))
        {
            throw new ArgumentException("Value cannot be null or whitespace.", nameof(directory));
        }
        
        // Github Client
        this._gitHubClient = githubClient;
        
        // Logging
        this._logger = logger;
        this._loggerFactory = loggerFactory;

        // Caching
        this._cache = cache;

        // Configuration
        this._configuration = configuration ?? new ManagerConfiguration();

        // Directory
        if (!Path.IsPathRooted(directory))
        {
            directory = Path.Combine(Directory.GetCurrentDirectory(), directory);
        }
        this._rootDirectory = directory;
        if (!Directory.Exists(this._rootDirectory))
        {
            Directory.CreateDirectory(this._rootDirectory);
            this._logger?.LogInformation("Created root directory: {RootDirectory}", this._rootDirectory);
        }
        else
        {
            this._logger?.LogDebug("Using existing root directory: {RootDirectory}", this._rootDirectory);
        }

        _metadata = new ManagerMetadata(directory);
    }

    public async Task<BasePythonRuntime> GetOrCreateInstanceAsync(
        string? pythonVersion = null,
        string? buildDate = null,
        CancellationToken cancellationToken = default)
    {
        // Use default version from configuration if not specified
        if (string.IsNullOrWhiteSpace(pythonVersion))
        {
            pythonVersion = this._configuration.DefaultPythonVersion ?? "3.12";
            this._logger?.LogDebug("Using default Python version from configuration: {Version}", pythonVersion);
        }
        
        this._logger?.LogInformation("Getting or creating Python instance: Version={Version}, BuildDate={BuildDate}",
            pythonVersion, buildDate ?? "latest");
        
        // Normalize version
        var normalizedVersion = VersionParser.NormalizeVersion(pythonVersion);
        
        var instance = this._metadata.FindInstance(normalizedVersion, buildDate);
        if (instance is null)
        {
            // Download, Extract, and add instance
            PlatformInfo platform = PlatformInfo.Detect();
            
            // Validate runtime requirements before attempting download
            try
            {
                platform.ValidateMinimumOsVersion();
                this._logger?.LogDebug("Runtime requirements validated successfully");
            }
            catch (Exceptions.PlatformNotSupportedException ex)
            {
                this._logger?.LogError(ex, "Runtime requirements validation failed");
                throw;
            }
            
            // Find the release asset
            this._logger?.LogInformation("Finding release asset for Python {Version} on platform {Platform}",
                normalizedVersion, platform.TargetTriple);
            var asset = await GitHubReleaseHelper.FindReleaseAssetAsync(
                this._gitHubClient,
                normalizedVersion,
                buildDate,
                platform,
                cancellationToken).ConfigureAwait(false);
            
            if (asset == null)
            {
                throw new InstanceNotFoundException(
                    $"No release asset found for Python {normalizedVersion} and build date {buildDate ?? "latest"}")
                {
                    PythonVersion = normalizedVersion,
                    BuildDate = buildDate
                };
            }
            
            // Extract build date from release if not provided
            string actualBuildDate = buildDate ?? await ExtractBuildDateFromReleaseAsync(
                normalizedVersion, asset, cancellationToken).ConfigureAwait(false);
            
            // Create instance directory
            string instanceDirectoryName = $"python-{normalizedVersion}-{actualBuildDate}";
            string instanceDirectory = Path.Combine(this._rootDirectory, instanceDirectoryName);
            
            if (Directory.Exists(instanceDirectory))
            {
                // Directory exists but metadata wasn't found - clean it up
                this._logger?.LogWarning("Instance directory exists but metadata not found. Cleaning up: {Directory}",
                    instanceDirectory);
                Directory.Delete(instanceDirectory, true);
            }
            
            Directory.CreateDirectory(instanceDirectory);
            this._logger?.LogInformation("Created instance directory: {Directory}", instanceDirectory);
            
            // Create temporary directory for download
            string tempDirectory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
            Directory.CreateDirectory(tempDirectory);
            
            try
            {
                // Download the asset
                this._logger?.LogInformation("Downloading asset: {AssetName}", asset.Name);
                string downloadedFilePath = await GitHubReleaseHelper.DownloadAssetAsync(
                    this._gitHubClient,
                    asset,
                    tempDirectory,
                    null,
                    cancellationToken).ConfigureAwait(false);
                
                this._logger?.LogInformation("Downloaded asset to: {Path}", downloadedFilePath);
                
                // Extract the archive
                this._logger?.LogInformation("Extracting archive to: {Directory}", instanceDirectory);
                await ArchiveHelper.ExtractAsync(downloadedFilePath, instanceDirectory, cancellationToken).ConfigureAwait(false);
                
                this._logger?.LogDebug("Extraction completed");
                
                // Find the actual Python installation directory (might be nested)
                string pythonInstallPath = FindPythonInstallPath(instanceDirectory);
                
                // Verify the extraction
                if (!ArchiveHelper.VerifyExtractedInstallation(pythonInstallPath))
                {
                    throw new PythonInstallationException(
                        $"Extracted Python installation verification failed for {pythonInstallPath}. " +
                        "Required files or directories not found in expected locations. " +
                        "The archive may be corrupted or incomplete.");
                }
                
                this._logger?.LogDebug("Extracted installation verified successfully");
                
                // Additional verification: Try to execute Python to ensure it actually works
                string pythonExePath = FindPythonExecutablePath(pythonInstallPath);
                if (!string.IsNullOrEmpty(pythonExePath))
                {
                    try
                    {
                        var testProcess = new System.Diagnostics.Process
                        {
                            StartInfo = new System.Diagnostics.ProcessStartInfo
                            {
                                FileName = pythonExePath,
                                Arguments = "--version",
                                RedirectStandardOutput = true,
                                RedirectStandardError = true,
                                UseShellExecute = false,
                                CreateNoWindow = true
                            }
                        };
                        testProcess.Start();
                        testProcess.WaitForExit(5000); // 5 second timeout
                        
                        if (testProcess.ExitCode == 0)
                        {
                            var versionOutput = testProcess.StandardOutput.ReadToEnd();
                            this._logger?.LogDebug("Python executable test successful: {Version}", versionOutput.Trim());
                        }
                        else
                        {
                            this._logger?.LogWarning(
                                "Python executable test failed with exit code {ExitCode}. " +
                                "The installation may still be valid but the Python executable could not be run.",
                                testProcess.ExitCode);
                        }
                    }
                    catch (Exception ex)
                    {
                        // Don't fail installation if version check fails - Python might work in different context
                        this._logger?.LogWarning(ex, 
                            "Could not verify Python executable works. " +
                            "The installation may still be valid.");
                    }
                }
                
                // Create and save instance metadata
                var instanceMetadata = new InstanceMetadata
                {
                    PythonVersion = normalizedVersion,
                    BuildDate = actualBuildDate,
                    WasLatestBuild = buildDate == null,
                    InstallationDate = DateTime.Now,
                    Directory = pythonInstallPath
                };
                
                instanceMetadata.Save(pythonInstallPath);
                this._logger?.LogInformation("Saved instance metadata to: {Path}", pythonInstallPath);
                
                // Add to metadata instances list
                this._metadata.Instances.Add(instanceMetadata);
                instance = instanceMetadata;
            }
            finally
            {
                // Clean up temporary directory and downloaded file
                try
                {
                    if (Directory.Exists(tempDirectory))
                    {
                        Directory.Delete(tempDirectory, true);
                        this._logger?.LogDebug("Cleaned up temporary directory: {Directory}", tempDirectory);
                    }
                }
                catch (Exception ex)
                {
                    this._logger?.LogWarning(ex, "Failed to clean up temporary directory: {Directory}", tempDirectory);
                }
            }
        }
        
        // use the instance 
        BasePythonRuntime runtime = GetPythonRuntimeForInstance(instance!);
        return runtime;
    }
    
    private async Task<string> ExtractBuildDateFromReleaseAsync(
        string pythonVersion,
        Octokit.ReleaseAsset asset,
        CancellationToken cancellationToken)
    {
        // Query releases to find which one contains this asset
        const string repositoryOwner = "astral-sh";
        const string repositoryName = "python-build-standalone";
        var releases = await this._gitHubClient.Repository.Release.GetAll(
            repositoryOwner,
            repositoryName).ConfigureAwait(false);
        
        // Find the release that contains this asset
        var release = releases.FirstOrDefault(r => 
            r.Assets.Any(a => a.Id == asset.Id || a.Name == asset.Name));
        
        if (release == null)
        {
            // Fallback: try to extract from asset name or use a default
            this._logger?.LogWarning(
                "Could not find release for asset {AssetName}. Using default build date.",
                asset.Name);
            return "unknown";
        }
        
        // Extract build date from release tag (typically YYYYMMDD format)
        var tag = release.TagName;
        var buildDateMatch = System.Text.RegularExpressions.Regex.Match(tag, @"(\d{8})");
        if (buildDateMatch.Success)
        {
            return buildDateMatch.Groups[1].Value;
        }
        
        // Try YYYY-MM-DD format
        buildDateMatch = System.Text.RegularExpressions.Regex.Match(tag, @"(\d{4}-\d{2}-\d{2})");
        if (buildDateMatch.Success)
        {
            return buildDateMatch.Groups[1].Value.Replace("-", "");
        }
        
        // Fallback to release tag itself
        this._logger?.LogWarning(
            "Could not extract build date from release tag {Tag}. Using tag as build date.",
            tag);
        return tag;
    }
    
    private string FindPythonInstallPath(string extractedDirectory)
    {
        // Python distributions from python-build-standalone typically extract to a subdirectory
        // Look for the Python executable to find the actual installation path
        
        var directories = Directory.GetDirectories(extractedDirectory);
        
        // Check if the extracted directory itself contains the Python executable
        if (ArchiveHelper.VerifyExtractedInstallation(extractedDirectory))
        {
            return extractedDirectory;
        }
        
        // Check subdirectories
        foreach (var subDir in directories)
        {
            if (ArchiveHelper.VerifyExtractedInstallation(subDir))
            {
                return subDir;
            }
            
            // Check nested subdirectories (some archives have multiple levels)
            var nestedDirs = Directory.GetDirectories(subDir);
            foreach (var nestedDir in nestedDirs)
            {
                if (ArchiveHelper.VerifyExtractedInstallation(nestedDir))
                {
                    return nestedDir;
                }
            }
        }
        
        // If we can't find it, return the extracted directory anyway
        // The verification should have caught this earlier, but return something
        this._logger?.LogWarning(
            "Could not find Python installation path in extracted directory. Using root: {Directory}",
            extractedDirectory);
        return extractedDirectory;
    }
    
    private string FindPythonExecutablePath(string pythonInstallPath)
    {
        if (System.Runtime.InteropServices.RuntimeInformation.IsOSPlatform(System.Runtime.InteropServices.OSPlatform.Windows))
        {
            var pythonExe = Path.Combine(pythonInstallPath, "python.exe");
            return File.Exists(pythonExe) ? pythonExe : string.Empty;
        }
        else
        {
            var pythonExe = Path.Combine(pythonInstallPath, "bin", "python3");
            return File.Exists(pythonExe) ? pythonExe : string.Empty;
        }
    }

    public abstract BasePythonRuntime GetPythonRuntimeForInstance(InstanceMetadata instanceMetadata);

    public BasePythonRuntime GetOrCreateInstance(
        string pythonVersion,
        string? buildDate = null)
    {
        Task<BasePythonRuntime> task = GetOrCreateInstanceAsync(pythonVersion, buildDate);
        task.Wait(cancellationToken: default);
        return task.Result;
    }

    public async Task<bool> DeleteInstanceAsync(
        string pythonVersion,
        string? buildDate = null,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(pythonVersion))
        {
            throw new ArgumentException("Python version cannot be null or empty.", nameof(pythonVersion));
        }
        
        this._logger?.LogInformation("Deleting Python instance: Version={Version}, BuildDate={BuildDate}", pythonVersion, buildDate);
        
        var instance = this._metadata.FindInstance(pythonVersion, buildDate);
        var result = this._metadata.RemoveInstance(pythonVersion, buildDate);
        if (result)
        {
            this._logger?.LogInformation("Deleted instance directory: {InstanceDirectory}", instance?.Directory ?? "");
        }
        else
        {
            this._logger?.LogWarning("Instance not found: Version={Version}, BuildDate={BuildDate}", pythonVersion, buildDate);
        }

        return result;
    }
    
    public bool DeleteInstance(
        string pythonVersion,
        string? buildDate = null)
    {
        Task<bool> task = DeleteInstanceAsync(pythonVersion, buildDate);
        task.Wait(cancellationToken: default);
        return task.Result;
    }
    
    public IReadOnlyList<InstanceMetadata> ListInstances()
    {
        var instances = this._metadata.Instances.AsReadOnly();
        return instances;
    }

    public async Task<IReadOnlyList<string>> ListAvailableVersionsAsync(
        string? releaseTag = null,
        CancellationToken cancellationToken = default)
    {
        string releaseTagString = releaseTag != null ? $" (release: {releaseTag})" : "";
        this._logger?.LogInformation($"Listing available Python versions from GitHub{releaseTagString}");

        // Check cache first
        string cacheKey = $"python_versions_{releaseTag ?? "all"}";
        if (_cache?.TryGetValue<IReadOnlyList<string>>(cacheKey, out var cachedVersions) == true)
        {
            this._logger?.LogDebug("Retrieved Python versions from cache");
            return cachedVersions;
        }

        try
        {
            var versions = await GitHubReleaseHelper.ListAvailableVersionsAsync(
                this._gitHubClient,
                releaseTag,
                cancellationToken).ConfigureAwait(false);

            // Cache the results for 1 hour
            _cache?.Set(cacheKey, versions, TimeSpan.FromHours(1));

            this._logger?.LogInformation("Found {Count} available Python versions", versions.Count);
            return versions;
        }
        catch (Exception ex)
        {
            this._logger?.LogError(ex, "Failed to list available Python versions");
            throw;
        }
    }
    
    public IReadOnlyList<string> ListAvailableVersions(string? releaseTag = null)
    {
        Task<IReadOnlyList<string>> task = ListAvailableVersionsAsync(releaseTag);
        task.Wait(cancellationToken: default);
        return task.Result;
    }

    /// <summary>
    /// Gets detailed information about a Python instance.
    /// </summary>
    /// <param name="pythonVersion">The Python version.</param>
    /// <param name="buildDate">The build date (optional, uses latest if not specified).</param>
    /// <returns>Instance metadata if found, null otherwise.</returns>
    public InstanceMetadata? GetInstanceInfo(string pythonVersion, string? buildDate = null)
    {
        if (string.IsNullOrWhiteSpace(pythonVersion))
            throw new ArgumentException("Python version cannot be null or empty.", nameof(pythonVersion));

        var normalizedVersion = VersionParser.NormalizeVersion(pythonVersion);
        return this._metadata.FindInstance(normalizedVersion, buildDate);
    }

    /// <summary>
    /// Gets the disk usage of a Python instance in bytes.
    /// </summary>
    /// <param name="pythonVersion">The Python version.</param>
    /// <param name="buildDate">The build date (optional, uses latest if not specified).</param>
    /// <returns>The size in bytes, or 0 if the instance is not found.</returns>
    public long GetInstanceSize(string pythonVersion, string? buildDate = null)
    {
        var instance = GetInstanceInfo(pythonVersion, buildDate);
        if (instance == null || string.IsNullOrWhiteSpace(instance.Directory))
            return 0;

        return CalculateDirectorySize(instance.Directory);
    }

    /// <summary>
    /// Gets the total disk usage across all Python instances managed by this manager in bytes.
    /// </summary>
    /// <returns>The total size in bytes.</returns>
    public long GetTotalDiskUsage()
    {
        long totalSize = 0;

        foreach (var instance in this._metadata.Instances)
        {
            if (!string.IsNullOrWhiteSpace(instance.Directory) && Directory.Exists(instance.Directory))
            {
                totalSize += CalculateDirectorySize(instance.Directory);
            }
        }

        return totalSize;
    }

    /// <summary>
    /// Validates the integrity of a Python instance by checking if required files exist.
    /// </summary>
    /// <param name="pythonVersion">The Python version.</param>
    /// <param name="buildDate">The build date (optional, uses latest if not specified).</param>
    /// <returns>True if the instance appears valid, false otherwise.</returns>
    public bool ValidateInstanceIntegrity(string pythonVersion, string? buildDate = null)
    {
        var instance = GetInstanceInfo(pythonVersion, buildDate);
        if (instance == null || string.IsNullOrWhiteSpace(instance.Directory))
            return false;

        return ArchiveHelper.VerifyExtractedInstallation(instance.Directory);
    }

    /// <summary>
    /// Gets the latest available Python version from GitHub releases.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The latest Python version string, or null if not found.</returns>
    public async Task<string?> GetLatestPythonVersionAsync(CancellationToken cancellationToken = default)
    {
        var versions = await ListAvailableVersionsAsync(null, cancellationToken).ConfigureAwait(false);
        return versions.FirstOrDefault();
    }

    /// <summary>
    /// Gets the latest available Python version (synchronous version).
    /// </summary>
    public string? GetLatestPythonVersion()
    {
        Task<string?> task = GetLatestPythonVersionAsync();
        task.Wait();
        return task.Result;
    }

    /// <summary>
    /// Finds the best matching Python version from available versions.
    /// </summary>
    /// <param name="versionSpec">The version specification (e.g., "3.12", "3.12.0", ">=3.11").</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The best matching version, or null if none found.</returns>
    public async Task<string?> FindBestMatchingVersionAsync(string versionSpec, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(versionSpec))
            throw new ArgumentException("Version specification cannot be null or empty.", nameof(versionSpec));

        var availableVersions = await ListAvailableVersionsAsync(null, cancellationToken).ConfigureAwait(false);
        
        // Simple version matching - look for exact or prefix match
        var normalizedSpec = VersionParser.NormalizeVersion(versionSpec);
        var matchingVersions = availableVersions
            .Where(v => v.StartsWith(normalizedSpec, StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(v => v)
            .ToList();

        return matchingVersions.FirstOrDefault();
    }

    /// <summary>
    /// Finds the best matching Python version (synchronous version).
    /// </summary>
    public string? FindBestMatchingVersion(string versionSpec)
    {
        Task<string?> task = FindBestMatchingVersionAsync(versionSpec);
        task.Wait();
        return task.Result;
    }

    /// <summary>
    /// Ensures that a specific Python version is available, downloading it if necessary.
    /// </summary>
    /// <param name="pythonVersion">The Python version to ensure.</param>
    /// <param name="buildDate">The build date (optional, uses latest if not specified).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The Python runtime instance.</returns>
    public async Task<BasePythonRuntime> EnsurePythonVersionAsync(
        string pythonVersion,
        string? buildDate = null,
        CancellationToken cancellationToken = default)
    {
        return await GetOrCreateInstanceAsync(pythonVersion, buildDate, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Ensures that a specific Python version is available (synchronous version).
    /// </summary>
    public BasePythonRuntime EnsurePythonVersion(string pythonVersion, string? buildDate = null)
    {
        Task<BasePythonRuntime> task = EnsurePythonVersionAsync(pythonVersion, buildDate);
        task.Wait();
        return task.Result;
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
}
