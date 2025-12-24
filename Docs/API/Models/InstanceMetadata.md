# InstanceMetadata

Represents metadata associated with a Python runtime instance, including its version, build information, and installation details.

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

### Directory

```csharp
public string Directory { get; internal set; }
```

Gets the directory path associated with the Python runtime instance. This property is set internally and should not be modified directly.

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

## Usage

InstanceMetadata is used to track Python installations and is automatically created when installing Python instances.

**Example:**

```csharp
// Check if metadata exists
if (InstanceMetadata.Exists(instanceDirectory))
{
    // Load metadata
    var metadata = InstanceMetadata.Load(instanceDirectory);
    Console.WriteLine($"Python {metadata.PythonVersion} installed on {metadata.InstallationDate}");
}
```

## Related Types

- [ManagerMetadata](./ManagerMetadata.md) - Manages collections of InstanceMetadata
- [BasePythonManager](../Managers/BasePythonManager.md) - Uses InstanceMetadata to track instances

