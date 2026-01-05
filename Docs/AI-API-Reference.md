# AI API Reference for PythonEmbedded.Net

This document is optimized for AI models to understand the PythonEmbedded.Net API. It provides structured, comprehensive information about all public classes, methods, and their usage patterns.

## Namespace

All classes are in the `PythonEmbedded.Net` namespace. Models are in `PythonEmbedded.Net.Models`. Exceptions are in `PythonEmbedded.Net.Exceptions`.

## Core Architecture

The library follows a hierarchical class structure:

1. **BasePythonManager** (abstract) - Manages Python installations
   - **PythonManager** (concrete) - Subprocess-based execution
   - **PythonNetManager** (concrete) - Python.NET in-process execution

2. **BasePythonRuntime** (abstract) - Executes Python code and manages packages
   - **BasePythonRootRuntime** (abstract) - Can create virtual environments
     - **PythonRootRuntime** (concrete) - Subprocess-based root runtime
     - **PythonNetRootRuntime** (concrete) - Python.NET root runtime (implements IDisposable)
   - **BasePythonVirtualRuntime** (abstract) - Virtual environment runtime
     - **PythonRootVirtualEnvironment** (concrete) - Subprocess-based virtual environment
     - **PythonNetVirtualEnvironment** (concrete) - Python.NET virtual environment (implements IDisposable)

## Getting Started Pattern

```csharp
using PythonEmbedded.Net;
using Octokit;

// Create a GitHub client (required for downloading Python)
var githubClient = new GitHubClient(new ProductHeaderValue("MyApp"));

// Create a manager
var manager = new PythonManager(
    directory: "./python-instances",
    githubClient: githubClient
);

// Get or create a Python instance
var runtime = await manager.GetOrCreateInstanceAsync("3.12");

// Execute Python code
var result = await runtime.ExecuteCommandAsync("print('Hello, World!')");
Console.WriteLine(result.StandardOutput); // "Hello, World!"
```

## BasePythonManager

**Purpose**: Manages Python installations - downloads, installs, and provides access to Python runtimes.

### Key Properties

- `Configuration` (ManagerConfiguration): Gets or sets the manager configuration

### Core Methods

#### GetOrCreateInstanceAsync

```csharp
Task<BasePythonRuntime> GetOrCreateInstanceAsync(
    string? pythonVersion = null,
    DateTime? buildDate = null,
    CancellationToken cancellationToken = default)
```

**Behavior**: 
- If `pythonVersion` is null, uses `Configuration.DefaultPythonVersion` (defaults to "3.12")
- Supports partial versions: "3.12" finds latest patch version (e.g., "3.12.19")
- If instance exists, returns existing runtime
- If instance doesn't exist, downloads and installs Python from GitHub releases
- Returns a `BasePythonRuntime` instance

**Example**:
```csharp
// Get latest 3.12.x
var runtime = await manager.GetOrCreateInstanceAsync("3.12");

// Get specific version
var runtime = await manager.GetOrCreateInstanceAsync("3.12.5");

// Get specific build date
var runtime = await manager.GetOrCreateInstanceAsync("3.12", new DateTime(2024, 1, 15));
```

#### DeleteInstanceAsync

```csharp
Task<bool> DeleteInstanceAsync(
    string pythonVersion,
    DateTime? buildDate = null,
    CancellationToken cancellationToken = default)
```

**Returns**: `true` if deleted, `false` if not found

#### ListInstances

```csharp
IReadOnlyList<InstanceMetadata> ListInstances()
```

**Returns**: List of all managed Python instances with metadata

#### ListAvailableVersionsAsync

```csharp
Task<IReadOnlyList<string>> ListAvailableVersionsAsync(
    string? releaseTag = null,
    CancellationToken cancellationToken = default)
```

**Returns**: List of available Python versions from GitHub releases

#### GetInstanceInfo

```csharp
InstanceMetadata? GetInstanceInfo(string pythonVersion, DateTime? buildDate = null)
```

**Returns**: Instance metadata if found, `null` otherwise

#### GetInstanceSize

```csharp
long GetInstanceSize(string pythonVersion, DateTime? buildDate = null)
```

**Returns**: Disk size in bytes, or 0 if not found

#### GetTotalDiskUsage

```csharp
long GetTotalDiskUsage()
```

**Returns**: Total disk usage across all instances in bytes

#### ValidateInstanceIntegrity

```csharp
bool ValidateInstanceIntegrity(string pythonVersion, DateTime? buildDate = null)
```

**Returns**: `true` if instance is valid, `false` otherwise

#### GetLatestPythonVersionAsync

```csharp
Task<string?> GetLatestPythonVersionAsync(CancellationToken cancellationToken = default)
```

**Returns**: Latest available Python version string, or `null` if not found

#### FindBestMatchingVersionAsync

```csharp
Task<string?> FindBestMatchingVersionAsync(string versionSpec, CancellationToken cancellationToken = default)
```

**Parameters**: `versionSpec` can be "3.12", "3.12.0", ">=3.11", etc.

**Returns**: Best matching version, or `null` if none found

#### EnsurePythonVersionAsync

```csharp
Task<BasePythonRuntime> EnsurePythonVersionAsync(
    string pythonVersion,
    DateTime? buildDate = null,
    CancellationToken cancellationToken = default)
```

**Behavior**: Same as `GetOrCreateInstanceAsync` but throws if version cannot be ensured

#### CheckDiskSpace

```csharp
bool CheckDiskSpace(long requiredBytes)
```

**Returns**: `true` if sufficient disk space available

#### TestNetworkConnectivityAsync

```csharp
Task<bool> TestNetworkConnectivityAsync(CancellationToken cancellationToken = default)
```

**Returns**: `true` if network connectivity to GitHub API is available

#### GetSystemRequirements

```csharp
Dictionary<string, object> GetSystemRequirements()
```

**Returns**: Dictionary with system requirements information

#### DiagnoseIssuesAsync

```csharp
Task<IReadOnlyList<string>> DiagnoseIssuesAsync(CancellationToken cancellationToken = default)
```

**Returns**: List of diagnostic issue messages

#### ExportInstanceAsync

```csharp
Task<string> ExportInstanceAsync(
    string pythonVersion,
    string outputPath,
    DateTime? buildDate = null,
    CancellationToken cancellationToken = default)
```

**Returns**: Path to the created archive file

#### ImportInstanceAsync

```csharp
Task<InstanceMetadata> ImportInstanceAsync(
    string archivePath,
    CancellationToken cancellationToken = default)
```

**Returns**: Metadata of the imported instance

**Note**: All async methods have synchronous versions (without "Async" suffix) that block until completion.

## BasePythonRuntime

**Purpose**: Executes Python code, manages packages using [uv](https://github.com/astral-sh/uv), and provides Python runtime functionality.

### Package Manager (uv)

This library uses `uv` as its package manager, which provides significantly faster package operations compared to pip. `uv` is automatically installed when runtime instances and virtual environments are created.

#### uv Properties

```csharp
bool IsUvAvailable { get; }  // Whether uv is available
string? UvExecutablePath { get; }  // Path to uv executable
```

#### uv Methods

```csharp
Task DetectUvAsync(CancellationToken cancellationToken = default)  // Detect uv availability
Task EnsureUvInstalledAsync(CancellationToken cancellationToken = default)  // Install uv if needed
```

### Core Execution Methods

#### ExecuteCommandAsync

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

**Purpose**: Executes a Python command string (using `-c` flag)

**Parameters**:
- `command`: Python code to execute (e.g., `"print('Hello')"` or `"import sys; print(sys.version)"`)
- `stdinHandler`: Optional function that returns lines to send to stdin. Return `null` to end input.
- `stdoutHandler`: Optional action called for each stdout line
- `stderrHandler`: Optional action called for each stderr line
- `workingDirectory`: Override working directory (defaults to runtime's working directory)
- `environmentVariables`: Additional environment variables to set
- `priority`: Process priority (Windows-specific)
- `maxMemoryMB`: Maximum memory limit (not fully implemented)
- `timeout`: Per-execution timeout (in addition to CancellationToken)

**Returns**: `PythonExecutionResult` with `ExitCode`, `StandardOutput`, and `StandardError`

**Example**:
```csharp
var result = await runtime.ExecuteCommandAsync("print('Hello, World!')");
// result.StandardOutput contains "Hello, World!"
// result.ExitCode is 0 if successful
```

#### ExecuteScriptAsync

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

**Purpose**: Executes a Python script file

**Parameters**:
- `scriptPath`: Path to the Python script file
- `arguments`: Command-line arguments to pass to the script

**Example**:
```csharp
var result = await runtime.ExecuteScriptAsync(
    "script.py",
    new[] { "arg1", "arg2" }
);
```

### Package Management Methods

#### InstallPackageAsync

```csharp
Task<PythonExecutionResult> InstallPackageAsync(
    string packageSpecification,
    bool upgrade = false,
    string? indexUrl = null,
    CancellationToken cancellationToken = default)
```

**Purpose**: Installs a Python package using `uv` (significantly faster than pip)

**Parameters**:
- `packageSpecification`: Package name or specification (e.g., "numpy", "torch==2.0.0", "numpy>=1.20.0")
- `upgrade`: Whether to upgrade if already installed
- `indexUrl`: Custom PyPI index URL for this installation

**Throws**: `PackageInstallationException` if installation fails, `InvalidOperationException` if uv is not available

**Example**:
```csharp
await runtime.InstallPackageAsync("numpy");
await runtime.InstallPackageAsync("torch==2.0.0", upgrade: true);
```

#### InstallRequirementsAsync

```csharp
Task<PythonExecutionResult> InstallRequirementsAsync(
    string requirementsFilePath,
    bool upgrade = false,
    CancellationToken cancellationToken = default)
```

**Purpose**: Installs packages from a requirements.txt file

**Throws**: `RequirementsFileException` if installation fails

#### InstallPyProjectAsync

```csharp
Task<PythonExecutionResult> InstallPyProjectAsync(
    string pyProjectFilePath,
    bool editable = false,
    CancellationToken cancellationToken = default)
```

**Purpose**: Installs a package from a pyproject.toml file

**Parameters**:
- `pyProjectFilePath`: Path to directory containing pyproject.toml or the file itself
- `editable`: Whether to install in editable mode (`pip install -e`)

#### ListInstalledPackagesAsync

```csharp
Task<IReadOnlyList<PackageInfo>> ListInstalledPackagesAsync(CancellationToken cancellationToken = default)
```

**Returns**: List of installed packages with name, version, location, and summary

#### GetPackageVersionAsync

```csharp
Task<string?> GetPackageVersionAsync(string packageName, CancellationToken cancellationToken = default)
```

**Returns**: Package version if installed, `null` otherwise

#### IsPackageInstalledAsync

```csharp
Task<bool> IsPackageInstalledAsync(string packageName, CancellationToken cancellationToken = default)
```

**Returns**: `true` if package is installed, `false` otherwise

#### GetPackageInfoAsync

```csharp
Task<PackageInfo?> GetPackageInfoAsync(string packageName, CancellationToken cancellationToken = default)
```

**Returns**: Detailed package information if installed, `null` otherwise

#### UninstallPackageAsync

```csharp
Task<PythonExecutionResult> UninstallPackageAsync(
    string packageName,
    bool removeDependencies = false,
    CancellationToken cancellationToken = default)
```

**Note**: `removeDependencies` parameter is accepted but not fully implemented

#### ListOutdatedPackagesAsync

```csharp
Task<IReadOnlyList<OutdatedPackageInfo>> ListOutdatedPackagesAsync(CancellationToken cancellationToken = default)
```

**Returns**: List of packages with available updates (includes installed and latest versions)

#### UpgradeAllPackagesAsync

```csharp
Task<PythonExecutionResult> UpgradeAllPackagesAsync(CancellationToken cancellationToken = default)
```

**Behavior**: Upgrades all outdated packages sequentially

#### DowngradePackageAsync

```csharp
Task<PythonExecutionResult> DowngradePackageAsync(
    string packageName,
    string targetVersion,
    CancellationToken cancellationToken = default)
```

**Purpose**: Downgrades a package to a specific version

#### InstallPackagesAsync

```csharp
Task<Dictionary<string, PythonExecutionResult>> InstallPackagesAsync(
    IEnumerable<string> packages,
    bool parallel = false,
    bool upgrade = false,
    CancellationToken cancellationToken = default)
```

**Purpose**: Installs multiple packages in batch

**Returns**: Dictionary mapping package specifications to their installation results

**Note**: If `parallel` is true, packages are installed concurrently. Failures are captured in the result dictionary.

#### UninstallPackagesAsync

```csharp
Task<Dictionary<string, PythonExecutionResult>> UninstallPackagesAsync(
    IEnumerable<string> packages,
    bool parallel = false,
    bool removeDependencies = false,
    CancellationToken cancellationToken = default)
```

**Purpose**: Uninstalls multiple packages in batch

### Requirements Export Methods

#### ExportRequirementsAsync

```csharp
Task<PythonExecutionResult> ExportRequirementsAsync(string outputPath, CancellationToken cancellationToken = default)
```

**Purpose**: Exports installed packages to a requirements.txt file (with version constraints)

#### ExportRequirementsFreezeAsync

```csharp
Task<PythonExecutionResult> ExportRequirementsFreezeAsync(string outputPath, CancellationToken cancellationToken = default)
```

**Purpose**: Exports installed packages to a requirements.txt file with exact versions (pip freeze)

#### ExportRequirementsFreezeToStringAsync

```csharp
Task<string> ExportRequirementsFreezeToStringAsync(CancellationToken cancellationToken = default)
```

**Returns**: Requirements.txt content as a string (pip freeze output)

### Pip Configuration Methods

#### GetPipConfigurationAsync

```csharp
Task<PipConfiguration> GetPipConfigurationAsync(CancellationToken cancellationToken = default)
```

**Returns**: Current pip configuration (index URL, trusted host, proxy)

#### ConfigurePipIndexAsync

```csharp
Task<PythonExecutionResult> ConfigurePipIndexAsync(
    string indexUrl,
    bool trusted = false,
    CancellationToken cancellationToken = default)
```

**Purpose**: Configures pip to use a custom index URL

**Note**: If `trusted` is true, automatically configures trusted-host from the URL

#### ConfigurePipProxyAsync

```csharp
Task<PythonExecutionResult> ConfigurePipProxyAsync(
    string proxyUrl,
    CancellationToken cancellationToken = default)
```

**Purpose**: Configures pip proxy settings

### Version and Information Methods

#### GetPipVersionAsync

```csharp
Task<string> GetPipVersionAsync(CancellationToken cancellationToken = default)
```

**Returns**: Pip version string

#### GetPythonVersionInfoAsync

```csharp
Task<string> GetPythonVersionInfoAsync(CancellationToken cancellationToken = default)
```

**Returns**: Detailed Python version information (sys.version and sys.executable)

#### ValidatePythonInstallationAsync

```csharp
Task<Dictionary<string, object>> ValidatePythonInstallationAsync(CancellationToken cancellationToken = default)
```

**Returns**: Dictionary with health check results:
- `ExecutableExists` (bool): Whether Python executable exists
- `ExecutablePath` (string): Path to Python executable
- `WorkingDirectoryExists` (bool): Whether working directory exists
- `WorkingDirectory` (string): Working directory path
- `PythonVersionCheck` (string): "Success" or "Failed"
- `PythonVersionInfo` (string): Python version information if successful
- `PipCheck` (string): "Success" or "Failed"
- `PipVersion` (string): Pip version if successful
- `CommandExecution` (string): "Success" or "Failed"
- `CommandExitCode` (int): Exit code from test command
- `OverallHealth` (string): "Healthy" or "Unhealthy"

### PyPI Methods

#### SearchPackagesAsync

```csharp
Task<IReadOnlyList<PyPISearchResult>> SearchPackagesAsync(
    string query,
    CancellationToken cancellationToken = default)
```

**Purpose**: Searches PyPI for packages

**Note**: Currently performs exact package name lookup. Full search functionality may be limited.

**Returns**: List of search results with name, version, and summary

#### GetPackageMetadataAsync

```csharp
Task<PyPIPackageInfo?> GetPackageMetadataAsync(
    string packageName,
    string? version = null,
    CancellationToken cancellationToken = default)
```

**Purpose**: Gets package metadata from PyPI

**Parameters**:
- `packageName`: Package name
- `version`: Optional version (defaults to latest)

**Returns**: Package metadata or `null` if not found

### Static Validation Methods

#### ValidatePythonVersionString

```csharp
static bool ValidatePythonVersionString(string version)
```

**Purpose**: Validates Python version string format

**Returns**: `true` if valid format (e.g., "3.12.0", "3.12", "3")

#### ValidatePackageSpecification

```csharp
static bool ValidatePackageSpecification(string packageSpec)
```

**Purpose**: Validates package specification format

**Returns**: `true` if valid (supports version specifiers like ==, >=, <=, >, <, ~=, !=)

## BasePythonRootRuntime

**Purpose**: Extends `BasePythonRuntime` with virtual environment management capabilities. Virtual environments are created using `uv`, which is significantly faster than `python -m venv`.

### Virtual Environment Methods

#### GetOrCreateVirtualEnvironmentAsync

```csharp
Task<BasePythonVirtualRuntime> GetOrCreateVirtualEnvironmentAsync(
    string name,
    bool recreateIfExists = false,
    string? externalPath = null,
    CancellationToken cancellationToken = default)
```

**Purpose**: Gets or creates a virtual environment using `uv`

**Parameters**:
- `name`: Name of the virtual environment
- `recreateIfExists`: Whether to recreate if it already exists
- `externalPath`: Optional external path where the venv should be created. If null, uses default location within instance directory.

**Returns**: `BasePythonVirtualRuntime` instance

**Throws**: `InvalidOperationException` if a venv with the same name already exists and `recreateIfExists` is false

**Example**:
```csharp
// Create venv in default location
var venv = await rootRuntime.GetOrCreateVirtualEnvironmentAsync("myenv");

// Create venv at external path
var externalVenv = await rootRuntime.GetOrCreateVirtualEnvironmentAsync(
    "projectenv",
    externalPath: "/path/to/project/.venv");
```

#### DeleteVirtualEnvironmentAsync

```csharp
Task<bool> DeleteVirtualEnvironmentAsync(
    string name,
    CancellationToken cancellationToken = default,
    bool deleteExternalFiles = true)
```

**Purpose**: Deletes a virtual environment

**Parameters**:
- `name`: Name of the virtual environment to delete
- `cancellationToken`: Cancellation token
- `deleteExternalFiles`: For external venvs, whether to delete the actual files (default true). If false, only removes the metadata tracking.

**Returns**: `true` if deleted, `false` if not found

#### ListVirtualEnvironments

```csharp
IReadOnlyList<string> ListVirtualEnvironments()
```

**Returns**: List of virtual environment names

#### GetVirtualEnvironmentSize

```csharp
long GetVirtualEnvironmentSize(string name)
```

**Returns**: Disk size in bytes

#### GetVirtualEnvironmentInfo

```csharp
Dictionary<string, object> GetVirtualEnvironmentInfo(string name)
```

**Returns**: Dictionary with virtual environment information (Name, Path, SizeBytes, Exists, Created, Modified, PythonVersion, IsExternal, ExternalPath)

#### VirtualEnvironmentExists

```csharp
bool VirtualEnvironmentExists(string name)
```

**Purpose**: Checks if a virtual environment with the specified name exists (standard or external)

**Returns**: `true` if exists, `false` otherwise

#### ResolveVirtualEnvironmentPath

```csharp
string ResolveVirtualEnvironmentPath(string name)
```

**Purpose**: Resolves the actual path to a virtual environment from its name. Checks metadata for external paths.

**Returns**: The actual path to the virtual environment

#### GetVirtualEnvironmentMetadata

```csharp
VirtualEnvironmentMetadata? GetVirtualEnvironmentMetadata(string name)
```

**Purpose**: Gets the metadata for a virtual environment

**Returns**: The metadata if found, null otherwise

#### CloneVirtualEnvironmentAsync

```csharp
Task<BasePythonVirtualRuntime> CloneVirtualEnvironmentAsync(
    string sourceName,
    string targetName,
    CancellationToken cancellationToken = default)
```

**Purpose**: Clones a virtual environment with all packages and configuration

**Throws**: `InvalidOperationException` if target already exists

#### ExportVirtualEnvironmentAsync

```csharp
Task<string> ExportVirtualEnvironmentAsync(
    string name,
    string outputPath,
    CancellationToken cancellationToken = default)
```

**Purpose**: Exports virtual environment to a zip archive

**Returns**: Path to created archive

**Note**: Currently only supports .zip format

#### ImportVirtualEnvironmentAsync

```csharp
Task<BasePythonVirtualRuntime> ImportVirtualEnvironmentAsync(
    string name,
    string archivePath,
    CancellationToken cancellationToken = default)
```

**Purpose**: Imports virtual environment from an archive

**Throws**: `InvalidOperationException` if virtual environment already exists

## BasePythonVirtualRuntime

**Purpose**: Represents a Python virtual environment runtime. Extends `BasePythonRuntime`.

**Key Difference**: The Python executable path points to the virtual environment's Python, and packages installed are isolated to this virtual environment.

**Usage**: All methods from `BasePythonRuntime` are available, but operate within the virtual environment context.

## Data Models

### PythonExecutionResult

```csharp
public record PythonExecutionResult(
    int ExitCode,
    string StandardOutput = "",
    string StandardError = "")
```

**Purpose**: Result of executing a Python command or script

**Properties**:
- `ExitCode` (int): Process exit code (0 = success)
- `StandardOutput` (string): Standard output text
- `StandardError` (string): Standard error text

### PackageInfo

```csharp
public record PackageInfo(
    string Name,
    string Version,
    string? Location = null,
    string? Summary = null)
```

**Purpose**: Information about an installed Python package

### OutdatedPackageInfo

```csharp
public record OutdatedPackageInfo(
    string Name,
    string InstalledVersion,
    string LatestVersion)
```

**Purpose**: Information about a package with available updates

### PyPIPackageInfo

```csharp
public record PyPIPackageInfo(
    string Name,
    string Version,
    string? Summary = null,
    string? Description = null,
    string? Author = null,
    string? AuthorEmail = null,
    string? HomePage = null,
    string? License = null,
    IReadOnlyList<string>? RequiresPython = null)
```

**Purpose**: Package metadata from PyPI

### PyPISearchResult

```csharp
public record PyPISearchResult(
    string Name,
    string Version,
    string? Summary = null)
```

**Purpose**: Simplified search result from PyPI

### PipConfiguration

```csharp
public record PipConfiguration(
    string? IndexUrl = null,
    string? TrustedHost = null,
    string? Proxy = null)
```

**Purpose**: Pip configuration information

### ManagerConfiguration

```csharp
public class ManagerConfiguration
{
    public string? DefaultPythonVersion { get; set; }
    public string? DefaultPipIndexUrl { get; set; }
    public string? ProxyUrl { get; set; }
    public TimeSpan? DefaultTimeout { get; set; }
    public int RetryAttempts { get; set; } = 3;
    public TimeSpan RetryDelay { get; set; } = TimeSpan.FromSeconds(1);
    public bool UseExponentialBackoff { get; set; } = true;
}
```

**Purpose**: Configuration settings for Python managers

**Default Values**:
- `DefaultPythonVersion`: "3.12"
- `DefaultPipIndexUrl`: "https://pypi.org/simple/"
- `RetryAttempts`: 3
- `RetryDelay`: 1 second
- `UseExponentialBackoff`: true

### InstanceMetadata

```csharp
public class InstanceMetadata
{
    public string PythonVersion { get; set; }
    public DateTime BuildDate { get; set; }
    public bool WasLatestBuild { get; set; }
    public DateTime InstallationDate { get; set; }
    public List<VirtualEnvironmentMetadata> VirtualEnvironments { get; set; }
    public string Directory { get; } // Read-only
    
    // Methods
    public VirtualEnvironmentMetadata? GetVirtualEnvironment(string name);
    public void SetVirtualEnvironment(VirtualEnvironmentMetadata metadata);
    public bool RemoveVirtualEnvironment(string name);
}
```

**Purpose**: Metadata about a Python runtime instance and its managed virtual environments

### VirtualEnvironmentMetadata

```csharp
public class VirtualEnvironmentMetadata
{
    public string Name { get; set; }
    public string? ExternalPath { get; set; }
    public DateTime CreatedDate { get; set; }
    public bool IsExternal { get; } // Computed from ExternalPath
    
    // Methods
    public string GetResolvedPath(string defaultPath);
}
```

**Purpose**: Metadata about a virtual environment, stored within InstanceMetadata

## Exceptions

All exceptions inherit from standard .NET exceptions. Key exceptions:

### PythonInstallationException
Base exception for installation-related errors. Inherits from `Exception`.

### InstanceNotFoundException
Thrown when a Python instance is not found. Inherits from `PythonInstallationException`.

**Properties**:
- `PythonVersion` (string)
- `BuildDate` (DateTime?)

### PythonExecutionException
Thrown when Python execution fails. Inherits from `Exception`.

**Properties**:
- `ExitCode` (int?)
- `StandardError` (string?)

### PackageInstallationException
Thrown when package installation fails. Inherits from `Exception`.

**Properties**:
- `PackageSpecification` (string)
- `InstallationOutput` (string)

### RequirementsFileException
Thrown when requirements file installation fails. Inherits from `PackageInstallationException`.

### VirtualEnvironmentNotFoundException
Thrown when a virtual environment is not found. Inherits from `Exception`.

**Properties**:
- `VirtualEnvironmentName` (string)

### PlatformNotSupportedException
Thrown when the platform is not supported. Inherits from `PythonInstallationException`.

### PythonNotInstalledException
Thrown when Python installation is missing or invalid. Inherits from `PythonInstallationException`.

### InvalidPackageSpecificationException
Thrown when a package specification is invalid. Inherits from `PackageInstallationException`.

### InvalidPythonVersionException
Thrown when a Python version string is invalid. Inherits from `PythonInstallationException`.

### MetadataCorruptedException
Thrown when instance metadata is corrupted. Inherits from `PythonInstallationException`.

### PythonNetExecutionException
Thrown when Python.NET execution fails. Inherits from `PythonExecutionException`.

### PythonNetInitializationException
Thrown when Python.NET initialization fails. Inherits from `Exception`.

## Concrete Implementations

### PythonManager

**Constructor**:
```csharp
public PythonManager(
    string directory,
    GitHubClient githubClient,
    ILogger<PythonManager>? logger = null,
    ILoggerFactory? loggerFactory = null,
    IMemoryCache? cache = null,
    ManagerConfiguration? configuration = null)
```

**Purpose**: Subprocess-based Python execution manager

**Returns**: `PythonRootRuntime` instances from `GetOrCreateInstanceAsync`

### PythonNetManager

**Constructor**:
```csharp
public PythonNetManager(
    string directory,
    GitHubClient githubClient,
    ILogger<PythonNetManager>? logger = null,
    ILoggerFactory? loggerFactory = null,
    IMemoryCache? cache = null,
    ManagerConfiguration? configuration = null)
```

**Purpose**: Python.NET in-process execution manager

**Returns**: `PythonNetRootRuntime` instances from `GetOrCreateInstanceAsync`

**Note**: Implements `IDisposable` - dispose to clean up Python.NET runtime

### PythonRootRuntime

**Purpose**: Subprocess-based root runtime. Can create `PythonRootVirtualEnvironment` instances.

### PythonNetRootRuntime

**Purpose**: Python.NET root runtime. Can create `PythonNetVirtualEnvironment` instances.

**Note**: Implements `IDisposable`

### PythonRootVirtualEnvironment

**Purpose**: Subprocess-based virtual environment runtime

### PythonNetVirtualEnvironment

**Purpose**: Python.NET virtual environment runtime

**Note**: Implements `IDisposable`

## Common Usage Patterns

### Pattern 1: Basic Python Execution

```csharp
var manager = new PythonManager("./python", githubClient);
var runtime = await manager.GetOrCreateInstanceAsync("3.12");
var result = await runtime.ExecuteCommandAsync("print('Hello')");
```

### Pattern 2: Install and Use Packages

```csharp
var runtime = await manager.GetOrCreateInstanceAsync("3.12");
await runtime.InstallPackageAsync("numpy");
var result = await runtime.ExecuteCommandAsync("import numpy; print(numpy.__version__)");
```

### Pattern 3: Virtual Environment

```csharp
var rootRuntime = await manager.GetOrCreateInstanceAsync("3.12") as BasePythonRootRuntime;
var venv = await rootRuntime.GetOrCreateVirtualEnvironmentAsync("myproject");
await venv.InstallPackageAsync("requests");
var result = await venv.ExecuteCommandAsync("import requests; print(requests.__version__)");
```

### Pattern 3b: External Virtual Environment

```csharp
var rootRuntime = await manager.GetOrCreateInstanceAsync("3.12") as BasePythonRootRuntime;

// Create venv at project-specific location
var venv = await rootRuntime.GetOrCreateVirtualEnvironmentAsync(
    "projectenv",
    externalPath: "/path/to/myproject/.venv");

// Check if it's external
var metadata = rootRuntime.GetVirtualEnvironmentMetadata("projectenv");
Console.WriteLine($"Is external: {metadata?.IsExternal}");  // True

// Resolve actual path
var path = rootRuntime.ResolveVirtualEnvironmentPath("projectenv");
```

### Pattern 4: Requirements File

```csharp
var runtime = await manager.GetOrCreateInstanceAsync("3.12");
await runtime.InstallRequirementsAsync("requirements.txt");
```

### Pattern 5: Health Check

```csharp
var health = await runtime.ValidatePythonInstallationAsync();
if (health["OverallHealth"] as string == "Healthy")
{
    // Proceed with operations
}
```

### Pattern 6: Error Handling

```csharp
try
{
    await runtime.InstallPackageAsync("nonexistent-package-xyz");
}
catch (PackageInstallationException ex)
{
    Console.WriteLine($"Failed: {ex.Message}");
    Console.WriteLine($"Output: {ex.InstallationOutput}");
}
```

## Important Notes for AI Models

1. **Async/Sync Methods**: All async methods have synchronous counterparts (same name without "Async" suffix) that block until completion.

2. **Cancellation Tokens**: All async methods accept `CancellationToken` for cancellation support.

3. **Null Returns**: Methods that return nullable types (`string?`, `PackageInfo?`, etc.) return `null` when the requested item is not found, rather than throwing exceptions.

4. **Exception Handling**: Most operations throw specific exceptions (e.g., `PackageInstallationException`) with additional context properties.

5. **Virtual Environments**: Virtual environments are isolated - packages installed in one venv are not available in others or the root runtime. They can be created at external paths using the `externalPath` parameter.

6. **Python.NET vs Subprocess**: `PythonNetManager` provides in-process execution (faster, but requires proper disposal), while `PythonManager` uses subprocess execution (more isolated, slower).

7. **Version Matching**: Partial versions (e.g., "3.12") automatically match the latest patch version. Exact versions (e.g., "3.12.5") match exactly.

8. **Working Directory**: Defaults to the runtime's working directory, but can be overridden in execution methods.

9. **Stream Handlers**: `stdinHandler`, `stdoutHandler`, and `stderrHandler` allow real-time interaction with Python processes.

10. **Batch Operations**: `InstallPackagesAsync` and `UninstallPackagesAsync` support parallel execution, but failures are captured in the result dictionary rather than throwing immediately.

11. **Package Manager (uv)**: All package operations use `uv` instead of pip for significantly faster performance. `uv` is automatically installed when runtime instances are created.

12. **External Virtual Environments**: Virtual environments can be created at custom locations using `externalPath`. The metadata is stored centrally in `InstanceMetadata.VirtualEnvironments`, and the venv is tracked by name regardless of physical location.

13. **Duplicate Venv Names**: Virtual environment names must be unique per runtime instance, even if one is at an external location. Creating a venv with a name that already exists will throw `InvalidOperationException` unless `recreateIfExists` is true.


