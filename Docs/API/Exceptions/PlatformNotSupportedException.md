# PlatformNotSupportedException

Exception thrown when the requested Python version is not available for the current platform.

## Namespace

`PythonEmbedded.Net.Exceptions`

## Inheritance

```
Exception
└── PythonInstallationException
    └── PlatformNotSupportedException
```

## Properties

### Platform

```csharp
public string? Platform { get; set; }
```

Gets or sets the platform that was requested or detected.

### SupportedPlatforms

```csharp
public IReadOnlyList<string>? SupportedPlatforms { get; set; }
```

Gets or sets the list of supported platforms.

## Constructors

Standard exception constructors are available. See [PythonInstallationException](./PythonInstallationException.md) for details.

## Usage

Thrown when the current platform is not supported or when OS version requirements are not met.

**Example:**

```csharp
try
{
    var runtime = await manager.GetOrCreateInstanceAsync("3.12");
}
catch (PlatformNotSupportedException ex)
{
    Console.WriteLine($"Platform {ex.Platform} is not supported");
    Console.WriteLine($"Supported platforms: {string.Join(", ", ex.SupportedPlatforms ?? Array.Empty<string>())}");
}
```

## Related Types

- [PythonInstallationException](./PythonInstallationException.md) - Base exception class
- [PlatformInfo](../Models/PlatformInfo.md) - Platform information model



