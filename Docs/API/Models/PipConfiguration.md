# PipConfiguration

Represents pip configuration information. Note: This library uses [uv](https://github.com/astral-sh/uv) for package operations, but this record is used to read existing pip configuration settings.

## Namespace

`PythonEmbedded.Net.Models`

## Definition

```csharp
public record PipConfiguration(
    string? IndexUrl = null,
    string? TrustedHost = null,
    string? Proxy = null)
```

## Properties

### IndexUrl

```csharp
public string? IndexUrl { get; init; }
```

Gets the PyPI index URL. If null, uses the default PyPI index.

### TrustedHost

```csharp
public string? TrustedHost { get; init; }
```

Gets the trusted host for pip operations. Used when connecting to custom PyPI indexes.

### Proxy

```csharp
public string? Proxy { get; init; }
```

Gets the proxy URL for pip operations.

## Usage

PipConfiguration is returned by `GetPipConfigurationAsync` to read existing pip configuration.

**Example:**

```csharp
// Read existing pip configuration
var pipConfig = await runtime.GetPipConfigurationAsync();
Console.WriteLine($"Index URL: {pipConfig.IndexUrl}");
Console.WriteLine($"Proxy: {pipConfig.Proxy}");
```

## Note

Package installation is handled by `uv`, not pip. The `InstallPackageAsync` method accepts an optional `indexUrl` parameter directly:

```csharp
await runtime.InstallPackageAsync("requests", indexUrl: "https://custom-pypi.example.com/simple/");
```

## Related Types

- [BasePythonRuntime](../Runtimes/BasePythonRuntime.md) - Provides `GetPipConfigurationAsync` method
