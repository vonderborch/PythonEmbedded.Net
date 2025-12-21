# API Reference

This document provides detailed API reference for PythonEmbedded.Net.

## Table of Contents

- [Interfaces](#interfaces)
  - [IPythonManager](#ipythonmanager)
  - [IPythonRuntime](#ipythonruntime)
  - [IPythonRootRuntime](#ipythonrootruntime)
  - [IPythonVirtualRuntime](#ipythonvirtualruntime)
- [Classes](#classes)
  - [PythonManager](#pythonmanager)
  - [PythonNetManager](#pythonnetmanager)
  - [PythonExecutionResult](#pythonexecutionresult)
  - [InstanceMetadata](#instancemetadata)
- [Exceptions](#exceptions)

## Interfaces

### IPythonManager

Interface for Python manager implementations.

#### Methods

##### GetOrCreateInstanceAsync

```csharp
Task<IPythonRuntime> GetOrCreateInstanceAsync(
    string pythonVersion,
    string? buildDate = null,
    CancellationToken cancellationToken = default)
```

Gets or creates a Python runtime instance for the specified version. Downloads and installs the Python distribution if it doesn't exist.

**Parameters:**
- `pythonVersion`: The Python version (e.g., "3.12.0", "3.11")
- `buildDate`: Optional build date in YYYYMMDD format. If null, uses the latest build.
- `cancellationToken`: Cancellation token.

**Returns:** A task that represents the asynchronous operation. The result is an `IPythonRuntime` instance.

**Exceptions:**
- `ArgumentException`: If `pythonVersion` is null or empty.
- `InstanceNotFoundException`: If the specified version/build date combination cannot be found.

##### GetOrCreateInstance

```csharp
IPythonRuntime GetOrCreateInstance(
    string pythonVersion,
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

##### DeleteInstance

```csharp
bool DeleteInstance(string pythonVersion, string? buildDate = null)
```

Synchronous version of `DeleteInstanceAsync`.

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

##### ListAvailableVersions

```csharp
IReadOnlyList<string> ListAvailableVersions(string? releaseTag = null)
```

Synchronous version of `ListAvailableVersionsAsync`.

---

### IPythonRuntime

Interface for Python runtime implementations, providing functionality for executing commands, scripts, and installing packages.

#### Methods

##### ExecuteCommandAsync

```csharp
Task<PythonExecutionResult> ExecuteCommandAsync(
    string command,
    Func<string?>? stdinHandler = null,
    Action<string>? stdoutHandler = null,
    Action<string>? stderrHandler = null,
    CancellationToken cancellationToken = default)
```

Executes a Python command and returns the result.

**Parameters:**
- `command`: The Python command to execute (e.g., "print('Hello')")
- `stdinHandler`: Optional handler for providing stdin input line by line. Return null to end input.
- `stdoutHandler`: Optional handler for processing stdout output line by line.
- `stderrHandler`: Optional handler for processing stderr output line by line.
- `cancellationToken`: Cancellation token.

**Returns:** A task that represents the asynchronous operation. The result contains exit code, stdout, and stderr.

**Example:**
```csharp
var result = await runtime.ExecuteCommandAsync("print('Hello, World!')");
Console.WriteLine(result.StandardOutput);
```

##### ExecuteCommand

```csharp
PythonExecutionResult ExecuteCommand(
    string command,
    Func<string?>? stdinHandler = null,
    Action<string>? stdoutHandler = null,
    Action<string>? stderrHandler = null)
```

Synchronous version of `ExecuteCommandAsync`.

##### ExecuteScriptAsync

```csharp
Task<PythonExecutionResult> ExecuteScriptAsync(
    string scriptPath,
    IEnumerable<string>? arguments = null,
    Func<string?>? stdinHandler = null,
    Action<string>? stdoutHandler = null,
    Action<string>? stderrHandler = null,
    CancellationToken cancellationToken = default)
```

Executes a Python script file and returns the result.

**Parameters:**
- `scriptPath`: The path to the Python script file to execute.
- `arguments`: Optional arguments to pass to the script.
- `stdinHandler`: Optional handler for providing stdin input.
- `stdoutHandler`: Optional handler for processing stdout output.
- `stderrHandler`: Optional handler for processing stderr output.
- `cancellationToken`: Cancellation token.

**Returns:** A task that represents the asynchronous operation. The result contains exit code, stdout, and stderr.

##### ExecuteScript

```csharp
PythonExecutionResult ExecuteScript(
    string scriptPath,
    IEnumerable<string>? arguments = null,
    Func<string?>? stdinHandler = null,
    Action<string>? stdoutHandler = null,
    Action<string>? stderrHandler = null)
```

Synchronous version of `ExecuteScriptAsync`.

##### InstallPackageAsync

```csharp
Task<PythonExecutionResult> InstallPackageAsync(
    string packageSpecification,
    bool upgrade = false,
    CancellationToken cancellationToken = default)
```

Installs a Python package using pip.

**Parameters:**
- `packageSpecification`: The package specification (e.g., "numpy", "requests==2.31.0", "numpy>=1.20.0").
- `upgrade`: Whether to upgrade the package if it's already installed.
- `cancellationToken`: Cancellation token.

**Returns:** A task that represents the asynchronous operation. The result contains pip output.

**Throws:**
- `PackageInstallationException`: If package installation fails.

##### InstallPackage

```csharp
PythonExecutionResult InstallPackage(
    string packageSpecification,
    bool upgrade = false)
```

Synchronous version of `InstallPackageAsync`.

##### InstallRequirementsAsync

```csharp
Task<PythonExecutionResult> InstallRequirementsAsync(
    string requirementsFilePath,
    bool upgrade = false,
    CancellationToken cancellationToken = default)
```

Installs Python packages from a requirements.txt file.

**Parameters:**
- `requirementsFilePath`: The path to the requirements.txt file.
- `upgrade`: Whether to upgrade packages if they're already installed.
- `cancellationToken`: Cancellation token.

**Returns:** A task that represents the asynchronous operation.

**Throws:**
- `RequirementsFileException`: If installation fails.

##### InstallRequirements

```csharp
PythonExecutionResult InstallRequirements(
    string requirementsFilePath,
    bool upgrade = false)
```

Synchronous version of `InstallRequirementsAsync`.

##### InstallPyProjectAsync

```csharp
Task<PythonExecutionResult> InstallPyProjectAsync(
    string pyProjectFilePath,
    bool editable = false,
    CancellationToken cancellationToken = default)
```

Installs a Python package from a pyproject.toml file.

**Parameters:**
- `pyProjectFilePath`: The path to the directory containing pyproject.toml or the pyproject.toml file itself.
- `editable`: Whether to install in editable mode (pip install -e).
- `cancellationToken`: Cancellation token.

**Returns:** A task that represents the asynchronous operation.

##### InstallPyProject

```csharp
PythonExecutionResult InstallPyProject(
    string pyProjectFilePath,
    bool editable = false)
```

Synchronous version of `InstallPyProjectAsync`.

---

### IPythonRootRuntime

Interface for Python root runtime implementations that can manage virtual environments. Extends `IPythonRuntime`.

#### Methods

##### GetOrCreateVirtualEnvironmentAsync

```csharp
Task<IPythonVirtualRuntime> GetOrCreateVirtualEnvironmentAsync(
    string name,
    bool recreateIfExists = false,
    CancellationToken cancellationToken = default)
```

Gets or creates a virtual environment with the specified name.

**Parameters:**
- `name`: The name of the virtual environment.
- `recreateIfExists`: Whether to recreate the virtual environment if it already exists.
- `cancellationToken`: Cancellation token.

**Returns:** A task that represents the asynchronous operation. The result is an `IPythonVirtualRuntime` instance.

**Throws:**
- `ArgumentException`: If `name` is null or empty.
- `PythonInstallationException`: If virtual environment creation fails.

##### GetOrCreateVirtualEnvironment

```csharp
IPythonVirtualRuntime GetOrCreateVirtualEnvironment(
    string name,
    bool recreateIfExists = false)
```

Synchronous version of `GetOrCreateVirtualEnvironmentAsync`.

##### DeleteVirtualEnvironmentAsync

```csharp
Task<bool> DeleteVirtualEnvironmentAsync(
    string name,
    CancellationToken cancellationToken = default)
```

Deletes a virtual environment with the specified name.

**Returns:** `true` if the virtual environment was deleted, `false` if it wasn't found.

##### DeleteVirtualEnvironment

```csharp
bool DeleteVirtualEnvironment(string name)
```

Synchronous version of `DeleteVirtualEnvironmentAsync`.

##### ListVirtualEnvironments

```csharp
IReadOnlyList<string> ListVirtualEnvironments()
```

Lists all virtual environments managed by this root runtime.

**Returns:** A read-only list of virtual environment names.

---

### IPythonVirtualRuntime

Interface for Python virtual environment runtime implementations. Extends `IPythonRuntime` and provides the same methods as `IPythonRuntime` but executed within the virtual environment context.

---

## Classes

### PythonManager

Manager for direct Python runtime instances (subprocess execution).

#### Constructor

```csharp
public PythonManager(
    string directory,
    GitHubClient githubClient,
    ILogger<PythonManager>? logger = null,
    ILoggerFactory? loggerFactory = null,
    IMemoryCache? cache = null)
```

**Parameters:**
- `directory`: The directory where Python instances will be stored.
- `githubClient`: The GitHub client for downloading Python distributions.
- `logger`: Optional logger for this manager.
- `loggerFactory`: Optional logger factory for creating loggers for runtime instances.
- `cache`: Optional memory cache for caching GitHub API responses (e.g., version lists). Improves performance by reducing API calls.

#### Methods

Implements `IPythonManager`. See [IPythonManager](#ipythonmanager) for method documentation.

---

### PythonNetManager

Manager for Python.NET runtime instances (in-process execution).

#### Constructor

```csharp
public PythonNetManager(
    string directory,
    GitHubClient githubClient,
    ILogger<PythonNetManager>? logger = null,
    ILoggerFactory? loggerFactory = null,
    IMemoryCache? cache = null)
```

**Parameters:**
- `directory`: The directory where Python instances will be stored.
- `githubClient`: The GitHub client for downloading Python distributions.
- `logger`: Optional logger for this manager.
- `loggerFactory`: Optional logger factory for creating loggers for runtime instances.
- `cache`: Optional memory cache for caching GitHub API responses (e.g., version lists). Improves performance by reducing API calls.

#### Methods

Implements `IPythonManager`. See [IPythonManager](#ipythonmanager) for method documentation.

**Note:** Runtimes created by `PythonNetManager` implement `IDisposable` and should be disposed when no longer needed.

---

### PythonExecutionResult

A record type representing the result of executing a Python command or script.

```csharp
public record PythonExecutionResult(
    int ExitCode,
    string StandardOutput = "",
    string StandardError = "");
```

**Properties:**
- `ExitCode`: The exit code from the Python process (0 indicates success).
- `StandardOutput`: The standard output from the Python process.
- `StandardError`: The standard error output from the Python process.

---

### InstanceMetadata

Represents metadata about a Python instance.

**Properties:**
- `PythonVersion`: The Python version (e.g., "3.12.0").
- `BuildDate`: The build date in YYYYMMDD format.
- `WasLatestBuild`: Whether this was the latest build at installation time.
- `InstallationDate`: When this instance was installed.
- `Directory`: The directory where this Python instance is installed.

---

## Exceptions

See [Error Handling](Error-Handling.md) for detailed exception documentation.

### Exception Hierarchy

- `Exception`
  - `PythonInstallationException` (base for installation-related exceptions)
    - `InstanceNotFoundException`
    - `InvalidPythonVersionException`
    - `MetadataCorruptedException`
    - `PlatformNotSupportedException`
    - `PythonNotInstalledException`
  - `PackageInstallationException`
    - `InvalidPackageSpecificationException`
    - `RequirementsFileException`
  - `PythonExecutionException`
    - `PythonNetExecutionException`
  - `PythonNetInitializationException`
  - `VirtualEnvironmentNotFoundException`

---

## See Also

- [Getting Started](Getting-Started.md)
- [Examples](Examples.md)
- [Error Handling](Error-Handling.md)
- [Architecture](Architecture.md)

