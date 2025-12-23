# PythonRootVirtualEnvironment

Subprocess-based virtual environment runtime. Created by [PythonRootRuntime](./PythonRootRuntime.md).

## Namespace

`PythonEmbedded.Net`

## Inheritance

```
BasePythonRuntime
└── BasePythonVirtualRuntime
    └── PythonRootVirtualEnvironment
```

## Constructors

### PythonRootVirtualEnvironment

```csharp
public PythonRootVirtualEnvironment(string virtualEnvironmentPath, ILogger<PythonRootVirtualEnvironment>? logger = null)
```

Initializes a new instance of the PythonRootVirtualEnvironment class.

**Parameters:**
- `virtualEnvironmentPath` - The path to the virtual environment directory
- `logger` - Optional logger for this runtime

## Methods

Inherits all methods from [BasePythonRuntime](./BasePythonRuntime.md) for executing Python code and managing packages.

## Usage

PythonRootVirtualEnvironment is used for isolated Python environments with their own package installations.

**Example:**

```csharp
// Create a virtual environment from a root runtime
var venv = await rootRuntime.GetOrCreateVirtualEnvironmentAsync("myenv");

// Install packages in the virtual environment
await venv.InstallPackageAsync("requests");

// Execute code in the virtual environment
var result = await venv.ExecuteCommandAsync("-c \"import requests; print('OK')\"");
```

## Related Types

- [BasePythonVirtualRuntime](./BasePythonVirtualRuntime.md) - Base class
- [PythonRootRuntime](./PythonRootRuntime.md) - Root runtime that creates this virtual environment

