# BasePythonRuntime

Abstract base class for Python runtime implementations. Provides common functionality for executing Python commands, scripts, and managing packages using [uv](https://github.com/astral-sh/uv).

## Namespace

`PythonEmbedded.Net`

## Inheritance

```
BasePythonRuntime (abstract)
├── BasePythonRootRuntime (abstract)
│   ├── PythonRootRuntime
│   └── PythonNetRootRuntime
└── BasePythonVirtualRuntime (abstract)
    ├── PythonRootVirtualEnvironment
    └── PythonNetVirtualEnvironment
```

## Package Manager (uv)

This library uses [uv](https://github.com/astral-sh/uv) as its package manager, which provides significantly faster package operations compared to pip. `uv` is automatically installed when runtime instances and virtual environments are created.

### uv Properties

```csharp
public bool IsUvAvailable { get; }
```

Gets whether `uv` is available and ready to use.

```csharp
public string? UvExecutablePath { get; }
```

Gets the path to the `uv` executable, or null if not available.

### uv Methods

```csharp
public async Task DetectUvAsync(CancellationToken cancellationToken = default)
```

Detects if `uv` is available (checks both PATH and local installation).

```csharp
public async Task EnsureUvInstalledAsync(CancellationToken cancellationToken = default)
```

Ensures `uv` is installed, installing it if necessary. This is called automatically when runtime instances are created.

## Protected Abstract Properties

### PythonExecutablePath

```csharp
protected abstract string PythonExecutablePath { get; }
```

Gets the Python executable path for this runtime.

### WorkingDirectory

```csharp
protected abstract string WorkingDirectory { get; }
```

Gets the working directory for this runtime.

### Logger

```csharp
protected abstract ILogger? Logger { get; }
```

Gets the logger for this runtime.

## Protected Abstract Methods

### ValidateInstallation

```csharp
protected abstract void ValidateInstallation();
```

Validates that the Python installation is complete and valid. Must be implemented by derived classes.

## Public Methods

### ExecuteCommandAsync

```csharp
public async Task<PythonExecutionResult> ExecuteCommandAsync(
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
- `command` - The Python command to execute (e.g., `"-c 'print(\"Hello\")'"`)
- `stdinHandler` - Optional handler for providing stdin input line by line
- `stdoutHandler` - Optional handler for processing stdout output line by line
- `stderrHandler` - Optional handler for processing stderr output line by line
- `cancellationToken` - Cancellation token
- `workingDirectory` - Optional working directory override
- `environmentVariables` - Optional environment variables to set
- `priority` - Optional process priority
- `maxMemoryMB` - Optional maximum memory limit in MB
- `timeout` - Optional per-execution timeout

**Returns:**
- `Task<PythonExecutionResult>` - The execution result. See [PythonExecutionResult](../Records/PythonExecutionResult.md)

**Example:**

```csharp
var result = await runtime.ExecuteCommandAsync("-c \"print('Hello, World!')\"");
Console.WriteLine(result.StandardOutput); // "Hello, World!"
```

### ExecuteCommand

```csharp
public PythonExecutionResult ExecuteCommand(
    string command,
    Func<string?>? stdinHandler = null,
    Action<string>? stdoutHandler = null,
    Action<string>? stderrHandler = null)
```

Synchronous version of ExecuteCommandAsync.

### ExecuteScriptAsync

```csharp
public async Task<PythonExecutionResult> ExecuteScriptAsync(
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

**Parameters:**
- `scriptPath` - The path to the Python script file
- `arguments` - Optional arguments to pass to the script
- Other parameters same as ExecuteCommandAsync

**Returns:**
- `Task<PythonExecutionResult>` - The execution result

**Example:**

```csharp
var result = await runtime.ExecuteScriptAsync("script.py", new[] { "arg1", "arg2" });
```

### InstallPackageAsync

```csharp
public async Task<PythonExecutionResult> InstallPackageAsync(
    string packageSpecification,
    bool upgrade = false,
    string? indexUrl = null,
    CancellationToken cancellationToken = default)
```

Installs a Python package using `uv`.

**Parameters:**
- `packageSpecification` - Package specification (e.g., "requests==2.31.0", "numpy>=1.20.0")
- `upgrade` - Whether to upgrade the package if already installed
- `indexUrl` - Optional custom PyPI index URL
- `cancellationToken` - Cancellation token

**Returns:**
- `Task<PythonExecutionResult>` - The execution result from uv

**Exceptions:**
- `InvalidOperationException` - If uv is not available
- [PackageInstallationException](../Exceptions/PackageInstallationException.md) - When installation fails

**Example:**

```csharp
// Install a package
await runtime.InstallPackageAsync("numpy");

// Install with version constraint
await runtime.InstallPackageAsync("requests==2.31.0");

// Install and upgrade if exists
await runtime.InstallPackageAsync("pandas", upgrade: true);
```

### InstallRequirementsAsync

```csharp
public async Task<PythonExecutionResult> InstallRequirementsAsync(
    string requirementsFilePath,
    bool upgrade = false,
    CancellationToken cancellationToken = default)
```

Installs packages from a requirements.txt file using `uv`.

**Parameters:**
- `requirementsFilePath` - Path to requirements.txt file
- `upgrade` - Whether to upgrade packages
- `cancellationToken` - Cancellation token

**Returns:**
- `Task<PythonExecutionResult>` - The execution result

**Exceptions:**
- [RequirementsFileException](../Exceptions/RequirementsFileException.md) - When requirements file is invalid

### InstallPyProjectAsync

```csharp
public async Task<PythonExecutionResult> InstallPyProjectAsync(
    string pyProjectFilePath,
    bool editable = false,
    CancellationToken cancellationToken = default)
```

Installs a Python package from a pyproject.toml file using `uv`.

**Parameters:**
- `pyProjectFilePath` - The path to the directory containing pyproject.toml or the file itself
- `editable` - Whether to install in editable mode
- `cancellationToken` - Cancellation token

**Returns:**
- `Task<PythonExecutionResult>` - The execution result

### UninstallPackageAsync

```csharp
public async Task<PythonExecutionResult> UninstallPackageAsync(
    string packageName,
    bool removeDependencies = false,
    CancellationToken cancellationToken = default)
```

Uninstalls a Python package using `uv`.

**Parameters:**
- `packageName` - Name of the package to uninstall
- `removeDependencies` - Whether to also remove dependencies (not used by uv)
- `cancellationToken` - Cancellation token

**Returns:**
- `Task<PythonExecutionResult>` - The execution result

### ListInstalledPackagesAsync

```csharp
public async Task<IReadOnlyList<PackageInfo>> ListInstalledPackagesAsync(
    CancellationToken cancellationToken = default)
```

Lists all installed packages using `uv pip list`.

**Returns:**
- `Task<IReadOnlyList<PackageInfo>>` - List of installed packages. See [PackageInfo](../Models/PackageInfo.md)

### GetPackageVersionAsync

```csharp
public async Task<string?> GetPackageVersionAsync(
    string packageName,
    CancellationToken cancellationToken = default)
```

Gets the installed version of a package using `importlib.metadata`.

**Parameters:**
- `packageName` - Name of the package

**Returns:**
- `Task<string?>` - Package version, or null if not installed

### IsPackageInstalledAsync

```csharp
public async Task<bool> IsPackageInstalledAsync(
    string packageName,
    CancellationToken cancellationToken = default)
```

Checks if a package is installed using `uv pip show`.

**Parameters:**
- `packageName` - Name of the package

**Returns:**
- `Task<bool>` - True if installed, false otherwise

### GetPackageInfoAsync

```csharp
public async Task<PackageInfo?> GetPackageInfoAsync(
    string packageName,
    CancellationToken cancellationToken = default)
```

Gets detailed information about an installed package using `importlib.metadata`.

**Parameters:**
- `packageName` - Name of the package

**Returns:**
- `Task<PackageInfo?>` - Package information, or null if not installed

### ListOutdatedPackagesAsync

```csharp
public async Task<IReadOnlyList<OutdatedPackageInfo>> ListOutdatedPackagesAsync(
    CancellationToken cancellationToken = default)
```

Lists packages that have available updates using `uv pip list --outdated`.

**Returns:**
- `Task<IReadOnlyList<OutdatedPackageInfo>>` - List of outdated packages

### ExportRequirementsAsync

```csharp
public async Task<PythonExecutionResult> ExportRequirementsAsync(
    string outputPath,
    CancellationToken cancellationToken = default)
```

Exports installed packages to a requirements.txt file using `uv pip freeze`.

**Parameters:**
- `outputPath` - The path where to write the requirements.txt file
- `cancellationToken` - Cancellation token

**Returns:**
- `Task<PythonExecutionResult>` - The execution result

### ExportRequirementsFreezeAsync

```csharp
public async Task<PythonExecutionResult> ExportRequirementsFreezeAsync(
    string outputPath,
    CancellationToken cancellationToken = default)
```

Exports installed packages with exact versions to a requirements.txt file using `uv pip freeze`.

### ExportRequirementsFreezeToStringAsync

```csharp
public async Task<string> ExportRequirementsFreezeToStringAsync(
    CancellationToken cancellationToken = default)
```

Exports installed packages as a requirements.txt string using `uv pip freeze`.

**Returns:**
- `Task<string>` - The requirements.txt content as a string

### InstallPackagesAsync

```csharp
public async Task<Dictionary<string, PythonExecutionResult>> InstallPackagesAsync(
    IEnumerable<string> packages,
    bool parallel = false,
    bool upgrade = false,
    CancellationToken cancellationToken = default)
```

Installs multiple packages in batch.

**Parameters:**
- `packages` - The list of package specifications to install
- `parallel` - Whether to install packages in parallel
- `upgrade` - Whether to upgrade packages if already installed
- `cancellationToken` - Cancellation token

**Returns:**
- `Task<Dictionary<string, PythonExecutionResult>>` - Dictionary mapping package names to results

### UninstallPackagesAsync

```csharp
public async Task<Dictionary<string, PythonExecutionResult>> UninstallPackagesAsync(
    IEnumerable<string> packages,
    bool parallel = false,
    bool removeDependencies = false,
    CancellationToken cancellationToken = default)
```

Uninstalls multiple packages in batch.

### UpgradeAllPackagesAsync

```csharp
public async Task<PythonExecutionResult> UpgradeAllPackagesAsync(
    CancellationToken cancellationToken = default)
```

Upgrades all installed packages.

### DowngradePackageAsync

```csharp
public async Task<PythonExecutionResult> DowngradePackageAsync(
    string packageName,
    string targetVersion,
    CancellationToken cancellationToken = default)
```

Downgrades a package to a specific version.

### GetPythonVersionInfoAsync

```csharp
public async Task<string> GetPythonVersionInfoAsync(
    CancellationToken cancellationToken = default)
```

Gets detailed Python version information.

### SearchPackagesAsync

```csharp
public async Task<IReadOnlyList<PyPISearchResult>> SearchPackagesAsync(
    string query,
    CancellationToken cancellationToken = default)
```

Searches PyPI for packages matching the query.

**Parameters:**
- `query` - Search query

**Returns:**
- `Task<IReadOnlyList<PyPISearchResult>>` - List of search results

### GetPackageMetadataAsync

```csharp
public async Task<PyPIPackageInfo?> GetPackageMetadataAsync(
    string packageName,
    string? version = null,
    CancellationToken cancellationToken = default)
```

Gets package metadata from PyPI.

**Parameters:**
- `packageName` - The name of the package
- `version` - Optional version. If not specified, returns the latest version

**Returns:**
- `Task<PyPIPackageInfo?>` - Package metadata, or null if not found

### ValidatePythonInstallationAsync

```csharp
public async Task<Dictionary<string, object>> ValidatePythonInstallationAsync(
    CancellationToken cancellationToken = default)
```

Performs a comprehensive health check of the Python installation.

**Returns:**
- `Task<Dictionary<string, object>>` - Dictionary containing health check results including:
  - `ExecutableExists` - Whether the Python executable exists
  - `WorkingDirectoryExists` - Whether the working directory exists
  - `PythonVersionCheck` - Status of Python version check
  - `UvCheck` - Status of uv availability
  - `CommandExecution` - Status of command execution test
  - `OverallHealth` - Overall health status ("Healthy" or "Unhealthy")

## Static Methods

### ValidatePythonVersionString

```csharp
public static bool ValidatePythonVersionString(string version)
```

Validates that a Python version string is in a valid format.

### ValidatePackageSpecification

```csharp
public static bool ValidatePackageSpecification(string packageSpec)
```

Validates that a package specification is in a valid format.

## Related Types

- [BasePythonRootRuntime](./BasePythonRootRuntime.md) - Base class for root runtimes
- [BasePythonVirtualRuntime](./BasePythonVirtualRuntime.md) - Base class for virtual environment runtimes
- [PythonExecutionResult](../Records/PythonExecutionResult.md) - Execution result record
- [PackageInfo](../Models/PackageInfo.md) - Package information model
