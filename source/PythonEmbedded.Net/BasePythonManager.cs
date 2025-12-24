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
        DateTime? buildDate = null,
        CancellationToken cancellationToken = default)
    {
        // Use default version from configuration if not specified
        if (string.IsNullOrWhiteSpace(pythonVersion))
        {
            pythonVersion = this._configuration.DefaultPythonVersion ?? "3.12";
            this._logger?.LogDebug("Using default Python version from configuration: {Version}", pythonVersion);
        }
        
        this._logger?.LogInformation("Getting or creating Python instance: Version={Version}, BuildDate={BuildDate}",
            pythonVersion, buildDate?.ToString("yyyy-MM-dd") ?? "latest");
        
        // Search for existing instance using original version format (preserves partial vs exact matching)
        var instance = this._metadata.FindInstance(pythonVersion, buildDate);
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
            
            // Find the release asset (pass original version - GitHubReleaseHelper handles partial vs exact)
            this._logger?.LogInformation("Finding release asset for Python {Version} on platform {Platform}",
                pythonVersion, platform.TargetTriple);
            var (release, asset) = await GitHubReleaseHelper.FindReleaseAssetAsync(
                this._gitHubClient,
                pythonVersion,
                buildDate,
                platform,
                cancellationToken).ConfigureAwait(false);
            
            if (asset == null)
            {
                throw new InstanceNotFoundException(
                    $"No release asset found for Python {pythonVersion} and build date {buildDate?.ToString("yyyy-MM-dd") ?? "latest"}")
                {
                    PythonVersion = pythonVersion,
                    BuildDate = buildDate
                };
            }
            
            // Extract build date from release if not provided
            DateTime actualBuildDate = buildDate ?? ExtractBuildDateFromTag(release.TagName);
            
            // Extract the actual version from the asset (will be full Major.Minor.Patch)
            // This ensures we store the exact version that was downloaded
            var actualVersion = GitHubReleaseHelper.ExtractVersionFromAssetName(asset.Name);
            
            // Create instance directory using the actual (normalized) version
            string instanceDirectoryName = $"python-{actualVersion}-{actualBuildDate:yyyyMMdd}";
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
                // Use actualVersion (extracted from asset) to store the exact version that was downloaded
                var instanceMetadata = new InstanceMetadata
                {
                    PythonVersion = actualVersion,
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
    
    private static DateTime ExtractBuildDateFromTag(string tag)
    {
        // Extract build date from release tag (typically YYYYMMDD format)
        var buildDateMatch = System.Text.RegularExpressions.Regex.Match(tag, @"(\d{8})");
        if (buildDateMatch.Success)
        {
            var dateStr = buildDateMatch.Groups[1].Value;
            if (DateTime.TryParseExact(dateStr, "yyyyMMdd", null, System.Globalization.DateTimeStyles.None, out var date))
            {
                return date;
            }
        }
        
        // Try YYYY-MM-DD format
        buildDateMatch = System.Text.RegularExpressions.Regex.Match(tag, @"(\d{4}-\d{2}-\d{2})");
        if (buildDateMatch.Success)
        {
            var dateStr = buildDateMatch.Groups[1].Value;
            if (DateTime.TryParseExact(dateStr, "yyyy-MM-dd", null, System.Globalization.DateTimeStyles.None, out var date))
            {
                return date;
            }
        }
        
        // If we can't parse it, return current date
        return DateTime.Now;
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
        DateTime? buildDate = null)
    {
        Task<BasePythonRuntime> task = GetOrCreateInstanceAsync(pythonVersion, buildDate);
        task.Wait(cancellationToken: default);
        return task.Result;
    }

    public async Task<bool> DeleteInstanceAsync(
        string pythonVersion,
        DateTime? buildDate = null,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(pythonVersion))
        {
            throw new ArgumentException("Python version cannot be null or empty.", nameof(pythonVersion));
        }
        
        this._logger?.LogInformation("Deleting Python instance: Version={Version}, BuildDate={BuildDate}", pythonVersion, buildDate?.ToString("yyyy-MM-dd") ?? "latest");
        
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
        DateTime? buildDate = null)
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
    public InstanceMetadata? GetInstanceInfo(string pythonVersion, DateTime? buildDate = null)
    {
        if (string.IsNullOrWhiteSpace(pythonVersion))
            throw new ArgumentException("Python version cannot be null or empty.", nameof(pythonVersion));

        // Use original version format to preserve partial vs exact matching behavior
        return this._metadata.FindInstance(pythonVersion, buildDate);
    }

    /// <summary>
    /// Gets the disk usage of a Python instance in bytes.
    /// </summary>
    /// <param name="pythonVersion">The Python version.</param>
    /// <param name="buildDate">The build date (optional, uses latest if not specified).</param>
    /// <returns>The size in bytes, or 0 if the instance is not found.</returns>
    public long GetInstanceSize(string pythonVersion, DateTime? buildDate = null)
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
    public bool ValidateInstanceIntegrity(string pythonVersion, DateTime? buildDate = null)
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
        DateTime? buildDate = null,
        CancellationToken cancellationToken = default)
    {
        return await GetOrCreateInstanceAsync(pythonVersion, buildDate, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Ensures that a specific Python version is available (synchronous version).
    /// </summary>
    public BasePythonRuntime EnsurePythonVersion(string pythonVersion, DateTime? buildDate = null)
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

    /// <summary>
    /// Checks if there is sufficient disk space available.
    /// </summary>
    /// <param name="requiredBytes">The required disk space in bytes.</param>
    /// <returns>True if sufficient disk space is available, false otherwise.</returns>
    public bool CheckDiskSpace(long requiredBytes)
    {
        try
        {
            var driveInfo = new DriveInfo(_rootDirectory);
            var availableBytes = driveInfo.AvailableFreeSpace;
            return availableBytes >= requiredBytes;
        }
        catch (Exception ex)
        {
            this._logger?.LogWarning(ex, "Failed to check disk space");
            // Return true if we can't determine - fail later rather than blocking
            return true;
        }
    }

    /// <summary>
    /// Tests network connectivity to the GitHub API.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>True if connectivity test succeeds, false otherwise.</returns>
    public async Task<bool> TestNetworkConnectivityAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            using var httpClient = new System.Net.Http.HttpClient();
            httpClient.Timeout = TimeSpan.FromSeconds(10);
            
            var response = await httpClient.GetAsync("https://api.github.com", cancellationToken).ConfigureAwait(false);
            return response.IsSuccessStatusCode;
        }
        catch (Exception ex)
        {
            this._logger?.LogWarning(ex, "Network connectivity test failed");
            return false;
        }
    }

    /// <summary>
    /// Tests network connectivity (synchronous version).
    /// </summary>
    public bool TestNetworkConnectivity()
    {
        Task<bool> task = TestNetworkConnectivityAsync();
        task.Wait();
        return task.Result;
    }

    /// <summary>
    /// Gets system requirements information.
    /// </summary>
    /// <returns>A dictionary containing system requirements check results.</returns>
    public Dictionary<string, object> GetSystemRequirements()
    {
        var results = new Dictionary<string, object>();
        var platform = PlatformInfo.Detect();

        results["Platform"] = platform.OperatingSystem ?? "Unknown";
        results["Architecture"] = platform.Architecture ?? "Unknown";
        results["TargetTriple"] = platform.TargetTriple ?? "Unknown";

        // Check OS version requirements
        try
        {
            platform.ValidateMinimumOsVersion();
            results["OsVersionCheck"] = "Passed";
        }
        catch (Exception ex)
        {
            results["OsVersionCheck"] = "Failed";
            results["OsVersionError"] = ex.Message;
        }

        // Check disk space (sample check for 100MB)
        var sampleRequiredBytes = 100L * 1024 * 1024; // 100 MB
        results["DiskSpaceCheck"] = CheckDiskSpace(sampleRequiredBytes) ? "Sufficient" : "Insufficient";

        return results;
    }

    /// <summary>
    /// Runs diagnostics and returns issues found.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A list of diagnostic issues found.</returns>
    public async Task<IReadOnlyList<string>> DiagnoseIssuesAsync(CancellationToken cancellationToken = default)
    {
        var issues = new List<string>();

        // Check system requirements
        var systemReqs = GetSystemRequirements();
        if (systemReqs.TryGetValue("OsVersionCheck", out var osCheck) && osCheck is string osCheckStr && osCheckStr == "Failed")
        {
            issues.Add($"OS version check failed: {systemReqs.GetValueOrDefault("OsVersionError", "Unknown error")}");
        }

        // Check network connectivity
        var networkOk = await TestNetworkConnectivityAsync(cancellationToken).ConfigureAwait(false);
        if (!networkOk)
        {
            issues.Add("Network connectivity test failed - cannot reach GitHub API");
        }

        // Check disk space for each instance
        foreach (var instance in _metadata.Instances)
        {
            if (!string.IsNullOrWhiteSpace(instance.Directory) && Directory.Exists(instance.Directory))
            {
                var size = GetInstanceSize(instance.PythonVersion, instance.BuildDate);
                var requiredBytes = size + (100L * 1024 * 1024); // Add 100MB buffer
                if (!CheckDiskSpace(requiredBytes))
                {
                    issues.Add($"Insufficient disk space for instance {instance.PythonVersion} (required: {requiredBytes / (1024 * 1024)} MB)");
                }

                // Validate instance integrity
                if (!ValidateInstanceIntegrity(instance.PythonVersion, instance.BuildDate))
                {
                    issues.Add($"Instance {instance.PythonVersion} failed integrity check");
                }
            }
        }

        return issues.AsReadOnly();
    }

    /// <summary>
    /// Runs diagnostics (synchronous version).
    /// </summary>
    public IReadOnlyList<string> DiagnoseIssues()
    {
        Task<IReadOnlyList<string>> task = DiagnoseIssuesAsync();
        task.Wait();
        return task.Result;
    }

    /// <summary>
    /// Exports a Python instance to an archive file.
    /// </summary>
    /// <param name="pythonVersion">The Python version of the instance to export.</param>
    /// <param name="outputPath">The path where to save the archive file.</param>
    /// <param name="buildDate">The build date (optional, uses latest if not specified).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The path to the created archive file.</returns>
    public async Task<string> ExportInstanceAsync(
        string pythonVersion,
        string outputPath,
        DateTime? buildDate = null,
        CancellationToken cancellationToken = default)
    {
        var instance = GetInstanceInfo(pythonVersion, buildDate);
        if (instance == null || string.IsNullOrWhiteSpace(instance.Directory))
            throw new InstanceNotFoundException($"Instance not found: {pythonVersion}");

        if (!Directory.Exists(instance.Directory))
            throw new DirectoryNotFoundException($"Instance directory not found: {instance.Directory}");

        this._logger?.LogInformation("Exporting instance {Version} to {Path}", pythonVersion, outputPath);

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
            System.IO.Compression.ZipFile.CreateFromDirectory(instance.Directory, outputPath);
        }
        else
        {
            // Default to zip if no extension or unsupported format
            var zipPath = string.IsNullOrEmpty(Path.GetExtension(outputPath)) ? outputPath + ".zip" : outputPath;
            System.IO.Compression.ZipFile.CreateFromDirectory(instance.Directory, zipPath);
            outputPath = zipPath;
        }

        this._logger?.LogInformation("Successfully exported instance {Version} to {Path}", pythonVersion, outputPath);
        return outputPath;
    }

    /// <summary>
    /// Imports a Python instance from an archive file.
    /// </summary>
    /// <param name="archivePath">The path to the archive file to import.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The imported instance metadata.</returns>
    public async Task<InstanceMetadata> ImportInstanceAsync(
        string archivePath,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(archivePath))
            throw new ArgumentException("Archive path cannot be null or empty.", nameof(archivePath));
        if (!File.Exists(archivePath))
            throw new FileNotFoundException($"Archive file not found: {archivePath}", archivePath);

        this._logger?.LogInformation("Importing instance from {Path}", archivePath);

        // Extract to a temporary directory first
        var tempDir = Path.Combine(_rootDirectory, "temp_import_" + Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(tempDir);

        try
        {
            // Extract archive
            var extension = Path.GetExtension(archivePath).ToLowerInvariant();
            if (extension == ".zip")
            {
                System.IO.Compression.ZipFile.ExtractToDirectory(archivePath, tempDir);
            }
            else
            {
                await ArchiveHelper.ExtractAsync(archivePath, tempDir, cancellationToken).ConfigureAwait(false);
            }

            // Find Python installation path in extracted directory
            var pythonInstallPath = FindPythonInstallPath(tempDir);

            // Try to load instance metadata
            InstanceMetadata? importedMetadata = null;
            var metadataFile = Path.Combine(pythonInstallPath, "instance_metadata.json");
            if (File.Exists(metadataFile))
            {
                importedMetadata = InstanceMetadata.Load(pythonInstallPath);
            }

            if (importedMetadata == null)
            {
                // Try to extract version from Python executable
                var pythonExePath = FindPythonExecutablePath(pythonInstallPath);
                if (string.IsNullOrEmpty(pythonExePath))
                {
                    throw new PythonInstallationException("Could not find Python executable in imported archive");
                }

                // Create a temporary runtime to get version info
                var tempMetadata = new InstanceMetadata
                {
                    PythonVersion = "unknown",
                    BuildDate = DateTime.Now,
                    Directory = pythonInstallPath ?? tempDir
                };
                var tempRuntime = GetPythonRuntimeForInstance(tempMetadata);
                var versionInfo = tempRuntime.GetPythonVersionInfo();

                // Parse version from output
                var versionMatch = System.Text.RegularExpressions.Regex.Match(versionInfo, @"(\d+\.\d+\.\d+)");
                var detectedVersion = versionMatch.Success ? versionMatch.Groups[1].Value : "unknown";

                importedMetadata = new InstanceMetadata
                {
                    PythonVersion = detectedVersion,
                    BuildDate = DateTime.Now,
                    Directory = pythonInstallPath ?? tempDir,
                    InstallationDate = DateTime.Now,
                    WasLatestBuild = false
                };
            }

            // Move to final location
            var finalPath = Path.Combine(_rootDirectory, $"python-{importedMetadata.PythonVersion}-{importedMetadata.BuildDate:yyyyMMdd}");
            if (Directory.Exists(finalPath))
            {
                throw new InvalidOperationException($"Instance already exists at: {finalPath}");
            }

            Directory.Move(pythonInstallPath, finalPath);
            importedMetadata.Directory = finalPath;
            importedMetadata.Save(finalPath);

            // Add to metadata
            _metadata.Instances.Add(importedMetadata);

            this._logger?.LogInformation("Successfully imported instance {Version} from {Path}", importedMetadata.PythonVersion, archivePath);
            return importedMetadata;
        }
        finally
        {
            // Clean up temporary directory
            if (Directory.Exists(tempDir))
            {
                try
                {
                    Directory.Delete(tempDir, true);
                }
                catch
                {
                    // Ignore cleanup errors
                }
            }
        }
    }
}
