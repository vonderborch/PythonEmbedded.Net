# ManagerConfiguration

Configuration settings for a Python manager.

## Namespace

`PythonEmbedded.Net.Models`

## Properties

### DefaultPythonVersion

```csharp
public string? DefaultPythonVersion { get; set; }
```

Gets or sets the default Python version to use when version is not specified (e.g., "3.12").

### DefaultPipIndexUrl

```csharp
public string? DefaultPipIndexUrl { get; set; }
```

Gets or sets the default PyPI index URL for pip operations.

### ProxyUrl

```csharp
public string? ProxyUrl { get; set; }
```

Gets or sets the proxy URL for pip operations.

### DefaultTimeout

```csharp
public TimeSpan? DefaultTimeout { get; set; }
```

Gets or sets the default timeout for operations.

### RetryAttempts

```csharp
public int RetryAttempts { get; set; }
```

Gets or sets the number of retry attempts for failed operations. Default: 3.

### RetryDelay

```csharp
public TimeSpan RetryDelay { get; set; }
```

Gets or sets the delay between retry attempts. Default: 1 second.

### UseExponentialBackoff

```csharp
public bool UseExponentialBackoff { get; set; }
```

Gets or sets whether to use exponential backoff for retries. Default: true.

## Usage

ManagerConfiguration is used to configure [BasePythonManager](../Managers/BasePythonManager.md) instances.

**Example:**

```csharp
var config = new ManagerConfiguration
{
    DefaultPythonVersion = "3.12",
    RetryAttempts = 5,
    RetryDelay = TimeSpan.FromSeconds(2)
};

var manager = new PythonManager("./instances", githubClient, configuration: config);
```

## Related Types

- [BasePythonManager](../Managers/BasePythonManager.md) - Uses ManagerConfiguration




