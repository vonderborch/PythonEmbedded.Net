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
  - [VirtualEnvironmentMetadata](#virtualenvironmentmetadata)
  - [ManagerConfiguration](#managerconfiguration)
- [Models](#models)
  - [PackageInfo](#packageinfo)
  - [PyPIPackageInfo](#pypipackageinfo)
- [Exceptions](#exceptions)

## Package Manager

PythonEmbedded.Net uses [uv](https://github.com/astral-sh/uv) by default for package and virtual-environment operations, which provides significantly faster operations compared to pip. When `useUv` is `true` (the default), `uv` is ensured on new runtime instances and virtual environments via `EnsureUvInstalledAsync`.

Set `useUv: false` on manager, root-runtime, or package methods to use standard **pip** (`python -m pip`) and **`python -m venv`** instead. Pip-based venvs include pip in the environment; uv-created venvs do not ship pip locally—package commands still work via uv with `--python` targeting the venv interpreter.

**uv detection** searches, in order: adjacent to the Python executable, additional runtime-specific paths, then common install locations and `PATH`. `BasePythonVirtualRuntime` also reads `pyvenv.cfg` and checks the **base interpreter** (`home = ...`) for `uv` next to that Python install—so venvs created with `uv venv` can use the parent runtime's uv.

## Abstract Classes

### BasePythonManager

Abstract base class for managing Python installations. Provides functionality for downloading, installing, and managing Python instances.

#### Properties

##### Configuration

```csharp
ManagerConfiguration Configuration { get; set; }
```

Gets or sets the manager configuration.

#### Methods

##### GetOrCreateInstanceAsync

```csharp
Task<BasePythonRuntime> GetOrCreateInstanceAsync(
    string? pythonVersion = null,
    DateTime? buildDate = null,
    bool useUv = true,
    CancellationToken cancellationToken = default)
```

Gets or creates a Python runtime instance for the specified version. Downloads and installs the Python distribution if it doesn't exist. If `pythonVersion` is null, uses the default version from configuration. When `useUv` is `true`, calls `EnsureUvInstalledAsync` on the returned runtime.

**Parameters:**
- `pythonVersion`: The Python version (e.g., "3.12.0", "3.11", "3.10"). Supports partial versions - if only major.minor is specified (e.g., "3.10"), finds the latest patch version (e.g., "3.10.19"). If null, uses `Configuration.DefaultPythonVersion`.
- `buildDate`: Optional build date. If null, uses the latest build. If specified, finds the first release on or after this date.
- `useUv`: When `true` (default), ensures uv is installed on the runtime; when `false`, skips uv setup (use pip via `useUv: false` on package methods).
- `cancellationToken`: Cancellation token.

**Returns:** A task that represents the asynchronous operation. The result is a `BasePythonRuntime` instance.

**Exceptions:**
- `ArgumentException`: If `pythonVersion` is invalid.
- `InstanceNotFoundException`: If the specified version/build date combination cannot be found.

##### GetOrCreateInstance

```csharp
BasePythonRuntime GetOrCreateInstance(
    string pythonVersion,
    DateTime? buildDate = null,
    bool useUv = true)
```

Synchronous version of `GetOrCreateInstanceAsync`. `pythonVersion` is required (not nullable).

##### DeleteInstanceAsync

```csharp
Task<bool> DeleteInstanceAsync(
    string pythonVersion,
    DateTime? buildDate = null,
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
InstanceMetadata? GetInstanceInfo(string pythonVersion, DateTime? buildDate = null)
```

Gets detailed information about a Python instance.

##### GetInstanceSize

```csharp
long GetInstanceSize(string pythonVersion, DateTime? buildDate = null)
```

Gets the disk size of a Python instance in bytes.

##### GetTotalDiskUsage

```csharp
long GetTotalDiskUsage()
```

Gets the total disk usage across all managed instances in bytes.

##### ValidateInstanceIntegrity

```csharp
bool ValidateInstanceIntegrity(string pythonVersion, DateTime? buildDate = null)
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
    DateTime? buildDate = null,
    bool useUv = true,
    CancellationToken cancellationToken = default)
```

Ensures that a specific Python version is available, downloading it if necessary. Delegates to `GetOrCreateInstanceAsync`.

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
    DateTime? buildDate = null,
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

Abstract base class for Python runtime implementations, providing functionality for executing commands, scripts, and managing packages. Package operations use [uv](https://github.com/astral-sh/uv) when `useUv` is `true` (default), or `python -m pip` when `useUv` is `false`.

#### uv Properties

##### IsUvAvailable

```csharp
bool IsUvAvailable { get; }
```

Gets whether `uv` is available and ready to use.

##### UvPath

```csharp
string? UvPath { get; }
```

Gets the detected path to the `uv` executable, or null if not found.

#### uv Methods

##### DetectUvAsync

```csharp
Task<bool> DetectUvAsync(string? customPath = null, CancellationToken cancellationToken = default)
```

Detects if `uv` is installed. Checks `customPath` first, then paths adjacent to the Python executable, runtime-specific candidates (including `pyvenv.cfg` base interpreter for virtual environments), common install locations, and `PATH`. Returns `true` if uv is available.

##### EnsureUvInstalledAsync

```csharp
Task<bool> EnsureUvInstalledAsync(CancellationToken cancellationToken = default)
```

Ensures `uv` is installed, attempting `pip install uv` if not found. Returns `true` if uv is available after this call. Called automatically when `useUv` is `true` on instance/venv creation.

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
    bool useUv = true,
    CancellationToken cancellationToken = default)
```

Installs a Python package using uv (`uv pip install ... --python`) or pip (`python -m pip install`).

**Parameters:**
- `packageSpecification`: The package specification (e.g., "numpy", "torch==2.0.0", "numpy>=1.20.0").
- `upgrade`: Whether to upgrade the package if it's already installed.
- `indexUrl`: Optional custom PyPI index URL to use for this installation.
- `useUv`: When `true` (default), uses uv; when `false`, uses `python -m pip`.
- `cancellationToken`: Cancellation token.

**Returns:** The execution result from the package manager.

##### InstallRequirementsAsync

```csharp
Task<PythonExecutionResult> InstallRequirementsAsync(
    string requirementsFilePath,
    bool upgrade = false,
    bool useUv = true,
    CancellationToken cancellationToken = default)
```

Installs Python packages from a requirements.txt file.

**Parameters:**
- `requirementsFilePath`: The path to the requirements.txt file.
- `upgrade`: Whether to upgrade packages if they're already installed.
- `useUv`: When `true` (default), uses uv; when `false`, uses pip.
- `cancellationToken`: Cancellation token.

**Returns:** The execution result from the package manager.

##### CheckRequirementsAsync

```csharp
Task<IReadOnlyList<RequirementStatus>> CheckRequirementsAsync(
    string requirementsFilePath,
    CancellationToken cancellationToken = default)
```

Checks which packages from a requirements file are missing or do not meet version constraints. Does not use uv/pip—runs an embedded Python check script.

**Returns:** A list of `RequirementStatus` records (`PackageSpecification`, `IsInstalled`, `MeetsRequirement`, `InstalledVersion`, `RequiredVersion`).

##### GetMissingPackagesAsync

```csharp
Task<string[]> GetMissingPackagesAsync(string[] packageNames, CancellationToken cancellationToken = default)
```

Determines which package names from a list are not importable (uses `importlib.util.find_spec` in a single Python invocation). Hyphens in names are mapped to underscores for module lookup.

##### InstallPyProjectAsync

```csharp
Task<PythonExecutionResult> InstallPyProjectAsync(
    string pyProjectFilePath,
    bool editable = false,
    bool useUv = true,
    CancellationToken cancellationToken = default)
```

Installs a Python package from a pyproject.toml file.

**Parameters:**
- `pyProjectFilePath`: The path to the directory containing pyproject.toml or the pyproject.toml file itself.
- `editable`: Whether to install in editable mode.
- `useUv`: When `true` (default), uses uv; when `false`, uses pip.
- `cancellationToken`: Cancellation token.

**Returns:** The execution result from the package manager.

##### ListInstalledPackagesAsync

```csharp
Task<IReadOnlyList<PackageInfo>> ListInstalledPackagesAsync(
    bool useUv = true,
    CancellationToken cancellationToken = default)
```

Lists all installed packages (`pip list --format=json` via uv or pip).

##### GetPackageVersionAsync

```csharp
Task<string?> GetPackageVersionAsync(
    string packageName,
    bool useUv = true,
    CancellationToken cancellationToken = default)
```

Gets the version of a specific installed package (`pip show`).

##### IsPackageInstalledAsync

```csharp
Task<bool> IsPackageInstalledAsync(
    string packageName,
    bool useUv = true,
    CancellationToken cancellationToken = default)
```

Checks if a package is installed (`pip show` exit code).

##### GetPackageInfoAsync

```csharp
Task<PackageInfo?> GetPackageInfoAsync(
    string packageName,
    bool useUv = true,
    CancellationToken cancellationToken = default)
```

Gets detailed information about an installed package (parsed from `pip show`).

##### UninstallPackageAsync

```csharp
Task<PythonExecutionResult> UninstallPackageAsync(
    string packageName,
    bool useUv = true,
    CancellationToken cancellationToken = default)
```

Uninstalls a Python package (`pip uninstall -y`).

##### UpgradeAllPackagesAsync

```csharp
Task<PythonExecutionResult> UpgradeAllPackagesAsync(
    bool useUv = true,
    CancellationToken cancellationToken = default)
```

Upgrades all outdated installed packages.

##### ListOutdatedPackagesAsync

```csharp
Task<IReadOnlyList<OutdatedPackageInfo>> ListOutdatedPackagesAsync(
    bool useUv = true,
    CancellationToken cancellationToken = default)
```

Lists packages that have available updates (`pip list --outdated --format=json`).

**Returns:** A list of outdated packages with their current and latest versions.

##### DowngradePackageAsync

```csharp
Task<PythonExecutionResult> DowngradePackageAsync(
    string packageName,
    string targetVersion,
    bool useUv = true,
    CancellationToken cancellationToken = default)
```

Downgrades a package to a specific version (installs `package==targetVersion`).

##### ExportRequirementsAsync

```csharp
Task<PythonExecutionResult> ExportRequirementsAsync(
    string outputPath,
    bool useUv = true,
    CancellationToken cancellationToken = default)
```

Exports installed packages to a requirements.txt file (`pip freeze` output written to `outputPath`).

##### ExportRequirementsFreezeAsync

```csharp
Task<PythonExecutionResult> ExportRequirementsFreezeAsync(
    string outputPath,
    bool useUv = true,
    CancellationToken cancellationToken = default)
```

Same as `ExportRequirementsAsync`—writes `pip freeze` output to a file.

##### ExportRequirementsFreezeToStringAsync

```csharp
Task<string> ExportRequirementsFreezeToStringAsync(
    bool useUv = true,
    CancellationToken cancellationToken = default)
```

Returns `pip freeze` output as a string.

##### InstallPackagesAsync

```csharp
Task<Dictionary<string, PythonExecutionResult>> InstallPackagesAsync(
    IEnumerable<string> packages,
    bool parallel = false,
    bool upgrade = false,
    bool useUv = true,
    CancellationToken cancellationToken = default)
```

Installs multiple packages in batch.

**Parameters:**
- `packages`: The list of package specifications to install.
- `parallel`: Whether to install packages in parallel.
- `upgrade`: Whether to upgrade packages if they're already installed.
- `useUv`: When `true` (default), uses uv; when `false`, uses pip.
- `cancellationToken`: Cancellation token.

**Returns:** A dictionary mapping package specifications to their installation results.

##### UninstallPackagesAsync

```csharp
Task<Dictionary<string, PythonExecutionResult>> UninstallPackagesAsync(
    IEnumerable<string> packages,
    bool parallel = false,
    bool useUv = true,
    CancellationToken cancellationToken = default)
```

Uninstalls multiple packages in batch.

**Parameters:**
- `packages`: The list of package names to uninstall.
- `parallel`: Whether to uninstall packages in parallel.
- `useUv`: When `true` (default), uses uv; when `false`, uses pip.
- `cancellationToken`: Cancellation token.

**Returns:** A dictionary mapping package names to their uninstallation results.

##### GetPipVersionAsync / GetPipConfigurationAsync / ConfigurePipIndexAsync / ConfigurePipProxyAsync

Pip configuration helpers always use `python -m pip` (not uv). `ConfigurePipIndexAsync` optionally sets `trusted-host` when `trusted` is `true`.

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

**Parameters:**
- `packageName`: The name of the package.
- `version`: Optional version. If not specified, returns the latest version.
- `cancellationToken`: Cancellation token.

**Returns:** Package metadata, or null if not found.

##### ValidatePythonInstallationAsync

```csharp
Task<Dictionary<string, object>> ValidatePythonInstallationAsync(CancellationToken cancellationToken = default)
```

Performs a comprehensive health check of the Python installation.

**Returns:** A dictionary containing health check results including:
- `ExecutableExists`: Whether the Python executable exists
- `WorkingDirectoryExists`: Whether the working directory exists
- `PythonVersionCheck`: Status of Python version check
- `PipCheck`: Status of pip availability
- `CommandExecution`: Status of command execution test
- `OverallHealth`: Overall health status ("Healthy" or "Unhealthy")

##### ValidatePythonVersionString

```csharp
static bool ValidatePythonVersionString(string version)
```

Validates that a Python version string is in a valid format.

**Parameters:**
- `version`: The version string to validate.

**Returns:** True if the version string is valid, false otherwise.

##### ValidatePackageSpecification

```csharp
static bool ValidatePackageSpecification(string packageSpec)
```

Validates that a package specification is in a valid format.

**Parameters:**
- `packageSpec`: The package specification to validate.

**Returns:** True if the package specification is valid, false otherwise.

---

### BasePythonRootRuntime

Abstract base class for Python root runtime implementations that can manage virtual environments. Extends `BasePythonRuntime`.

#### Methods

##### GetOrCreateVirtualEnvironmentAsync

```csharp
Task<BasePythonVirtualRuntime> GetOrCreateVirtualEnvironmentAsync(
    string name,
    bool recreateIfExists = false,
    string? externalPath = null,
    bool useUv = true,
    CancellationToken cancellationToken = default)
```

Gets or creates a virtual environment with the specified name. When `useUv` is `true`, creates with `uv venv` and ensures uv on the returned runtime; when `false`, uses `python -m venv` (standard pip-inclusive venv).

**Parameters:**
- `name`: The name of the virtual environment.
- `recreateIfExists`: Whether to recreate the virtual environment if it already exists.
- `externalPath`: Optional external path where the venv should be created. If null, uses default location.
- `useUv`: When `true` (default), `uv venv` + `EnsureUvInstalledAsync`; when `false`, `python -m venv`.
- `cancellationToken`: Cancellation token.

**Returns:** The virtual environment runtime.

**Exceptions:**
- `InvalidOperationException`: If a venv with the same name already exists and `recreateIfExists` is false.

##### DeleteVirtualEnvironmentAsync

```csharp
Task<bool> DeleteVirtualEnvironmentAsync(
    string name,
    CancellationToken cancellationToken = default,
    bool deleteExternalFiles = true)
```

Deletes a virtual environment with the specified name.

**Parameters:**
- `name`: The name of the virtual environment to delete.
- `cancellationToken`: Cancellation token.
- `deleteExternalFiles`: For external venvs, whether to delete the actual files (default true). If false, only removes the metadata tracking.

**Returns:** `true` if deleted, `false` if not found.

**Exceptions:**
- `VirtualEnvironmentNotFoundException`: When deletion fails.

##### ListVirtualEnvironments

```csharp
IReadOnlyList<string> ListVirtualEnvironments()
```

Lists all virtual environments.

##### VirtualEnvironmentExists

```csharp
bool VirtualEnvironmentExists(string name)
```

Checks if a virtual environment with the specified name exists.

**Parameters:**
- `name`: The name of the virtual environment.

**Returns:** True if the venv exists (standard or external), false otherwise.

##### ResolveVirtualEnvironmentPath

```csharp
string ResolveVirtualEnvironmentPath(string name)
```

Resolves the actual path to a virtual environment from its name. Checks metadata for external paths.

**Parameters:**
- `name`: The name of the virtual environment.

**Returns:** The actual path to the virtual environment.

##### GetVirtualEnvironmentMetadata

```csharp
VirtualEnvironmentMetadata? GetVirtualEnvironmentMetadata(string name)
```

Gets the metadata for a virtual environment.

**Parameters:**
- `name`: The name of the virtual environment.

**Returns:** The metadata if found, null otherwise.

##### GetVirtualEnvironmentSize

```csharp
long GetVirtualEnvironmentSize(string name)
```

Gets the disk size of a virtual environment in bytes.

**Parameters:**
- `name`: The name of the virtual environment.

**Returns:** The size in bytes.

##### GetVirtualEnvironmentInfo

```csharp
Dictionary<string, object> GetVirtualEnvironmentInfo(string name)
```

Gets detailed information about a virtual environment.

**Parameters:**
- `name`: The name of the virtual environment.

**Returns:** A dictionary containing information about the virtual environment including `Name`, `Path`, `SizeBytes`, `Exists`, `Created`, `Modified`, `IsExternal`, `ExternalPath`, and `PythonVersion`.

##### CloneVirtualEnvironmentAsync

```csharp
Task<BasePythonVirtualRuntime> CloneVirtualEnvironmentAsync(
    string sourceName,
    string targetName,
    bool useUv = true,
    CancellationToken cancellationToken = default)
```

Clones a virtual environment to create a new one with the same packages and configuration. When `useUv` is `true`, ensures uv on the cloned runtime.

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
    bool useUv = true,
    CancellationToken cancellationToken = default)
```

Imports a virtual environment from an archive file. When `useUv` is `true`, ensures uv on the imported runtime.

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
    ILoggerFactory? loggerFactory = null,
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
    ILoggerFactory? loggerFactory = null,
    IMemoryCache? cache = null,
    ManagerConfiguration? configuration = null)
```

### PythonRootRuntime

Concrete implementation of `BasePythonRootRuntime` for subprocess-based execution.

### PythonNetRootRuntime

Concrete implementation of `BasePythonRootRuntime` for Python.NET-based execution. Implements `IDisposable`.

### PythonRootVirtualEnvironment

Concrete implementation of `BasePythonVirtualRuntime` for subprocess-based execution.

### PythonNetVirtualEnvironment

Concrete implementation of `BasePythonVirtualRuntime` for Python.NET-based execution. Implements `IDisposable`.

### BasePythonVirtualRuntime

Abstract base class for Python virtual environment runtime implementations. Extends `BasePythonRuntime`.

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

### InstanceMetadata

Represents metadata associated with a Python runtime instance.

**Properties:**
- `PythonVersion` (string): The version of Python
- `BuildDate` (DateTime): The date indicating when the build was created
- `WasLatestBuild` (bool): Whether the build was the latest at installation time
- `InstallationDate` (DateTime): The date and time when the installation was completed
- `VirtualEnvironments` (List\<VirtualEnvironmentMetadata\>): Collection of virtual environments managed by this instance
- `Directory` (string): The directory path associated with the Python runtime instance (read-only)

**Methods:**
- `GetVirtualEnvironment(string name)`: Gets virtual environment metadata by name
- `SetVirtualEnvironment(VirtualEnvironmentMetadata metadata)`: Adds or updates virtual environment metadata
- `RemoveVirtualEnvironment(string name)`: Removes virtual environment metadata

### VirtualEnvironmentMetadata

Represents metadata associated with a Python virtual environment.

**Properties:**
- `Name` (string): The name of the virtual environment
- `ExternalPath` (string?): The actual path for external virtual environments (null for default location)
- `CreatedDate` (DateTime): When the virtual environment was created
- `IsExternal` (bool): Whether this venv is stored at an external location (computed from ExternalPath)

**Methods:**
- `GetResolvedPath(string defaultPath)`: Gets the resolved path (ExternalPath if set, otherwise defaultPath)

### PackageInfo

Represents information about an installed Python package.

**Properties:**
- `Name` (string): Package name
- `Version` (string): Package version
- `Location` (string?): Installation location
- `Summary` (string?): Package summary

### OutdatedPackageInfo

Represents information about an outdated package (available update).

**Properties:**
- `Name` (string): Package name
- `InstalledVersion` (string): Currently installed version
- `LatestVersion` (string): Latest available version

### RequirementStatus

Represents the status of a single line from a requirements file (`CheckRequirementsAsync`).

**Properties:**
- `PackageSpecification` (string): Original requirement string
- `IsInstalled` (bool): Whether the package is installed
- `MeetsRequirement` (bool): Whether the installed version satisfies the requirement
- `InstalledVersion` (string?): Currently installed version
- `RequiredVersion` (string?): Required version or range

### PyPIPackageInfo

Represents package metadata from PyPI.

**Properties:**
- `Name` (string): Package name
- `Version` (string): Package version
- `Summary` (string?): Package summary
- `Description` (string?): Package description
- `Author` (string?): Package author
- `AuthorEmail` (string?): Author email
- `HomePage` (string?): Package homepage URL
- `License` (string?): Package license
- `RequiresPython` (IReadOnlyList\<string\>?): Python version requirements

### PyPISearchResult

Represents a simplified search result from PyPI.

**Properties:**
- `Name` (string): Package name
- `Version` (string): Package version
- `Summary` (string?): Package summary

---

## Exceptions

- `PythonInstallationException`: Base exception for installation-related errors
- `InstanceNotFoundException`: Thrown when a Python instance is not found
- `PythonExecutionException`: Thrown when Python execution fails
- `PackageInstallationException`: Thrown when package installation fails
- `PythonNotInstalledException`: Thrown when Python installation is missing or invalid
- `PlatformNotSupportedException`: Thrown when the platform is not supported
- `InvalidPackageSpecificationException`: Thrown when a package specification is invalid
- `InvalidPythonVersionException`: Thrown when a Python version string is invalid
- `MetadataCorruptedException`: Thrown when instance metadata is corrupted
- `PythonNetExecutionException`: Thrown when Python.NET execution fails (extends `PythonExecutionException`)
- `PythonNetInitializationException`: Thrown when Python.NET initialization fails
- `RequirementsFileException`: Thrown when requirements file installation fails (extends `PackageInstallationException`)
- `VirtualEnvironmentNotFoundException`: Thrown when a virtual environment is not found
