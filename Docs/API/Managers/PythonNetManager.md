# PythonNetManager

Manager for Python.NET runtime instances using in-process execution. This manager provides high-performance Python execution with direct .NET-Python interop.

## Namespace

`PythonEmbedded.Net`

## Inheritance

```
BasePythonManager
└── PythonNetManager
```

## Constructors

### PythonNetManager

```csharp
public PythonNetManager(
    string directory,
    GitHubClient githubClient,
    ILogger<PythonNetManager>? logger = null,
    ILoggerFactory? loggerFactory = null,
    IMemoryCache? cache = null,
    ManagerConfiguration? configuration = null)
```

Initializes a new instance of the PythonNetManager class.

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
var manager = new PythonNetManager(
    "./python-instances",
    githubClient);
```

## Methods

### GetPythonRuntimeForInstance

```csharp
public override BasePythonRuntime GetPythonRuntimeForInstance(InstanceMetadata instanceMetadata)
```

Gets a Python.NET runtime instance for the specified instance metadata. Returns a [PythonNetRootRuntime](../Runtimes/PythonNetRootRuntime.md) instance.

**Parameters:**
- `instanceMetadata` - The metadata for the Python instance

**Returns:**
- `BasePythonRuntime` - A [PythonNetRootRuntime](../Runtimes/PythonNetRootRuntime.md) instance

## Usage

PythonNetManager creates [PythonNetRootRuntime](../Runtimes/PythonNetRootRuntime.md) instances that execute Python code in-process using Python.NET. This provides better performance and direct .NET-Python interop.

**Example:**

```csharp
// Get or create a Python instance
var runtime = await manager.GetOrCreateInstanceAsync("3.12");

// Execute Python code (in-process)
var result = await runtime.ExecuteCommandAsync("-c \"print('Hello, World!')\"");
Console.WriteLine(result.StandardOutput); // "Hello, World!"
```

**Note:** PythonNetManager requires Python.NET to be properly initialized. Make sure to dispose of the runtime when done to release resources.

## Related Types

- [BasePythonManager](./BasePythonManager.md) - Base class
- [PythonNetRootRuntime](../Runtimes/PythonNetRootRuntime.md) - Runtime created by this manager
- [PythonManager](./PythonManager.md) - Alternative manager using subprocess execution



