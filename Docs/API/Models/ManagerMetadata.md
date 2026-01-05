# ManagerMetadata

In-memory collection that manages multiple `InstanceMetadata` objects. This class loads instance metadata from individual `instance_metadata.json` files in each instance directory.

## Namespace

`PythonEmbedded.Net.Models`

## Important Note

**There is no central metadata file.** `ManagerMetadata` is an in-memory collection that scans the manager directory and loads `instance_metadata.json` files from each instance directory. It does not persist to a file.

## Properties

### Instances

```csharp
public List<InstanceMetadata> Instances { get; }
```

Gets the collection of `InstanceMetadata` objects loaded from instance directories.

## Constructors

### ManagerMetadata

```csharp
internal ManagerMetadata(string filePath)
```

**Note**: This constructor is internal. `ManagerMetadata` instances are created internally by `BasePythonManager`. You typically don't create instances of this class directly.

Initializes a new `ManagerMetadata` instance by scanning the specified directory for Python instance directories and loading their metadata files.

**Parameters:**
- `filePath` - The manager directory path to scan for instance directories

## Methods

### FindInstance

```csharp
public InstanceMetadata? FindInstance(string pythonVersion, DateTime? buildDate = null)
```

Retrieves an instance of `InstanceMetadata` matching the specified Python version and optionally a specific build date.

**Version Matching Behavior:**
- **Exact version** (e.g., "3.12.5"): Matches exactly that version
- **Partial version** (e.g., "3.12"): Finds the latest patch version among matching instances

**Parameters:**
- `pythonVersion` - The version of Python to locate (supports partial versions like "3.12")
- `buildDate` - Optional build date. If null, attempts to find the instance marked as the latest build

**Returns:**
- `InstanceMetadata?` - The matching instance, or `null` if not found

### RemoveInstance

```csharp
public bool RemoveInstance(InstanceMetadata? instance)
public bool RemoveInstance(string pythonVersion, DateTime? buildDate = null)
```

Removes a Python runtime instance from the collection and deletes its associated directory from the file system.

**Parameters:**
- `instance` - The `InstanceMetadata` object to remove, or
- `pythonVersion` - The Python version of the instance to remove
- `buildDate` - Optional build date (if null, removes the latest build)

**Returns:**
- `bool` - `true` if the instance was successfully removed, otherwise `false`

### GetInstances

```csharp
public IReadOnlyList<InstanceMetadata> GetInstances()
```

Retrieves a read-only list of all `InstanceMetadata` objects managed by this instance.

**Returns:**
- `IReadOnlyList<InstanceMetadata>` - A read-only list of instance metadata

## Usage

`ManagerMetadata` is used internally by `BasePythonManager` to track and manage Python instances. You typically interact with it through the manager's methods:

```csharp
var manager = new PythonManager("./instances", githubClient);

// ManagerMetadata is used internally
var runtime = await manager.GetOrCreateInstanceAsync("3.12.0");
var instances = manager.ListInstances(); // Uses ManagerMetadata internally
```

## Related Types

- [InstanceMetadata](./InstanceMetadata.md) - Individual instance metadata
- [BasePythonManager](../Managers/BasePythonManager.md) - Uses ManagerMetadata internally



