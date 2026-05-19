# Quick Reference

A quick reference guide for PythonEmbedded.Net **1.4.x** (**.NET 9** / **.NET 10**).

## Table of Contents

- [Setup](#setup)
- [Common Operations](#common-operations)
- [Package Manager (uv vs pip)](#package-manager-uv-vs-pip)
- [Code Snippets](#code-snippets)
- [Method Signatures](#method-signatures)

## Setup

### Create Manager (Subprocess)

```csharp
var manager = new PythonManager(
    "./python-instances",
    new GitHubClient(new ProductHeaderValue("MyApp")));
```

### Create Manager (Python.NET)

```csharp
var manager = new PythonNetManager(
    "./python-instances",
    new GitHubClient(new ProductHeaderValue("MyApp")));
```

### With Logging and Caching

```csharp
using var memoryCache = new Microsoft.Extensions.Caching.Memory.MemoryCache(
    new Microsoft.Extensions.Caching.Memory.MemoryCacheOptions());

var manager = new PythonManager(
    "./python-instances",
    githubClient,
    logger: loggerFactory.CreateLogger<PythonManager>(),
    loggerFactory: loggerFactory,
    cache: memoryCache); // Optional: caches GitHub API responses
```

### With Custom uv Path

```csharp
var manager = new PythonManager(
    "./python-instances",
    githubClient,
    configuration: new ManagerConfiguration
    {
        UvPath = "/usr/local/bin/uv" // Optional; auto-detected if null
    });
```

### With GitHub Authentication

```csharp
var githubClient = new GitHubClient(new ProductHeaderValue("MyApp"))
{
    Credentials = new Credentials("github-token")
};
```

## Common Operations

### Get Python Instance

```csharp
// useUv: true (default) — installs/detects uv on the instance
var runtime = await manager.GetOrCreateInstanceAsync("3.12.0");

// Pip-only workflow — skip uv installation on the instance
var pipRuntime = await manager.GetOrCreateInstanceAsync("3.12.0", useUv: false);
```

### Execute Command

```csharp
var result = await runtime.ExecuteCommandAsync("print('Hello')");
Console.WriteLine(result.StandardOutput);
```

### Install Package

```csharp
// Default: uv pip install
await runtime.InstallPackageAsync("numpy");

// Pip fallback
await runtime.InstallPackageAsync("numpy", useUv: false);
```

### Create Virtual Environment

```csharp
var rootRuntime = (BasePythonRootRuntime)runtime;

// Default: uv venv (fast)
var venv = await rootRuntime.GetOrCreateVirtualEnvironmentAsync("myenv");

// Standard library venv + pip
var pipVenv = await rootRuntime.GetOrCreateVirtualEnvironmentAsync("legacy", useUv: false);
```

### Create External Virtual Environment

```csharp
var rootRuntime = (BasePythonRootRuntime)runtime;
var venv = await rootRuntime.GetOrCreateVirtualEnvironmentAsync(
    "projectenv",
    externalPath: "/path/to/project/.venv");
```

### Install to Virtual Environment

```csharp
await venv.InstallPackageAsync("pandas");
// uv venvs resolve uv via pyvenv.cfg → base interpreter; packages use --python <venv>
```

### Execute in Virtual Environment

```csharp
var result = await venv.ExecuteCommandAsync("import pandas; print(pandas.__version__)");
```

### List Instances

```csharp
var instances = manager.ListInstances();
```

### List Virtual Environments

```csharp
var rootRuntime = (BasePythonRootRuntime)runtime;
var venvNames = rootRuntime.ListVirtualEnvironments();
```

### Delete Instance

```csharp
await manager.DeleteInstanceAsync("3.12.0");
```

### Delete Virtual Environment

```csharp
await rootRuntime.DeleteVirtualEnvironmentAsync("myenv");
```

## Package Manager (uv vs pip)

| API | Default (`useUv: true`) | Pip fallback (`useUv: false`) |
|-----|-------------------------|-------------------------------|
| `GetOrCreateInstanceAsync` | Ensures uv on instance | No uv install |
| `GetOrCreateVirtualEnvironmentAsync` | `uv venv` | `python -m venv` |
| `InstallPackageAsync` | `uv pip install` | `python -m pip install` |
| `ListInstalledPackagesAsync` | `uv pip list` | `python -m pip list` |

**Properties:** `runtime.UvPath`, `runtime.IsUvAvailable` (not `UvExecutablePath`).

**Virtual envs:** After `uv venv`, the venv runtime shares the root’s uv via `pyvenv.cfg` (`home =` base interpreter).

## Code Snippets

### Complete Setup and Usage

```csharp
using PythonEmbedded.Net;
using Octokit;

var githubClient = new GitHubClient(new ProductHeaderValue("MyApp"));
var manager = new PythonManager("./python-instances", githubClient);

var runtime = await manager.GetOrCreateInstanceAsync("3.12.0");
var result = await runtime.ExecuteCommandAsync("print('Hello, World!')");
Console.WriteLine(result.StandardOutput);
```

### Virtual Environment Workflow

```csharp
var runtime = await manager.GetOrCreateInstanceAsync("3.12.0");
var rootRuntime = (BasePythonRootRuntime)runtime;

var venv = await rootRuntime.GetOrCreateVirtualEnvironmentAsync("myproject");

await venv.InstallPackageAsync("requests");
await venv.InstallPackageAsync("pandas");

var result = await venv.ExecuteCommandAsync(
    "import requests, pandas; print('Success')");
```

### Pip-Only Workflow

```csharp
var runtime = await manager.GetOrCreateInstanceAsync("3.12.0", useUv: false);
var root = (BasePythonRootRuntime)runtime;
var venv = await root.GetOrCreateVirtualEnvironmentAsync("pip-env", useUv: false);

await venv.InstallPackageAsync("six==1.16.0", useUv: false);
```

### Error Handling Pattern

```csharp
try
{
    var runtime = await manager.GetOrCreateInstanceAsync("3.12.0");
    await runtime.InstallPackageAsync("package-name");
}
catch (InstanceNotFoundException ex)
{
    Console.WriteLine($"Version not found: {ex.PythonVersion}");
}
catch (PackageInstallationException ex)
{
    Console.WriteLine($"Installation failed: {ex.PackageSpecification}");
    Console.WriteLine($"Output: {ex.InstallationOutput}");
}
```

### Dependency Injection Pattern

```csharp
// Registration — use concrete or abstract manager type
services.AddSingleton<PythonManager>(sp =>
{
    var githubClient = sp.GetRequiredService<GitHubClient>();
    var loggerFactory = sp.GetRequiredService<ILoggerFactory>();
    return new PythonManager("./instances", githubClient,
        loggerFactory.CreateLogger<PythonManager>(), loggerFactory);
});

// Usage
public class MyService
{
    private readonly PythonManager _pythonManager;

    public MyService(PythonManager pythonManager)
    {
        _pythonManager = pythonManager;
    }

    public async Task<string> ExecutePythonAsync(string code)
    {
        var runtime = await _pythonManager.GetOrCreateInstanceAsync("3.12.0");
        var result = await runtime.ExecuteCommandAsync(code);
        return result.StandardOutput;
    }
}
```

### Resource Disposal (Python.NET)

```csharp
var netManager = new PythonNetManager("./instances", githubClient);
var runtime = await netManager.GetOrCreateInstanceAsync("3.12.0");

try
{
    await runtime.ExecuteCommandAsync("print('Hello')");
}
finally
{
    if (runtime is IDisposable disposable)
    {
        disposable.Dispose();
    }
}
```

### Cancellation Token Usage

```csharp
using var cts = new CancellationTokenSource(TimeSpan.FromMinutes(5));

var runtime = await manager.GetOrCreateInstanceAsync("3.12.0", cancellationToken: cts.Token);
await runtime.ExecuteCommandAsync("long-running-code", cancellationToken: cts.Token);
```

### Input/Output Handling

```csharp
var inputLines = new[] { "line1", "line2" };
int index = 0;
var outputs = new List<string>();

var result = await runtime.ExecuteCommandAsync(
    "import sys; [print(f'Got: {line}') for line in sys.stdin]",
    stdinHandler: () => index < inputLines.Length ? inputLines[index++] : null,
    stdoutHandler: line => outputs.Add(line));
```

## Method Signatures

Public APIs use **abstract base classes**, not `IPython*` interfaces.

### BasePythonManager

```csharp
Task<BasePythonRuntime> GetOrCreateInstanceAsync(
    string? pythonVersion = null,
    DateTime? buildDate = null,
    bool useUv = true,
    CancellationToken cancellationToken = default)

BasePythonRuntime GetOrCreateInstance(
    string pythonVersion,
    DateTime? buildDate = null,
    bool useUv = true)

Task<bool> DeleteInstanceAsync(string pythonVersion, DateTime? buildDate = null, CancellationToken cancellationToken = default)
bool DeleteInstance(string pythonVersion, DateTime? buildDate = null)
IReadOnlyList<InstanceMetadata> ListInstances()
Task<IReadOnlyList<string>> ListAvailableVersionsAsync(string? releaseTag = null, CancellationToken cancellationToken = default)
IReadOnlyList<string> ListAvailableVersions(string? releaseTag = null)

abstract BasePythonRuntime GetPythonRuntimeForInstance(InstanceMetadata instanceMetadata)
```

### BasePythonRuntime

```csharp
string? UvPath { get; }
bool IsUvAvailable { get; }

Task<PythonExecutionResult> ExecuteCommandAsync(...)
Task<PythonExecutionResult> InstallPackageAsync(string packageSpecification, bool upgrade = false, string? indexUrl = null, bool useUv = true, CancellationToken cancellationToken = default)
Task<PythonExecutionResult> InstallRequirementsAsync(string requirementsFilePath, bool upgrade = false, bool useUv = true, CancellationToken cancellationToken = default)
Task<PythonExecutionResult> InstallPyProjectAsync(string pyProjectFilePath, bool editable = false, bool useUv = true, CancellationToken cancellationToken = default)
Task<IReadOnlyList<PackageInfo>> ListInstalledPackagesAsync(bool useUv = true, CancellationToken cancellationToken = default)
// ... additional package APIs accept useUv (default true)
```

### BasePythonRootRuntime (extends BasePythonRuntime)

```csharp
Task<BasePythonVirtualRuntime> GetOrCreateVirtualEnvironmentAsync(
    string name,
    bool recreateIfExists = false,
    string? externalPath = null,
    bool useUv = true,
    CancellationToken cancellationToken = default)

BasePythonVirtualRuntime GetOrCreateVirtualEnvironment(
    string name,
    bool recreateIfExists = false,
    string? externalPath = null,
    bool useUv = true)

Task<bool> DeleteVirtualEnvironmentAsync(string name, CancellationToken cancellationToken = default, bool deleteExternalFiles = true)
IReadOnlyList<string> ListVirtualEnvironments()
bool VirtualEnvironmentExists(string name)
string ResolveVirtualEnvironmentPath(string name)
VirtualEnvironmentMetadata? GetVirtualEnvironmentMetadata(string name)
```

## PythonExecutionResult

```csharp
public record PythonExecutionResult(
    int ExitCode,
    string StandardOutput = "",
    string StandardError = "");
```

## Common Exception Types

- `InstanceNotFoundException` - Python version not found
- `PackageInstallationException` - Package installation failed
- `PythonExecutionException` - Python execution failed
- `PythonNotInstalledException` - Python installation invalid
- `VirtualEnvironmentNotFoundException` - Virtual environment not found
- `PythonNetInitializationException` - Python.NET initialization failed

## Version Format

- **Full version**: `"3.12.0"` - Matches exactly this version
- **Partial version**: `"3.12"` - Finds the latest patch version (e.g., "3.12.19")
- **With build date**: `GetOrCreateInstanceAsync("3.12.0", buildDate: new DateTime(2024, 1, 15))`
  - `buildDate` is `DateTime?`
  - If specified, finds the first release on or after this date
  - If null, uses the latest available release

## See Also

- [Getting Started](Getting-Started.md) - Detailed getting started guide
- [API Reference](API-Reference.md) - Complete API documentation
- [Examples](Examples.md) - Comprehensive examples
- [Error Handling](Error-Handling.md) - Exception reference
- [Architecture](Architecture.md) - Class hierarchy and uv/pip design
