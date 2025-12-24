# PythonNetVirtualEnvironment

Python.NET-based virtual environment runtime. Created by [PythonNetRootRuntime](./PythonNetRootRuntime.md). Executes Python code in-process.

## Namespace

`PythonEmbedded.Net`

## Inheritance

```
BasePythonRuntime
└── BasePythonVirtualRuntime
    └── PythonNetVirtualEnvironment : IDisposable
```

## Constructors

### PythonNetVirtualEnvironment

```csharp
public PythonNetVirtualEnvironment(string virtualEnvironmentPath, ILogger<PythonNetVirtualEnvironment>? logger = null)
```

Initializes a new instance of the PythonNetVirtualEnvironment class.

**Parameters:**
- `virtualEnvironmentPath` - The path to the virtual environment directory
- `logger` - Optional logger for this runtime

## Methods

Inherits all methods from [BasePythonRuntime](./BasePythonRuntime.md) for executing Python code and managing packages.

### Dispose

```csharp
public void Dispose()
```

Releases resources used by this instance. Implements `IDisposable`.

**Important:** Always dispose of PythonNetVirtualEnvironment instances when done to release Python.NET resources.

## Usage

PythonNetVirtualEnvironment is used for isolated Python environments with in-process execution.

**Example:**

```csharp
// Create a virtual environment from a root runtime
using var venv = await rootRuntime.GetOrCreateVirtualEnvironmentAsync("myenv");

// Install packages in the virtual environment
await venv.InstallPackageAsync("requests");

// Execute code in the virtual environment (in-process)
var result = await venv.ExecuteCommandAsync("-c \"import requests; print('OK')\"");
```

**Note:** Always use `using` statements or call `Dispose()` when done with PythonNetVirtualEnvironment instances.

## Related Types

- [BasePythonVirtualRuntime](./BasePythonVirtualRuntime.md) - Base class
- [PythonNetRootRuntime](./PythonNetRootRuntime.md) - Root runtime that creates this virtual environment


