# BasePythonManager

Abstract base class for managing Python installations and instances. Provides functionality for downloading, installing, and managing Python distributions.

## Namespace

`PythonEmbedded.Net`

## Inheritance

```
BasePythonManager (abstract)
├── PythonManager
└── PythonNetManager
```

## Properties

### Configuration

```csharp
public ManagerConfiguration Configuration { get; set; }
```

Gets or sets the configuration for this manager. See [ManagerConfiguration](../Models/ManagerConfiguration.md) for details.

## Constructors

### BasePythonManager

```csharp
protected BasePythonManager(
    string directory,
    GitHubClient githubClient,
    ILogger<BasePythonManager>? logger = null,
    ILoggerFactory? loggerFactory = null,
    IMemoryCache? cache = null,
    ManagerConfiguration? configuration = null)
```

Initializes a new instance of the BasePythonManager class.

**Parameters:**
- `directory` - The directory where Python instances will be stored
- `githubClient` - The GitHub client for downloading Python distributions (Octokit)
- `logger` - Optional logger for this manager
- `loggerFactory` - Optional logger factory for creating loggers for runtime instances
- `cache` - Optional memory cache for caching GitHub API responses
- `configuration` - Optional configuration settings

**Exceptions:**
- `ArgumentException` - When directory is null or whitespace

## Methods

### GetOrCreateInstanceAsync

```csharp
public async Task<BasePythonRuntime> GetOrCreateInstanceAsync(
    string? pythonVersion = null,
    DateTime? buildDate = null,
    CancellationToken cancellationToken = default)
```

Gets or creates a Python runtime instance for the specified version. Downloads and installs the Python distribution if it doesn't exist.

**Parameters:**
- `pythonVersion` - Optional Python version (e.g., "3.12", "3.11.5"). If null, uses default from configuration or "3.12". Supports partial versions (e.g., "3.10" will find the latest patch version like "3.10.19")
- `buildDate` - Optional build date. If null, uses the latest build. If specified, finds the first release on or after this date
- `cancellationToken` - Cancellation token

**Returns:**
- `Task<BasePythonRuntime>` - A Python runtime instance

**Exceptions:**
- [InstanceNotFoundException](../Exceptions/InstanceNotFoundException.md) - When the specified version/build date is not found
- [PlatformNotSupportedException](../Exceptions/PlatformNotSupportedException.md) - When the platform is not supported
- [PythonInstallationException](../Exceptions/PythonInstallationException.md) - When installation fails

### GetInstanceAsync

```csharp
public async Task<BasePythonRuntime?> GetInstanceAsync(
    string pythonVersion,
    DateTime? buildDate = null,
    CancellationToken cancellationToken = default)
```

Gets an existing Python runtime instance without creating a new one.

**Parameters:**
- `pythonVersion` - Python version (e.g., "3.12")
- `buildDate` - Optional build date
- `cancellationToken` - Cancellation token

**Returns:**
- `Task<BasePythonRuntime?>` - A Python runtime instance, or null if not found

### DeleteInstance

```csharp
public bool DeleteInstance(string pythonVersion, DateTime? buildDate = null)
```

Deletes a Python instance from disk.

**Parameters:**
- `pythonVersion` - Python version
- `buildDate` - Optional build date

**Returns:**
- `bool` - True if the instance was deleted, false if not found

### ListInstances

```csharp
public IReadOnlyList<InstanceMetadata> ListInstances()
```

Lists all installed Python instances.

**Returns:**
- `IReadOnlyList<InstanceMetadata>` - List of instance metadata. See [InstanceMetadata](../Models/InstanceMetadata.md)

### ListAvailableVersions

```csharp
public IReadOnlyList<string> ListAvailableVersions(string? releaseTag = null)
```

Lists available Python versions from GitHub releases.

**Parameters:**
- `releaseTag` - Optional release tag to query. If null, queries the latest release

**Returns:**
- `IReadOnlyList<string>` - List of available Python versions

**Exceptions:**
- [InstanceNotFoundException](../Exceptions/InstanceNotFoundException.md) - When the release is not found
- [PythonInstallationException](../Exceptions/PythonInstallationException.md) - When GitHub API access fails

### ListAvailableVersionsAsync

```csharp
public async Task<IReadOnlyList<string>> ListAvailableVersionsAsync(
    string? releaseTag = null,
    CancellationToken cancellationToken = default)
```

Asynchronous version of ListAvailableVersions.

### GetLatestPythonVersion

```csharp
public string? GetLatestPythonVersion()
```

Gets the latest available Python version from GitHub releases.

**Returns:**
- `string?` - Latest Python version, or null if not found

### FindBestMatchingVersion

```csharp
public string? FindBestMatchingVersion(string versionSpec)
```

Finds the best matching Python version for a version specification (e.g., ">=3.11", "~3.12").

**Parameters:**
- `versionSpec` - Version specification string

**Returns:**
- `string?` - Best matching version, or null if not found

### ValidateInstanceIntegrity

```csharp
public bool ValidateInstanceIntegrity(string pythonVersion, DateTime? buildDate = null)
```

Validates that a Python instance is complete and not corrupted.

**Parameters:**
- `pythonVersion` - Python version
- `buildDate` - Optional build date

**Returns:**
- `bool` - True if the instance is valid, false otherwise

### CheckDiskSpace

```csharp
public bool CheckDiskSpace(long requiredBytes)
```

Checks if there is enough disk space available.

**Parameters:**
- `requiredBytes` - Required bytes

**Returns:**
- `bool` - True if enough space is available

### TestNetworkConnectivity

```csharp
public bool TestNetworkConnectivity()
```

Tests network connectivity to the GitHub API.

**Returns:**
- `bool` - True if network connectivity is available

### DiagnoseIssues

```csharp
public IReadOnlyList<string> DiagnoseIssues()
```

Performs diagnostics and returns a list of issues found.

**Returns:**
- `IReadOnlyList<string>` - List of diagnostic messages

## Abstract Methods

### GetPythonRuntimeForInstance

```csharp
protected abstract BasePythonRuntime GetPythonRuntimeForInstance(InstanceMetadata instanceMetadata)
```

Gets a Python runtime instance for the specified instance metadata. Must be implemented by derived classes.

**Parameters:**
- `instanceMetadata` - The metadata for the Python instance

**Returns:**
- `BasePythonRuntime` - A Python runtime instance

## Related Types

- [BasePythonRuntime](../Runtimes/BasePythonRuntime.md) - Base class for Python runtimes
- [InstanceMetadata](../Models/InstanceMetadata.md) - Instance metadata model
- [ManagerConfiguration](../Models/ManagerConfiguration.md) - Manager configuration
- [PythonManager](./PythonManager.md) - Subprocess-based implementation
- [PythonNetManager](./PythonNetManager.md) - Python.NET-based implementation

