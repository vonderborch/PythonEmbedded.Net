# API Reference

This document provides detailed API reference for PythonEmbedded.Net.

## Table of Contents

- [Abstract Classes](#abstract-classes)
  - [BasePythonManager](#basepythonmanager)
  - [BasePythonRuntime](#basepythonruntime)
  - [BasePythonRootRuntime](#basepythonrootruntime)
- [Concrete Classes](#concrete-classes)
  - [PythonManager](#pythonmanager)
  - [PythonNetManager](#pythonnetmanager)
  - [PythonExecutionResult](#pythonexecutionresult)
  - [InstanceMetadata](#instancemetadata)
  - [ManagerConfiguration](#managerconfiguration)
- [Models](#models)
  - [PackageInfo](#packageinfo)
  - [PyPIPackageInfo](#pypipackageinfo)
- [Exceptions](#exceptions)

## Abstract Classes

### BasePythonManager

Abstract base class for managing Python installations. Provides functionality for downloading, installing, and managing Python instances.

#### Properties

##### Configuration

```csharp
ManagerConfiguration Configuration { get; }
```

Gets the manager configuration.

#### Methods

##### GetOrCreateInstanceAsync

```csharp
Task<BasePythonRuntime> GetOrCreateInstanceAsync(
    string? pythonVersion = null,
    string? buildDate = null,
    CancellationToken cancellationToken = default)
```

Gets or creates a Python runtime instance for the specified version. Downloads and installs the Python distribution if it doesn't exist. If `pythonVersion` is null, uses the default version from configuration.

**Parameters:**
- `pythonVersion`: The Python version (e.g., "3.12.0", "3.11"). If null, uses `Configuration.DefaultPythonVersion`.
- `buildDate`: Optional build date in YYYYMMDD format. If null, uses the latest build.
- `cancellationToken`: Cancellation token.

**Returns:** A task that represents the asynchronous operation. The result is a `BasePythonRuntime` instance.

**Exceptions:**
- `ArgumentException`: If `pythonVersion` is invalid.
- `InstanceNotFoundException`: If the specified version/build date combination cannot be found.

##### GetOrCreateInstance

```csharp
BasePythonRuntime GetOrCreateInstance(
    string? pythonVersion = null,
    string? buildDate = null)
```

Synchronous version of `GetOrCreateInstanceAsync`.

##### DeleteInstanceAsync

```csharp
Task<bool> DeleteInstanceAsync(
    string pythonVersion,
    string? buildDate = null,
    CancellationToken cancellationToken = default)
```

Deletes a Python instance.

**Returns:** `true` if the instance was deleted, `false` if it wasn't found.

##### ListInstances

```csharp
IReadOnlyList<InstanceMetadata> ListInstances()
```

Lists all Python instances managed by this manager.

**Returns:** A read-only list of `InstanceMetadata` objects.

##### ListAvailableVersionsAsync

```csharp
Task<IReadOnlyList<string>> ListAvailableVersionsAsync(
    string? releaseTag = null,
    CancellationToken cancellationToken = default)
```

Lists available Python versions from GitHub releases.

**Parameters:**
- `releaseTag`: Optional release tag to filter versions.
- `cancellationToken`: Cancellation token.

**Returns:** A task that represents the asynchronous operation. The result is a list of version strings.

##### GetInstanceInfo

```csharp
InstanceMetadata? GetInstanceInfo(string pythonVersion, string? buildDate = null)
```

Gets detailed information about a Python instance.

##### GetInstanceSize

```csharp
long GetInstanceSize(string pythonVersion, string? buildDate = null)
```

Gets the disk size of a Python instance in bytes.

##### GetTotalDiskUsage

```csharp
long GetTotalDiskUsage()
```

Gets the total disk usage across all managed instances in bytes.

##### ValidateInstanceIntegrity

```csharp
bool ValidateInstanceIntegrity(string pythonVersion, string? buildDate = null)
```

Validates the integrity of a Python instance.

##### GetLatestPythonVersionAsync

```csharp
Task<string?> GetLatestPythonVersionAsync(CancellationToken cancellationToken = default)
```

Gets the latest available Python version.

##### FindBestMatchingVersionAsync

```csharp
Task<string?> FindBestMatchingVersionAsync(string versionSpec, CancellationToken cancellationToken = default)
```

Finds the best matching Python version from available versions.

##### EnsurePythonVersionAsync

```csharp
Task<BasePythonRuntime> EnsurePythonVersionAsync(
    string pythonVersion,
    string? buildDate = null,
    CancellationToken cancellationToken = default)
```

Ensures that a specific Python version is available, downloading it if necessary.

##### CheckDiskSpace

```csharp
bool CheckDiskSpace(long requiredBytes)
```

Checks if there is sufficient disk space available.

##### TestNetworkConnectivityAsync

```csharp
Task<bool> TestNetworkConnectivityAsync(CancellationToken cancellationToken = default)
```

Tests network connectivity to the GitHub API.

##### GetSystemRequirements

```csharp
Dictionary<string, object> GetSystemRequirements()
```

Gets system requirements information.

##### DiagnoseIssuesAsync

```csharp
Task<IReadOnlyList<string>> DiagnoseIssuesAsync(CancellationToken cancellationToken = default)
```

Runs diagnostics and returns issues found.

##### ExportInstanceAsync

```csharp
Task<string> ExportInstanceAsync(
    string pythonVersion,
    string outputPath,
    string? buildDate = null,
    CancellationToken cancellationToken = default)
```

Exports a Python instance to an archive file.

##### ImportInstanceAsync

```csharp
Task<InstanceMetadata> ImportInstanceAsync(
    string archivePath,
    CancellationToken cancellationToken = default)
```

Imports a Python instance from an archive file.

---

### BasePythonRuntime

Abstract base class for Python runtime implementations, providing functionality for executing commands, scripts, and managing packages.

#### Methods

##### ExecuteCommandAsync

```csharp
Task<PythonExecutionResult> ExecuteCommandAsync(
    string command,
    Func<string?>? stdinHandler = null,
    Action<string>? stdoutHandler = null,
    Action<string>? stderrHandler = null,
    CancellationToken cancellationToken = default,
    string? workingDirectory = null,
    Dictionary<string, string>? environmentVariables = null,
    ProcessPriorityClass? priority = null,
    int? maxMemoryMB = null,
    TimeSpan? timeout = null)
```

Executes a Python command and returns the result.

**Parameters:**
- `command`: The Python command to execute (e.g., "print('Hello')")
- `stdinHandler`: Optional handler for providing stdin input line by line. Return null to end input.
- `stdoutHandler`: Optional handler for processing stdout output line by line.
- `stderrHandler`: Optional handler for processing stderr output line by line.
- `cancellationToken`: Cancellation token.
- `workingDirectory`: Optional working directory override.
- `environmentVariables`: Optional environment variables to set for the execution.
- `priority`: Optional process priority.
- `maxMemoryMB`: Optional maximum memory limit in MB (note: requires process access which is abstracted).
- `timeout`: Optional per-execution timeout (in addition to CancellationToken).

**Returns:** A task that represents the asynchronous operation. The result contains exit code, stdout, and stderr.

##### ExecuteScriptAsync

```csharp
Task<PythonExecutionResult> ExecuteScriptAsync(
    string scriptPath,
    IEnumerable<string>? arguments = null,
    Func<string?>? stdinHandler = null,
    Action<string>? stdoutHandler = null,
    Action<string>? stderrHandler = null,
    CancellationToken cancellationToken = default,
    string? workingDirectory = null,
    Dictionary<string, string>? environmentVariables = null,
    ProcessPriorityClass? priority = null,
    int? maxMemoryMB = null,
    TimeSpan? timeout = null)
```

Executes a Python script file and returns the result.

##### InstallPackageAsync

```csharp
Task<PythonExecutionResult> InstallPackageAsync(
    string packageSpecification,
    bool upgrade = false,
    string? indexUrl = null,
    CancellationToken cancellationToken = default)
```

Installs a Python package using pip.

##### ListInstalledPackagesAsync

```csharp
Task<IReadOnlyList<PackageInfo>> ListInstalledPackagesAsync(CancellationToken cancellationToken = default)
```

Lists all installed packages.

##### GetPackageVersionAsync

```csharp
Task<string?> GetPackageVersionAsync(string packageName, CancellationToken cancellationToken = default)
```

Gets the version of a specific package.

##### IsPackageInstalledAsync

```csharp
Task<bool> IsPackageInstalledAsync(string packageName, CancellationToken cancellationToken = default)
```

Checks if a package is installed.

##### GetPackageInfoAsync

```csharp
Task<PackageInfo?> GetPackageInfoAsync(string packageName, CancellationToken cancellationToken = default)
```

Gets detailed information about an installed package.

##### UninstallPackageAsync

```csharp
Task<PythonExecutionResult> UninstallPackageAsync(
    string packageName,
    bool removeDependencies = false,
    CancellationToken cancellationToken = default)
```

Uninstalls a Python package.

##### UpgradeAllPackagesAsync

```csharp
Task<PythonExecutionResult> UpgradeAllPackagesAsync(CancellationToken cancellationToken = default)
```

Upgrades all installed packages.

##### ListOutdatedPackagesAsync

```csharp
Task<IReadOnlyList<PackageInfo>> ListOutdatedPackagesAsync(CancellationToken cancellationToken = default)
```

Lists packages with available updates.

##### DowngradePackageAsync

```csharp
Task<PythonExecutionResult> DowngradePackageAsync(
    string packageName,
    string targetVersion,
    CancellationToken cancellationToken = default)
```

Downgrades a package to a specific version.

##### ExportRequirementsFreezeAsync

```csharp
Task<string> ExportRequirementsFreezeAsync(string outputPath, CancellationToken cancellationToken = default)
```

Exports installed packages to a requirements.txt file with exact versions (pip freeze).

##### ExportRequirementsFreezeToStringAsync

```csharp
Task<string> ExportRequirementsFreezeToStringAsync(CancellationToken cancellationToken = default)
```

Gets pip freeze output as a string.

##### InstallPackagesAsync

```csharp
Task<Dictionary<string, PythonExecutionResult>> InstallPackagesAsync(
    IEnumerable<string> packages,
    bool parallel = false,
    CancellationToken cancellationToken = default)
```

Installs multiple packages.

##### UninstallPackagesAsync

```csharp
Task<Dictionary<string, PythonExecutionResult>> UninstallPackagesAsync(
    IEnumerable<string> packages,
    bool parallel = false,
    CancellationToken cancellationToken = default)
```

Uninstalls multiple packages.

##### GetPipVersionAsync

```csharp
Task<string> GetPipVersionAsync(CancellationToken cancellationToken = default)
```

Gets the pip version.

##### GetPythonVersionInfoAsync

```csharp
Task<string> GetPythonVersionInfoAsync(CancellationToken cancellationToken = default)
```

Gets detailed Python version information.

##### SearchPackagesAsync

```csharp
Task<IReadOnlyList<PyPISearchResult>> SearchPackagesAsync(
    string query,
    CancellationToken cancellationToken = default)
```

Searches PyPI for packages matching the query.

##### GetPackageMetadataAsync

```csharp
Task<PyPIPackageInfo?> GetPackageMetadataAsync(
    string packageName,
    string? version = null,
    CancellationToken cancellationToken = default)
```

Gets package metadata from PyPI.

##### ValidatePythonInstallationAsync

```csharp
Task<Dictionary<string, object>> ValidatePythonInstallationAsync(CancellationToken cancellationToken = default)
```

Performs a comprehensive health check of the Python installation.

**Returns:** A dictionary containing health check results including:
- `ExecutableExists`: Whether the Python executable exists
- `WorkingDirectoryExists`: Whether the working directory exists
- `PythonVersionCheck`: Status of Python version check
- `PipCheck`: Status of pip check
- `CommandExecution`: Status of command execution test
- `OverallHealth`: Overall health status ("Healthy" or "Unhealthy")

---

### BasePythonRootRuntime

Abstract base class for Python root runtime implementations that can manage virtual environments. Extends `BasePythonRuntime`.

#### Methods

##### GetOrCreateVirtualEnvironmentAsync

```csharp
Task<BasePythonVirtualRuntime> GetOrCreateVirtualEnvironmentAsync(
    string name,
    bool recreateIfExists = false,
    CancellationToken cancellationToken = default)
```

Gets or creates a virtual environment with the specified name.

##### DeleteVirtualEnvironmentAsync

```csharp
Task<bool> DeleteVirtualEnvironmentAsync(string name, CancellationToken cancellationToken = default)
```

Deletes a virtual environment with the specified name.

##### ListVirtualEnvironments

```csharp
IReadOnlyList<string> ListVirtualEnvironments()
```

Lists all virtual environments.

##### GetVirtualEnvironmentSizeAsync

```csharp
Task<long> GetVirtualEnvironmentSizeAsync(string name, CancellationToken cancellationToken = default)
```

Gets the disk size of a virtual environment in bytes.

##### GetVirtualEnvironmentInfoAsync

```csharp
Task<Dictionary<string, object>> GetVirtualEnvironmentInfoAsync(
    string name,
    CancellationToken cancellationToken = default)
```

Gets detailed information about a virtual environment.

##### CloneVirtualEnvironmentAsync

```csharp
Task<BasePythonVirtualRuntime> CloneVirtualEnvironmentAsync(
    string sourceName,
    string targetName,
    CancellationToken cancellationToken = default)
```

Clones a virtual environment to create a new one with the same packages and configuration.

##### ExportVirtualEnvironmentAsync

```csharp
Task<string> ExportVirtualEnvironmentAsync(
    string name,
    string outputPath,
    CancellationToken cancellationToken = default)
```

Exports a virtual environment to an archive file.

##### ImportVirtualEnvironmentAsync

```csharp
Task<BasePythonVirtualRuntime> ImportVirtualEnvironmentAsync(
    string name,
    string archivePath,
    CancellationToken cancellationToken = default)
```

Imports a virtual environment from an archive file.

---

## Concrete Classes

### PythonManager

Concrete implementation of `BasePythonManager` for subprocess-based execution.

#### Constructor

```csharp
public PythonManager(
    string directory,
    GitHubClient githubClient,
    ILogger<PythonManager>? logger = null,
    IMemoryCache? cache = null,
    ManagerConfiguration? configuration = null)
```

### PythonNetManager

Concrete implementation of `BasePythonManager` for Python.NET-based execution.

#### Constructor

```csharp
public PythonNetManager(
    string directory,
    GitHubClient githubClient,
    ILogger<PythonNetManager>? logger = null,
    IMemoryCache? cache = null,
    ManagerConfiguration? configuration = null)
```

---

## Models

### ManagerConfiguration

Configuration settings for the Python manager.

**Properties:**
- `DefaultPythonVersion` (string): Default Python version to use when not specified. Defaults to "3.12".
- `DefaultPipIndexUrl` (string): Default PyPI index URL. Defaults to "https://pypi.org/simple/".
- `ProxyUrl` (string?): Proxy URL for network operations.
- `DefaultTimeout` (TimeSpan): Default timeout for Python process executions. Defaults to 5 minutes.
- `RetryAttempts` (int): Number of retry attempts for transient network operations. Defaults to 3.
- `RetryDelay` (TimeSpan): Delay between retry attempts. Defaults to 1 second.
- `UseExponentialBackoff` (bool): Whether to use exponential backoff for retry delays. Defaults to true.

### PackageInfo

Represents information about an installed Python package.

**Properties:**
- `Name` (string): Package name
- `Version` (string): Package version
- `Summary` (string?): Package summary
- `Location` (string?): Installation location

### PyPIPackageInfo

Represents package metadata from PyPI.

### PyPISearchResult

Represents a simplified search result from PyPI.

**Properties:**
- `Name` (string): Package name
- `Version` (string): Package version
- `Summary` (string): Package summary

---

## Exceptions

- `PythonInstallationException`: Base exception for installation-related errors
- `InstanceNotFoundException`: Thrown when a Python instance is not found
- `PythonExecutionException`: Thrown when Python execution fails
- `PackageInstallationException`: Thrown when package installation fails
- `PythonNotInstalledException`: Thrown when Python installation is missing or invalid
- `PlatformNotSupportedException`: Thrown when the platform is not supported

