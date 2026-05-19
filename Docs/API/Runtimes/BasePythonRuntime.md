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

## Package Manager (uv and pip)

By default, package operations use [uv](https://github.com/astral-sh/uv) (`useUv: true`). Pass `useUv: false` to use `python -m pip`. When `useUv` is `true` on manager/root creation, `EnsureUvInstalledAsync` runs on the runtime.

**uv detection order:** executable adjacent to this runtime's Python → `GetAdditionalUvCandidatePaths()` (for venvs: `pyvenv.cfg` `home =` base interpreter paths) → common install dirs → `PATH`.

### uv Properties

```csharp
public bool IsUvAvailable { get; }
public string? UvPath { get; }
```

### uv Methods

```csharp
public async Task<bool> DetectUvAsync(string? customPath = null, CancellationToken cancellationToken = default)
public async Task<bool> EnsureUvInstalledAsync(CancellationToken cancellationToken = default)
```

`EnsureUvInstalledAsync` attempts `pip install uv` if detection fails, then re-detects.

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
    bool useUv = true,
    CancellationToken cancellationToken = default)
```

Installs a Python package via uv or pip.

**Parameters:**
- `packageSpecification` - Package specification (e.g., "requests==2.31.0", "numpy>=1.20.0")
- `upgrade` - Whether to upgrade the package if already installed
- `indexUrl` - Optional custom PyPI index URL
- `useUv` - When `true` (default), uv; when `false`, `python -m pip`
- `cancellationToken` - Cancellation token

**Exceptions:**
- `InvalidOperationException` - If the selected package manager is not available
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
    bool useUv = true,
    CancellationToken cancellationToken = default)
```

Installs packages from a requirements.txt file.

**Exceptions:**
- [RequirementsFileException](../Exceptions/RequirementsFileException.md) - When installation or parsing fails

### CheckRequirementsAsync

```csharp
public async Task<IReadOnlyList<RequirementStatus>> CheckRequirementsAsync(
    string requirementsFilePath,
    CancellationToken cancellationToken = default)
```

Checks each requirements line for install/version satisfaction (embedded Python script).

### GetMissingPackagesAsync

```csharp
public async Task<string[]> GetMissingPackagesAsync(
    string[] packageNames,
    CancellationToken cancellationToken = default)
```

Returns package names not importable via `importlib.util.find_spec` (single Python invocation).

### InstallPyProjectAsync

```csharp
public async Task<PythonExecutionResult> InstallPyProjectAsync(
    string pyProjectFilePath,
    bool editable = false,
    bool useUv = true,
    CancellationToken cancellationToken = default)
```

Installs a Python package from a pyproject.toml file.

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
    bool useUv = true,
    CancellationToken cancellationToken = default)
```

Uninstalls a Python package (`pip uninstall -y`).

### ListInstalledPackagesAsync

```csharp
public async Task<IReadOnlyList<PackageInfo>> ListInstalledPackagesAsync(
    bool useUv = true,
    CancellationToken cancellationToken = default)
```

Lists all installed packages (`pip list --format=json`).

**Returns:**
- `Task<IReadOnlyList<PackageInfo>>` - List of installed packages. See [PackageInfo](../Models/PackageInfo.md)

### GetPackageVersionAsync

```csharp
public async Task<string?> GetPackageVersionAsync(
    string packageName,
    bool useUv = true,
    CancellationToken cancellationToken = default)
```

Gets the installed version via `pip show`.

**Parameters:**
- `packageName` - Name of the package

**Returns:**
- `Task<string?>` - Package version, or null if not installed

### IsPackageInstalledAsync

```csharp
public async Task<bool> IsPackageInstalledAsync(
    string packageName,
    bool useUv = true,
    CancellationToken cancellationToken = default)
```

Checks if a package is installed (`pip show` exit code).

**Parameters:**
- `packageName` - Name of the package

**Returns:**
- `Task<bool>` - True if installed, false otherwise

### GetPackageInfoAsync

```csharp
public async Task<PackageInfo?> GetPackageInfoAsync(
    string packageName,
    bool useUv = true,
    CancellationToken cancellationToken = default)
```

Gets detailed information about an installed package (parsed from `pip show`).

**Parameters:**
- `packageName` - Name of the package

**Returns:**
- `Task<PackageInfo?>` - Package information, or null if not installed

### ListOutdatedPackagesAsync

```csharp
public async Task<IReadOnlyList<OutdatedPackageInfo>> ListOutdatedPackagesAsync(
    bool useUv = true,
    CancellationToken cancellationToken = default)
```

Lists packages that have available updates (`pip list --outdated --format=json`). Accepts `bool useUv = true`.

**Returns:**
- `Task<IReadOnlyList<OutdatedPackageInfo>>` - List of outdated packages

### ExportRequirementsAsync

```csharp
public async Task<PythonExecutionResult> ExportRequirementsAsync(
    string outputPath,
    bool useUv = true,
    CancellationToken cancellationToken = default)
```

Writes `pip freeze` output to `outputPath`. Accepts `bool useUv = true`.

### ExportRequirementsFreezeAsync / ExportRequirementsFreezeToStringAsync

Same freeze output to file or string. Accepts `bool useUv = true`.

**Returns:**
- `Task<string>` - The requirements.txt content as a string

### InstallPackagesAsync

```csharp
public async Task<Dictionary<string, PythonExecutionResult>> InstallPackagesAsync(
    IEnumerable<string> packages,
    bool parallel = false,
    bool upgrade = false,
    bool useUv = true,
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
    bool useUv = true,
    CancellationToken cancellationToken = default)
```

Uninstalls multiple packages in batch.

### UpgradeAllPackagesAsync

```csharp
public async Task<PythonExecutionResult> UpgradeAllPackagesAsync(
    bool useUv = true,
    CancellationToken cancellationToken = default)
```

Upgrades all installed packages.

### DowngradePackageAsync

```csharp
public async Task<PythonExecutionResult> DowngradePackageAsync(
    string packageName,
    string targetVersion,
    bool useUv = true,
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
  - `PipCheck` - Status of pip availability
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
