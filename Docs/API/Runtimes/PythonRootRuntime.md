# PythonRootRuntime

Subprocess-based root Python runtime implementation. Created by [PythonManager](../Managers/PythonManager.md).

## Namespace

`PythonEmbedded.Net`

## Inheritance

```
BasePythonRuntime
└── BasePythonRootRuntime
    └── PythonRootRuntime
```

## Constructors

### PythonRootRuntime

```csharp
public PythonRootRuntime(InstanceMetadata instanceMetadata, ILogger<PythonRootRuntime>? logger = null)
```

Initializes a new instance of the PythonRootRuntime class.

**Parameters:**
- `instanceMetadata` - The metadata for the Python instance. See [InstanceMetadata](../Models/InstanceMetadata.md)
- `logger` - Optional logger for this runtime

**Example:**

```csharp
var runtime = new PythonRootRuntime(instanceMetadata, logger);
```

## Methods

Inherits all methods from:
- [BasePythonRuntime](./BasePythonRuntime.md) - For executing Python code and managing packages
- [BasePythonRootRuntime](./BasePythonRootRuntime.md) - For managing virtual environments

## Usage

PythonRootRuntime executes Python code via subprocess. This is the standard runtime for most use cases.

**Example:**

```csharp
// Get a runtime from a manager
var manager = new PythonManager("./instances", githubClient);
var runtime = await manager.GetOrCreateInstanceAsync("3.12");

// Execute Python code
var result = await runtime.ExecuteCommandAsync("-c \"print('Hello')\"");

// Create a virtual environment
var venv = await runtime.GetOrCreateVirtualEnvironmentAsync("myenv");
```

## Related Types

- [BasePythonRootRuntime](./BasePythonRootRuntime.md) - Base class
- [PythonManager](../Managers/PythonManager.md) - Manager that creates this runtime
- [PythonRootVirtualEnvironment](./PythonRootVirtualEnvironment.md) - Virtual environment created by this runtime

