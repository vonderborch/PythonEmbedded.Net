# VirtualEnvironmentMetadata

Represents metadata associated with a Python virtual environment, including its name, location, and creation details.

## Namespace

`PythonEmbedded.Net.Models`

## Properties

### Name

```csharp
public string Name { get; set; }
```

Gets or sets the name of the virtual environment.

### ExternalPath

```csharp
public string? ExternalPath { get; set; }
```

Gets or sets the actual path for external virtual environments. When set, the venv files are located at this path instead of the default location within the instance directory.

### CreatedDate

```csharp
public DateTime CreatedDate { get; set; }
```

Gets or sets the date and time when the virtual environment was created.

### IsExternal

```csharp
[JsonIgnore]
public bool IsExternal { get; }
```

Gets whether this virtual environment is stored at an external (non-default) location. Returns `true` if `ExternalPath` is set, `false` otherwise.

## Methods

### GetResolvedPath

```csharp
public string GetResolvedPath(string defaultPath)
```

Gets the resolved path to the virtual environment directory.

**Parameters:**
- `defaultPath` - The default path to use if the venv is not external

**Returns:**
- `string` - Returns `ExternalPath` if set, otherwise returns `defaultPath`

## Usage

`VirtualEnvironmentMetadata` is stored as part of `InstanceMetadata.VirtualEnvironments` to track virtual environments for a Python instance.

**Example:**

```csharp
// Creating a standard virtual environment (stored in default location)
var venv = await rootRuntime.GetOrCreateVirtualEnvironmentAsync("myenv");

// Creating an external virtual environment (stored at custom location)
var externalVenv = await rootRuntime.GetOrCreateVirtualEnvironmentAsync(
    "projectenv",
    externalPath: "/path/to/project/.venv");

// Getting metadata for a venv
var metadata = rootRuntime.GetVirtualEnvironmentMetadata("projectenv");
if (metadata?.IsExternal == true)
{
    Console.WriteLine($"Venv stored at: {metadata.ExternalPath}");
}
```

## JSON Serialization

When serialized (as part of `InstanceMetadata`), the `IsExternal` property is excluded as it's computed from `ExternalPath`.

```json
{
  "Name": "myenv",
  "ExternalPath": "/custom/path/to/venv",
  "CreatedDate": "2024-06-15T10:30:00Z"
}
```

## Related Types

- [InstanceMetadata](./InstanceMetadata.md) - Parent class that contains the `VirtualEnvironments` collection
- [BasePythonRootRuntime](../Runtimes/BasePythonRootRuntime.md) - Runtime class that manages virtual environments

