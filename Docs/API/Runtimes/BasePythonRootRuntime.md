# BasePythonRootRuntime

Abstract base class for Python root runtime implementations that can manage virtual environments.

## Namespace

`PythonEmbedded.Net`

## Inheritance

```
BasePythonRuntime
└── BasePythonRootRuntime (abstract)
    ├── PythonRootRuntime
    └── PythonNetRootRuntime
```

## Protected Abstract Properties

### VirtualEnvironmentsDirectory

```csharp
protected abstract string VirtualEnvironmentsDirectory { get; }
```

Gets the base directory for virtual environments managed by this root runtime.

## Public Methods

### GetOrCreateVirtualEnvironmentAsync

```csharp
public async Task<BasePythonVirtualRuntime> GetOrCreateVirtualEnvironmentAsync(
    string name,
    bool recreateIfExists = false,
    CancellationToken cancellationToken = default)
```

Gets or creates a virtual environment with the specified name.

**Parameters:**
- `name` - The name of the virtual environment
- `recreateIfExists` - Whether to recreate the virtual environment if it already exists
- `cancellationToken` - Cancellation token

**Returns:**
- `Task<BasePythonVirtualRuntime>` - The virtual environment runtime

**Example:**

```csharp
var venv = await rootRuntime.GetOrCreateVirtualEnvironmentAsync("myenv");
```

### DeleteVirtualEnvironmentAsync

```csharp
public async Task<bool> DeleteVirtualEnvironmentAsync(
    string name,
    CancellationToken cancellationToken = default)
```

Deletes a virtual environment with the specified name.

**Parameters:**
- `name` - The name of the virtual environment to delete
- `cancellationToken` - Cancellation token

**Returns:**
- `Task<bool>` - True if deleted, false if not found

**Exceptions:**
- [VirtualEnvironmentNotFoundException](../Exceptions/VirtualEnvironmentNotFoundException.md) - When deletion fails

### ListVirtualEnvironments

```csharp
public IReadOnlyList<string> ListVirtualEnvironments()
```

Lists all virtual environments managed by this root runtime.

**Returns:**
- `IReadOnlyList<string>` - List of virtual environment names

### CloneVirtualEnvironmentAsync

```csharp
public async Task<BasePythonVirtualRuntime> CloneVirtualEnvironmentAsync(
    string sourceName,
    string targetName,
    CancellationToken cancellationToken = default)
```

Clones a virtual environment to create a new one with the same packages and configuration.

**Parameters:**
- `sourceName` - The name of the source virtual environment
- `targetName` - The name of the target virtual environment
- `cancellationToken` - Cancellation token

**Returns:**
- `Task<BasePythonVirtualRuntime>` - The cloned virtual environment runtime

### ExportVirtualEnvironmentAsync

```csharp
public async Task<string> ExportVirtualEnvironmentAsync(
    string name,
    string outputPath,
    CancellationToken cancellationToken = default)
```

Exports a virtual environment to an archive file.

**Parameters:**
- `name` - The name of the virtual environment to export
- `outputPath` - The path where to save the archive file
- `cancellationToken` - Cancellation token

**Returns:**
- `Task<string>` - The path to the created archive file

### ImportVirtualEnvironmentAsync

```csharp
public async Task<BasePythonVirtualRuntime> ImportVirtualEnvironmentAsync(
    string name,
    string archivePath,
    CancellationToken cancellationToken = default)
```

Imports a virtual environment from an archive file.

**Parameters:**
- `name` - The name for the imported virtual environment
- `archivePath` - The path to the archive file to import
- `cancellationToken` - Cancellation token

**Returns:**
- `Task<BasePythonVirtualRuntime>` - The imported virtual environment runtime

### GetVirtualEnvironmentSize

```csharp
public long GetVirtualEnvironmentSize(string name)
```

Gets the disk usage of a virtual environment in bytes.

**Parameters:**
- `name` - The name of the virtual environment

**Returns:**
- `long` - The size in bytes

### GetVirtualEnvironmentInfo

```csharp
public Dictionary<string, object> GetVirtualEnvironmentInfo(string name)
```

Gets detailed information about a virtual environment.

**Parameters:**
- `name` - The name of the virtual environment

**Returns:**
- `Dictionary<string, object>` - Dictionary containing information about the virtual environment

## Protected Abstract Methods

### CreateVirtualRuntimeInstance

```csharp
protected abstract BasePythonVirtualRuntime CreateVirtualRuntimeInstance(string venvPath);
```

Creates a virtual runtime instance for the specified virtual environment path. Must be implemented by derived classes.

## Related Types

- [BasePythonRuntime](./BasePythonRuntime.md) - Base class
- [BasePythonVirtualRuntime](./BasePythonVirtualRuntime.md) - Virtual environment runtime base class
- [PythonRootRuntime](./PythonRootRuntime.md) - Subprocess-based implementation
- [PythonNetRootRuntime](./PythonNetRootRuntime.md) - Python.NET-based implementation


