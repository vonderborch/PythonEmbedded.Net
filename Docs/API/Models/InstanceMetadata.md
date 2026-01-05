# InstanceMetadata

Represents metadata associated with a Python runtime instance, including its version, build information, installation details, and managed virtual environments.

## Namespace

`PythonEmbedded.Net.Models`

## Properties

### PythonVersion

```csharp
public string PythonVersion { get; set; }
```

Gets or sets the version of Python associated with the instance (e.g., "3.12.0").

### BuildDate

```csharp
public DateTime BuildDate { get; set; }
```

Gets or sets the date indicating when the build was created.

### WasLatestBuild

```csharp
public bool WasLatestBuild { get; set; }
```

Gets or sets a value indicating whether the current build was the most recent successful build.

### InstallationDate

```csharp
public DateTime InstallationDate { get; set; }
```

Gets or sets the date and time when the installation was completed.

### VirtualEnvironments

```csharp
public List<VirtualEnvironmentMetadata> VirtualEnvironments { get; set; }
```

Gets or sets the collection of virtual environments managed by this instance. See [VirtualEnvironmentMetadata](./VirtualEnvironmentMetadata.md).

### Directory

```csharp
[JsonIgnore]
public string Directory { get; internal set; }
```

Gets the directory path associated with the Python runtime instance. This property is set internally when loading and should not be modified directly.

## Static Methods

### Exists

```csharp
public static bool Exists(string directory)
```

Checks whether the instance metadata file exists in the specified directory path.

**Parameters:**
- `directory` - The directory to check for the presence of the instance metadata file

**Returns:**
- `bool` - True if the metadata file exists; otherwise, false

### Load

```csharp
public static InstanceMetadata? Load(string directory)
```

Loads the metadata for an instance from the specified directory path.

**Parameters:**
- `directory` - The directory containing the instance metadata file

**Returns:**
- `InstanceMetadata?` - The InstanceMetadata object if successfully loaded; otherwise, null

## Instance Methods

### Save

```csharp
public void Save(string directory)
```

Saves the metadata for the current instance to a specified directory path.

**Parameters:**
- `directory` - The directory where the instance metadata file will be saved

### GetVirtualEnvironment

```csharp
public VirtualEnvironmentMetadata? GetVirtualEnvironment(string name)
```

Gets the virtual environment metadata for the specified name.

**Parameters:**
- `name` - The name of the virtual environment (case-insensitive)

**Returns:**
- `VirtualEnvironmentMetadata?` - The metadata if found, null otherwise

### SetVirtualEnvironment

```csharp
public void SetVirtualEnvironment(VirtualEnvironmentMetadata venvMetadata)
```

Adds or updates a virtual environment metadata entry. If a venv with the same name exists, it is replaced.

**Parameters:**
- `venvMetadata` - The virtual environment metadata to add or update

### RemoveVirtualEnvironment

```csharp
public bool RemoveVirtualEnvironment(string name)
```

Removes a virtual environment metadata entry.

**Parameters:**
- `name` - The name of the virtual environment to remove (case-insensitive)

**Returns:**
- `bool` - True if the entry was removed, false if it wasn't found

## Usage

InstanceMetadata is used to track Python installations and their virtual environments. It is automatically created when installing Python instances and updated when managing virtual environments.

**Example:**

```csharp
// Check if metadata exists
if (InstanceMetadata.Exists(instanceDirectory))
{
    // Load metadata
    var metadata = InstanceMetadata.Load(instanceDirectory);
    Console.WriteLine($"Python {metadata.PythonVersion} installed on {metadata.InstallationDate}");
    
    // List virtual environments
    foreach (var venv in metadata.VirtualEnvironments)
    {
        Console.WriteLine($"  - {venv.Name} (External: {venv.IsExternal})");
    }
}

// Working with virtual environment metadata
var venvMetadata = metadata.GetVirtualEnvironment("myenv");
if (venvMetadata != null)
{
    Console.WriteLine($"Venv created: {venvMetadata.CreatedDate}");
}
```

## JSON Structure

The metadata is stored as `instance_metadata.json` in each instance directory:

```json
{
  "PythonVersion": "3.12.0",
  "BuildDate": "2024-01-15T00:00:00Z",
  "WasLatestBuild": true,
  "InstallationDate": "2024-06-01T10:30:00Z",
  "VirtualEnvironments": [
    {
      "Name": "myenv",
      "ExternalPath": null,
      "CreatedDate": "2024-06-01T11:00:00Z"
    },
    {
      "Name": "projectenv",
      "ExternalPath": "/path/to/project/.venv",
      "CreatedDate": "2024-06-02T14:30:00Z"
    }
  ]
}
```

## Related Types

- [VirtualEnvironmentMetadata](./VirtualEnvironmentMetadata.md) - Metadata for individual virtual environments
- [ManagerMetadata](./ManagerMetadata.md) - In-memory collection that manages multiple InstanceMetadata objects
- [BasePythonManager](../Managers/BasePythonManager.md) - Uses InstanceMetadata to track instances
- [BasePythonRootRuntime](../Runtimes/BasePythonRootRuntime.md) - Uses InstanceMetadata.VirtualEnvironments to track venvs

**Note**: Each Python instance has its own `instance_metadata.json` file in its directory. Virtual environment metadata is stored within the instance metadata, not in separate files per venv.
