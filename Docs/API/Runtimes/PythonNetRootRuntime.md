# PythonNetRootRuntime

Python.NET-based root Python runtime implementation. Created by [PythonNetManager](../Managers/PythonNetManager.md). Executes Python code in-process for high performance.

## Namespace

`PythonEmbedded.Net`

## Inheritance

```
BasePythonRuntime
└── BasePythonRootRuntime
    └── PythonNetRootRuntime : IDisposable
```

## Constructors

### PythonNetRootRuntime

```csharp
public PythonNetRootRuntime(InstanceMetadata instanceMetadata, ILogger<PythonNetRootRuntime>? logger = null)
```

Initializes a new instance of the PythonNetRootRuntime class. Initializes Python.NET if not already initialized.

**Parameters:**
- `instanceMetadata` - The metadata for the Python instance. See [InstanceMetadata](../Models/InstanceMetadata.md)
- `logger` - Optional logger for this runtime

**Exceptions:**
- [PythonNetInitializationException](../Exceptions/PythonNetInitializationException.md) - When Python.NET initialization fails

## Methods

Inherits all methods from:
- [BasePythonRuntime](./BasePythonRuntime.md) - For executing Python code and managing packages
- [BasePythonRootRuntime](./BasePythonRootRuntime.md) - For managing virtual environments

### Dispose

```csharp
public void Dispose()
```

Releases resources used by this instance. Implements `IDisposable`.

**Important:** Always dispose of PythonNetRootRuntime instances when done to release Python.NET resources.

## Usage

PythonNetRootRuntime executes Python code in-process using Python.NET. This provides better performance and direct .NET-Python interop.

**Example:**

```csharp
// Get a runtime from a manager
var manager = new PythonNetManager("./instances", githubClient);
using var runtime = await manager.GetOrCreateInstanceAsync("3.12");

// Execute Python code (in-process)
var result = await runtime.ExecuteCommandAsync("-c \"print('Hello')\"");

// Create a virtual environment
var venv = await runtime.GetOrCreateVirtualEnvironmentAsync("myenv");
```

**Note:** Always use `using` statements or call `Dispose()` when done with PythonNetRootRuntime instances.

## Related Types

- [BasePythonRootRuntime](./BasePythonRootRuntime.md) - Base class
- [PythonNetManager](../Managers/PythonNetManager.md) - Manager that creates this runtime
- [PythonNetVirtualEnvironment](./PythonNetVirtualEnvironment.md) - Virtual environment created by this runtime

