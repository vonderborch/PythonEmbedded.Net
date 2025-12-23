# PipConfiguration

Represents pip configuration information for package installation operations.

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

PipConfiguration is used when installing packages to configure pip behavior.

**Example:**

```csharp
var pipConfig = new PipConfiguration(
    IndexUrl: "https://pypi.org/simple",
    TrustedHost: "pypi.org",
    Proxy: "http://proxy.example.com:8080"
);

await runtime.InstallPackageAsync("requests", pipConfig: pipConfig);
```

## Related Types

- [BasePythonRuntime](../Runtimes/BasePythonRuntime.md) - Uses PipConfiguration for package installation

