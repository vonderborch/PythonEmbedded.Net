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

### InstanceMetadata

```csharp
protected abstract InstanceMetadata InstanceMetadata { get; }
```

Gets the instance metadata for this root runtime. Used to track virtual environments.

## Public Methods

### GetOrCreateVirtualEnvironmentAsync

```csharp
public async Task<BasePythonVirtualRuntime> GetOrCreateVirtualEnvironmentAsync(
    string name,
    bool recreateIfExists = false,
    string? externalPath = null,
    CancellationToken cancellationToken = default)
```

Gets or creates a virtual environment with the specified name.

**Parameters:**
- `name` - The name of the virtual environment
- `recreateIfExists` - Whether to recreate the virtual environment if it already exists
- `externalPath` - Optional external path where the venv should be created. If null, uses default location within the instance directory
- `cancellationToken` - Cancellation token

**Returns:**
- `Task<BasePythonVirtualRuntime>` - The virtual environment runtime

**Exceptions:**
- `ArgumentException` - If name is null or empty
- `InvalidOperationException` - If a venv with the same name already exists and `recreateIfExists` is false

**Example:**

```csharp
// Create venv in default location
var venv = await rootRuntime.GetOrCreateVirtualEnvironmentAsync("myenv");

// Create venv at external path
var externalVenv = await rootRuntime.GetOrCreateVirtualEnvironmentAsync(
    "projectenv",
    externalPath: "/path/to/project/.venv");

// Recreate existing venv
var freshVenv = await rootRuntime.GetOrCreateVirtualEnvironmentAsync(
    "myenv",
    recreateIfExists: true);
```

### GetOrCreateVirtualEnvironment

```csharp
public BasePythonVirtualRuntime GetOrCreateVirtualEnvironment(
    string name,
    bool recreateIfExists = false,
    string? externalPath = null)
```

Synchronous version of `GetOrCreateVirtualEnvironmentAsync`.

### DeleteVirtualEnvironmentAsync

```csharp
public async Task<bool> DeleteVirtualEnvironmentAsync(
    string name,
    CancellationToken cancellationToken = default,
    bool deleteExternalFiles = true)
```

Deletes a virtual environment with the specified name.

**Parameters:**
- `name` - The name of the virtual environment to delete
- `cancellationToken` - Cancellation token
- `deleteExternalFiles` - For external venvs, whether to delete the actual files (default true). If false, only removes the metadata tracking.

**Returns:**
- `Task<bool>` - True if deleted, false if not found

**Exceptions:**
- [VirtualEnvironmentNotFoundException](../Exceptions/VirtualEnvironmentNotFoundException.md) - When deletion fails

**Example:**

```csharp
// Delete venv and all files
await rootRuntime.DeleteVirtualEnvironmentAsync("myenv");

// Delete external venv but keep files on disk
await rootRuntime.DeleteVirtualEnvironmentAsync("projectenv", deleteExternalFiles: false);
```

### DeleteVirtualEnvironment

```csharp
public bool DeleteVirtualEnvironment(string name, bool deleteExternalFiles = true)
```

Synchronous version of `DeleteVirtualEnvironmentAsync`.

### ListVirtualEnvironments

```csharp
public IReadOnlyList<string> ListVirtualEnvironments()
```

Lists all virtual environments managed by this root runtime.

**Returns:**
- `IReadOnlyList<string>` - List of virtual environment names

### VirtualEnvironmentExists

```csharp
public bool VirtualEnvironmentExists(string name)
```

Checks if a virtual environment with the specified name exists.

**Parameters:**
- `name` - The name of the virtual environment

**Returns:**
- `bool` - True if the venv exists (standard or external), false otherwise

### ResolveVirtualEnvironmentPath

```csharp
public string ResolveVirtualEnvironmentPath(string name)
```

Resolves the actual path to a virtual environment from its name. Checks metadata for external paths.

**Parameters:**
- `name` - The name of the virtual environment

**Returns:**
- `string` - The actual path to the virtual environment

### GetVirtualEnvironmentMetadata

```csharp
public VirtualEnvironmentMetadata? GetVirtualEnvironmentMetadata(string name)
```

Gets the metadata for a virtual environment.

**Parameters:**
- `name` - The name of the virtual environment

**Returns:**
- `VirtualEnvironmentMetadata?` - The metadata if found, null otherwise

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

**Exceptions:**
- `InvalidOperationException` - If target venv already exists

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

**Exceptions:**
- `InvalidOperationException` - If venv with name already exists

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
- `Dictionary<string, object>` - Dictionary containing information about the virtual environment including:
  - `Name` - The venv name
  - `Path` - The actual path to the venv
  - `SizeBytes` - Size in bytes
  - `Exists` - Whether the venv exists
  - `Created` - Creation date
  - `Modified` - Last modified date
  - `IsExternal` - Whether the venv is at an external location
  - `ExternalPath` - (if external) The external path
  - `PythonVersion` - The Python version in the venv

## Protected Methods

### SaveInstanceMetadata

```csharp
protected void SaveInstanceMetadata()
```

Saves the instance metadata to disk. Called internally after modifying virtual environments.

### CreateVirtualEnvironmentAsync

```csharp
protected virtual async Task CreateVirtualEnvironmentAsync(
    string venvPath,
    CancellationToken cancellationToken = default)
```

Creates a virtual environment at the specified path using `uv` (significantly faster than `python -m venv`).

## Protected Abstract Methods

### CreateVirtualRuntimeInstance

```csharp
protected abstract BasePythonVirtualRuntime CreateVirtualRuntimeInstance(string venvPath);
```

Creates a virtual runtime instance for the specified virtual environment path. Must be implemented by derived classes.

## Package Manager

Virtual environments are created using [uv](https://github.com/astral-sh/uv), a fast Python package installer and resolver. `uv` is automatically installed when the runtime instance is created.

## External Virtual Environments

Virtual environments can be created at custom locations outside the default instance directory:

```csharp
// Create venv at project-specific location
var venv = await rootRuntime.GetOrCreateVirtualEnvironmentAsync(
    "projectenv",
    externalPath: "/path/to/myproject/.venv");

// The venv is tracked by name but stored at the external path
var path = rootRuntime.ResolveVirtualEnvironmentPath("projectenv");
// Returns: "/path/to/myproject/.venv"

// Delete and keep external files
await rootRuntime.DeleteVirtualEnvironmentAsync("projectenv", deleteExternalFiles: false);
```

## Related Types

- [BasePythonRuntime](./BasePythonRuntime.md) - Base class
- [BasePythonVirtualRuntime](./BasePythonVirtualRuntime.md) - Virtual environment runtime base class
- [PythonRootRuntime](./PythonRootRuntime.md) - Subprocess-based implementation
- [PythonNetRootRuntime](./PythonNetRootRuntime.md) - Python.NET-based implementation
- [VirtualEnvironmentMetadata](../Models/VirtualEnvironmentMetadata.md) - Venv metadata model
- [InstanceMetadata](../Models/InstanceMetadata.md) - Instance metadata (contains VirtualEnvironments collection)
