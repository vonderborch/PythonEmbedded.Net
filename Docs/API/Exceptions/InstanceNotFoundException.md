# InstanceNotFoundException

Exception thrown when a requested Python instance is not found.

## Namespace

`PythonEmbedded.Net.Exceptions`

## Inheritance

```
Exception
└── PythonInstallationException
    └── InstanceNotFoundException
```

## Properties

### PythonVersion

```csharp
public string? PythonVersion { get; set; }
```

Gets or sets the Python version that was requested.

### BuildDate

```csharp
public string? BuildDate { get; set; }
```

Gets or sets the build date that was requested.

## Constructors

Standard exception constructors are available. See [PythonInstallationException](./PythonInstallationException.md) for details.

## Usage

Thrown when attempting to get an instance that doesn't exist or when a release asset cannot be found.

**Example:**

```csharp
try
{
    var runtime = await manager.GetInstanceAsync("3.99");
}
catch (InstanceNotFoundException ex)
{
    Console.WriteLine($"Python {ex.PythonVersion} not found");
}
```

## Related Types

- [PythonInstallationException](./PythonInstallationException.md) - Base exception class

