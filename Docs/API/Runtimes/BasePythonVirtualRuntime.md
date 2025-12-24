# BasePythonVirtualRuntime

Abstract base class for Python virtual environment runtime implementations.

## Namespace

`PythonEmbedded.Net`

## Inheritance

```
BasePythonRuntime
└── BasePythonVirtualRuntime (abstract)
    ├── PythonRootVirtualEnvironment
    └── PythonNetVirtualEnvironment
```

## Protected Abstract Properties

### VirtualEnvironmentPath

```csharp
protected abstract string VirtualEnvironmentPath { get; }
```

Gets the path to the virtual environment directory.

## Protected Properties

### PythonExecutablePath

```csharp
protected override string PythonExecutablePath { get; }
```

Gets the Python executable path for this virtual environment. Automatically resolves to the correct path based on the platform (Windows: `Scripts/python.exe`, Unix: `bin/python3`).

### WorkingDirectory

```csharp
protected override string WorkingDirectory { get; }
```

Gets the working directory for this virtual environment (defaults to the virtual environment path).

## Methods

Inherits all methods from [BasePythonRuntime](./BasePythonRuntime.md) for executing Python code and managing packages.

## Related Types

- [BasePythonRuntime](./BasePythonRuntime.md) - Base class
- [PythonRootVirtualEnvironment](./PythonRootVirtualEnvironment.md) - Subprocess-based implementation
- [PythonNetVirtualEnvironment](./PythonNetVirtualEnvironment.md) - Python.NET-based implementation


