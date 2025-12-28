# PythonManager

Manager for direct Python runtime instances using subprocess execution. This is the standard manager for most use cases.

## Namespace

`PythonEmbedded.Net`

## Inheritance

```
BasePythonManager
└── PythonManager
```

## Constructors

### PythonManager

```csharp
public PythonManager(
    string directory,
    GitHubClient githubClient,
    ILogger<PythonManager>? logger = null,
    ILoggerFactory? loggerFactory = null,
    IMemoryCache? cache = null,
    ManagerConfiguration? configuration = null)
```

Initializes a new instance of the PythonManager class.

**Parameters:**
- `directory` - The directory where Python instances will be stored
- `githubClient` - The GitHub client for downloading Python distributions (Octokit)
- `logger` - Optional logger for this manager
- `loggerFactory` - Optional logger factory for creating loggers for runtime instances
- `cache` - Optional memory cache for caching GitHub API responses
- `configuration` - Optional configuration settings

**Example:**

```csharp
using Octokit;
using PythonEmbedded.Net;

var githubClient = new GitHubClient(new ProductHeaderValue("MyApp"));
var manager = new PythonManager(
    "./python-instances",
    githubClient);
```

## Methods

### GetPythonRuntimeForInstance

```csharp
public override BasePythonRuntime GetPythonRuntimeForInstance(InstanceMetadata instanceMetadata)
```

Gets a Python runtime instance for the specified instance metadata. Returns a [PythonRootRuntime](../Runtimes/PythonRootRuntime.md) instance.

**Parameters:**
- `instanceMetadata` - The metadata for the Python instance

**Returns:**
- `BasePythonRuntime` - A [PythonRootRuntime](../Runtimes/PythonRootRuntime.md) instance

## Usage

PythonManager creates [PythonRootRuntime](../Runtimes/PythonRootRuntime.md) instances that execute Python code via subprocess. This is the recommended approach for most scenarios.

**Example:**

```csharp
// Get or create a Python instance
var runtime = await manager.GetOrCreateInstanceAsync("3.12");

// Execute Python code
var result = await runtime.ExecuteCommandAsync("-c \"print('Hello, World!')\"");
Console.WriteLine(result.StandardOutput); // "Hello, World!"
```

## Related Types

- [BasePythonManager](./BasePythonManager.md) - Base class
- [PythonRootRuntime](../Runtimes/PythonRootRuntime.md) - Runtime created by this manager
- [PythonNetManager](./PythonNetManager.md) - Alternative manager using Python.NET




