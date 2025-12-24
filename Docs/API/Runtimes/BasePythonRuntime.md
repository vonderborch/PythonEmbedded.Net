# BasePythonRuntime

Abstract base class for Python runtime implementations. Provides common functionality for executing Python commands, scripts, and managing packages.

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
public async Task InstallPackageAsync(
    string packageSpecification,
    CancellationToken cancellationToken = default,
    PipConfiguration? pipConfig = null)
```

Installs a Python package using pip.

**Parameters:**
- `packageSpecification` - Package specification (e.g., "requests==2.31.0", "numpy>=1.20.0")
- `cancellationToken` - Cancellation token
- `pipConfig` - Optional pip configuration. See [PipConfiguration](../Models/PipConfiguration.md)

**Exceptions:**
- [PackageInstallationException](../Exceptions/PackageInstallationException.md) - When installation fails

### UninstallPackageAsync

```csharp
public async Task UninstallPackageAsync(
    string packageName,
    CancellationToken cancellationToken = default)
```

Uninstalls a Python package.

**Parameters:**
- `packageName` - Name of the package to uninstall
- `cancellationToken` - Cancellation token

### ListInstalledPackages

```csharp
public IReadOnlyList<PackageInfo> ListInstalledPackages()
```

Lists all installed packages. Returns a list of [PackageInfo](../Models/PackageInfo.md) records.

### GetPackageVersion

```csharp
public string? GetPackageVersion(string packageName)
```

Gets the installed version of a package.

**Parameters:**
- `packageName` - Name of the package

**Returns:**
- `string?` - Package version, or null if not installed

### IsPackageInstalled

```csharp
public bool IsPackageInstalled(string packageName)
```

Checks if a package is installed.

**Parameters:**
- `packageName` - Name of the package

**Returns:**
- `bool` - True if installed, false otherwise

### InstallRequirementsAsync

```csharp
public async Task InstallRequirementsAsync(
    string requirementsFilePath,
    CancellationToken cancellationToken = default,
    PipConfiguration? pipConfig = null)
```

Installs packages from a requirements.txt file.

**Parameters:**
- `requirementsFilePath` - Path to requirements.txt file
- `cancellationToken` - Cancellation token
- `pipConfig` - Optional pip configuration

**Exceptions:**
- [RequirementsFileException](../Exceptions/RequirementsFileException.md) - When requirements file is invalid

### GetPythonVersionInfo

```csharp
public string GetPythonVersionInfo()
```

Gets detailed Python version information.

**Returns:**
- `string` - Python version information

### GetPipVersion

```csharp
public string GetPipVersion()
```

Gets the pip version.

**Returns:**
- `string` - Pip version

### ValidatePythonInstallationAsync

```csharp
public async Task<Dictionary<string, object>> ValidatePythonInstallationAsync(
    CancellationToken cancellationToken = default)
```

Performs a comprehensive health check of the Python installation.

**Returns:**
- `Task<Dictionary<string, object>>` - Dictionary containing health check results

### SearchPackages

```csharp
public IReadOnlyList<PyPISearchResult> SearchPackages(string query)
```

Searches PyPI for packages matching the query.

**Parameters:**
- `query` - Search query

**Returns:**
- `IReadOnlyList<PyPISearchResult>` - List of search results. See [PyPISearchResult](../Models/PyPIPackageInfo.md)

## Related Types

- [BasePythonRootRuntime](./BasePythonRootRuntime.md) - Base class for root runtimes
- [BasePythonVirtualRuntime](./BasePythonVirtualRuntime.md) - Base class for virtual environment runtimes
- [PythonExecutionResult](../Records/PythonExecutionResult.md) - Execution result record
- [PackageInfo](../Models/PackageInfo.md) - Package information model



