# Quick Reference

A quick reference guide for PythonEmbedded.Net common operations and patterns.

## Table of Contents

- [Setup](#setup)
- [Common Operations](#common-operations)
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
var runtime = await manager.GetOrCreateInstanceAsync("3.12.0");
```

### Execute Command

```csharp
var result = await runtime.ExecuteCommandAsync("print('Hello')");
Console.WriteLine(result.StandardOutput);
```

### Install Package

```csharp
await runtime.InstallPackageAsync("numpy");
```

### Create Virtual Environment

```csharp
var rootRuntime = (IPythonRootRuntime)runtime;
var venv = await rootRuntime.GetOrCreateVirtualEnvironmentAsync("myenv");
```

### Install to Virtual Environment

```csharp
await venv.InstallPackageAsync("pandas");
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

## Code Snippets

### Complete Setup and Usage

```csharp
using PythonEmbedded.Net;
using Octokit;

// Setup
var githubClient = new GitHubClient(new ProductHeaderValue("MyApp"));
var manager = new PythonManager("./python-instances", githubClient);

// Get Python
var runtime = await manager.GetOrCreateInstanceAsync("3.12.0");

// Execute
var result = await runtime.ExecuteCommandAsync("print('Hello, World!')");
Console.WriteLine(result.StandardOutput);
```

### Virtual Environment Workflow

```csharp
// Get runtime
var runtime = await manager.GetOrCreateInstanceAsync("3.12.0");
var rootRuntime = (IPythonRootRuntime)runtime;

// Create venv
var venv = await rootRuntime.GetOrCreateVirtualEnvironmentAsync("myproject");

// Install packages
await venv.InstallPackageAsync("requests");
await venv.InstallPackageAsync("pandas");

// Use venv
var result = await venv.ExecuteCommandAsync(
    "import requests, pandas; print('Success')");
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
// Registration
services.AddSingleton<IPythonManager>(sp =>
{
    var githubClient = sp.GetRequiredService<GitHubClient>();
    var loggerFactory = sp.GetRequiredService<ILoggerFactory>();
    return new PythonManager("./instances", githubClient, 
        loggerFactory.CreateLogger<PythonManager>(), loggerFactory);
});

// Usage
public class MyService
{
    private readonly IPythonManager _pythonManager;
    
    public MyService(IPythonManager pythonManager)
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

### IPythonManager

```csharp
Task<IPythonRuntime> GetOrCreateInstanceAsync(string pythonVersion, string? buildDate = null, CancellationToken cancellationToken = default)
IPythonRuntime GetOrCreateInstance(string pythonVersion, string? buildDate = null)
Task<bool> DeleteInstanceAsync(string pythonVersion, string? buildDate = null, CancellationToken cancellationToken = default)
bool DeleteInstance(string pythonVersion, string? buildDate = null)
IReadOnlyList<InstanceMetadata> ListInstances()
Task<IReadOnlyList<string>> ListAvailableVersionsAsync(string? releaseTag = null, CancellationToken cancellationToken = default)
IReadOnlyList<string> ListAvailableVersions(string? releaseTag = null)
```

### IPythonRuntime

```csharp
Task<PythonExecutionResult> ExecuteCommandAsync(string command, Func<string?>? stdinHandler = null, Action<string>? stdoutHandler = null, Action<string>? stderrHandler = null, CancellationToken cancellationToken = default)
PythonExecutionResult ExecuteCommand(string command, Func<string?>? stdinHandler = null, Action<string>? stdoutHandler = null, Action<string>? stderrHandler = null)
Task<PythonExecutionResult> ExecuteScriptAsync(string scriptPath, IEnumerable<string>? arguments = null, Func<string?>? stdinHandler = null, Action<string>? stdoutHandler = null, Action<string>? stderrHandler = null, CancellationToken cancellationToken = default)
PythonExecutionResult ExecuteScript(string scriptPath, IEnumerable<string>? arguments = null, Func<string?>? stdinHandler = null, Action<string>? stdoutHandler = null, Action<string>? stderrHandler = null)
Task<PythonExecutionResult> InstallPackageAsync(string packageSpecification, bool upgrade = false, CancellationToken cancellationToken = default)
PythonExecutionResult InstallPackage(string packageSpecification, bool upgrade = false)
Task<PythonExecutionResult> InstallRequirementsAsync(string requirementsFilePath, bool upgrade = false, CancellationToken cancellationToken = default)
PythonExecutionResult InstallRequirements(string requirementsFilePath, bool upgrade = false)
Task<PythonExecutionResult> InstallPyProjectAsync(string pyProjectFilePath, bool editable = false, CancellationToken cancellationToken = default)
PythonExecutionResult InstallPyProject(string pyProjectFilePath, bool editable = false)
```

### IPythonRootRuntime (extends IPythonRuntime)

```csharp
Task<IPythonVirtualRuntime> GetOrCreateVirtualEnvironmentAsync(string name, bool recreateIfExists = false, CancellationToken cancellationToken = default)
IPythonVirtualRuntime GetOrCreateVirtualEnvironment(string name, bool recreateIfExists = false)
Task<bool> DeleteVirtualEnvironmentAsync(string name, CancellationToken cancellationToken = default)
bool DeleteVirtualEnvironment(string name)
IReadOnlyList<string> ListVirtualEnvironments()
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

- Full version: `"3.12.0"`
- Minor version: `"3.12"`
- With build date: `GetOrCreateInstanceAsync("3.12.0", buildDate: "20240115")`

## See Also

- [Getting Started](Getting-Started.md) - Detailed getting started guide
- [API Reference](API-Reference.md) - Complete API documentation
- [Examples](Examples.md) - Comprehensive examples
- [Error Handling](Error-Handling.md) - Exception reference

