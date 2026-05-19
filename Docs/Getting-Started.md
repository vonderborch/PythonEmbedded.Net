# Getting Started with PythonEmbedded.Net

This guide will help you get started with PythonEmbedded.Net **1.4.x**, a .NET library for managing local, embeddable Python instances. The library targets **.NET 9.0** and **.NET 10.0**.

## Installation

Install the NuGet package:

```bash
dotnet add package PythonEmbedded.Net
```

Or via Package Manager Console:

```powershell
Install-Package PythonEmbedded.Net
```

## Quick Start

### 1. Choose Your Execution Mode

PythonEmbedded.Net provides two execution modes:

- **PythonManager**: Subprocess-based execution (standard Python execution)
- **PythonNetManager**: Python.NET-based execution (in-process, high-performance)

Both return `BasePythonRuntime`. Cast to **`BasePythonRootRuntime`** for virtual environment APIs. There are no `IPythonRuntime` or `IPythonRootRuntime` interfaces in 1.4.x.

### 2. Create a Manager Instance

```csharp
using PythonEmbedded.Net;
using Octokit;

// For subprocess execution
var manager = new PythonManager(
    directory: "./python-instances",
    githubClient: new GitHubClient(new ProductHeaderValue("MyApp")));

// Or for Python.NET execution
var netManager = new PythonNetManager(
    directory: "./python-instances",
    githubClient: new GitHubClient(new ProductHeaderValue("MyApp")));
```

#### With Configuration

```csharp
// Create custom configuration
var configuration = new ManagerConfiguration
{
    DefaultPythonVersion = "3.12",
    DefaultTimeout = TimeSpan.FromMinutes(10),
    RetryAttempts = 3,
    UseExponentialBackoff = true,
    UvPath = null // Optional: custom path to uv; otherwise auto-detected or installed
};

var manager = new PythonManager(
    directory: "./python-instances",
    githubClient: new GitHubClient(new ProductHeaderValue("MyApp")),
    configuration: configuration);

// Use default version from configuration
var runtime = await manager.GetOrCreateInstanceAsync(); // Uses 3.12
```

### 3. Get or Create a Python Instance

```csharp
// Get or download Python 3.12.0 (downloads if not already present; uv installed by default)
var runtime = await manager.GetOrCreateInstanceAsync("3.12.0");

// Or use partial version to get latest patch (e.g., "3.12" finds "3.12.19")
var runtimeLatest = await manager.GetOrCreateInstanceAsync("3.12");

// Or use default version from configuration (if pythonVersion is null)
var runtimeDefault = await manager.GetOrCreateInstanceAsync();

// With build date
var runtimeWithDate = await manager.GetOrCreateInstanceAsync(
    "3.12.0",
    buildDate: new DateTime(2024, 1, 15));

// Classic pip-only instance (no uv)
var pipRuntime = await manager.GetOrCreateInstanceAsync("3.12.0", useUv: false);
```

### 4. Execute Python Code

```csharp
// Execute a simple Python command
var result = await runtime.ExecuteCommandAsync("print('Hello, World!')");
Console.WriteLine(result.StandardOutput); // "Hello, World!"
```

### 5. Install Packages (uv default, pip optional)

PythonEmbedded.Net uses [uv](https://github.com/astral-sh/uv) by default for fast package and virtual environment management. uv is detected on the system or installed automatically when you create a runtime with `useUv: true` (the default).

Pass **`useUv: false`** on package and venv APIs to use `python -m pip` and `python -m venv` instead.

```csharp
// Install a package (uses uv by default)
await runtime.InstallPackageAsync("numpy");

// Install with version constraint
await runtime.InstallPackageAsync("requests==2.31.0");

// Upgrade a package
await runtime.InstallPackageAsync("numpy", upgrade: true);

// Use pip instead of uv
await runtime.InstallPackageAsync("numpy", useUv: false);

// Install from requirements.txt or pyproject.toml
await runtime.InstallRequirementsAsync("requirements.txt");
await runtime.InstallPyProjectAsync("pyproject.toml", editable: true);

// Inspect installed packages
var installed = await runtime.ListInstalledPackagesAsync();
var version = await runtime.GetPackageVersionAsync("numpy");

// Validate requirements without installing
var requirementStatus = await runtime.CheckRequirementsAsync("requirements.txt");
```

Virtual environments created with `uv venv` do not include pip or a local uv copy. `BasePythonVirtualRuntime` finds the base interpreter's uv via `pyvenv.cfg` and targets the venv with uv's `--python` flag.

```csharp
if (runtime.IsUvAvailable)
    Console.WriteLine($"Using uv at: {runtime.UvPath}");
```

### 6. Work with Virtual Environments

```csharp
// Cast to BasePythonRootRuntime for virtual environment features
var rootRuntime = (BasePythonRootRuntime)runtime;

// Create a virtual environment (uv venv by default)
var venv = await rootRuntime.GetOrCreateVirtualEnvironmentAsync("myenv");

// Or use python -m venv
var pipVenv = await rootRuntime.GetOrCreateVirtualEnvironmentAsync("myenv-pip", useUv: false);

// Install packages in the virtual environment
await venv.InstallPackageAsync("pandas");

// Execute code in the virtual environment
var result = await venv.ExecuteCommandAsync("import pandas; print(pandas.__version__)");

// Clone, export, and import
var clone = await rootRuntime.CloneVirtualEnvironmentAsync("myenv", "myenv-copy");
await rootRuntime.ExportVirtualEnvironmentAsync("myenv", "./backups/myenv.zip");
var restored = await rootRuntime.ImportVirtualEnvironmentAsync("myenv-restored", "./backups/myenv.zip");

// List and delete
foreach (var name in rootRuntime.ListVirtualEnvironments())
    Console.WriteLine(name);
await rootRuntime.DeleteVirtualEnvironmentAsync("myenv-copy");
```

### 7. Health Checks and Diagnostics

```csharp
// Validate Python installation health
var health = await runtime.ValidatePythonInstallationAsync();
Console.WriteLine($"Health status: {health["OverallHealth"]}");

// Check system requirements
var requirements = manager.GetSystemRequirements();

// Run diagnostics
var issues = await manager.DiagnoseIssuesAsync();
if (issues.Count == 0)
{
    Console.WriteLine("No issues found!");
}

// PyPI lookup
var searchResults = await runtime.SearchPackagesAsync("requests");
var metadata = await runtime.GetPackageMetadataAsync("requests");
```

### 8. Export and Import Instances

```csharp
// Export an instance for backup
await manager.ExportInstanceAsync("3.12.0", "./backups/python-3.12.0.zip");

// Import an instance from backup
var imported = await manager.ImportInstanceAsync("./backups/python-3.12.0.zip");
```

## Complete Example

Here's a complete working example:

```csharp
using PythonEmbedded.Net;
using Octokit;
using Microsoft.Extensions.Logging;

// Create a logger factory (optional)
using var loggerFactory = LoggerFactory.Create(builder => builder.AddConsole());

// Create a GitHub client (authenticated for higher rate limits)
var githubClient = new GitHubClient(new ProductHeaderValue("MyApp"))
{
    Credentials = new Credentials(Environment.GetEnvironmentVariable("GITHUB_TOKEN"))
};

// Optional: Add memory cache for better performance (caches GitHub API responses)
using var memoryCache = new Microsoft.Extensions.Caching.Memory.MemoryCache(
    new Microsoft.Extensions.Caching.Memory.MemoryCacheOptions());

// Create a Python manager
var manager = new PythonManager(
    directory: "./python-instances",
    githubClient: githubClient,
    logger: loggerFactory.CreateLogger<PythonManager>(),
    loggerFactory: loggerFactory,
    cache: memoryCache); // Optional: improves performance for ListAvailableVersionsAsync

try
{
    // Get or create Python 3.12.0 (uv installed by default)
    var runtime = await manager.GetOrCreateInstanceAsync("3.12.0");
    
    // Execute Python code
    var result = await runtime.ExecuteCommandAsync("print('Hello from Python!')");
    Console.WriteLine($"Output: {result.StandardOutput}");
    
    // Install a package
    await runtime.InstallPackageAsync("requests");
    
    // Create a virtual environment
    var rootRuntime = (BasePythonRootRuntime)runtime;
    var venv = await rootRuntime.GetOrCreateVirtualEnvironmentAsync("myproject");
    
    // Install packages in the virtual environment
    await venv.InstallPackageAsync("numpy");
    await venv.InstallPackageAsync("pandas");
    
    // Execute code in the virtual environment
    var pandasResult = await venv.ExecuteCommandAsync(
        "import pandas as pd; print(f'Pandas version: {pd.__version__}')");
    Console.WriteLine(pandasResult.StandardOutput);
}
catch (Exception ex)
{
    Console.Error.WriteLine($"Error: {ex.Message}");
}
```

## Acknowledgments

This library utilizes [python-build-standalone](https://github.com/astral-sh/python-build-standalone) by [astral-sh](https://github.com/astral-sh) for providing high-quality, redistributable Python distributions. We are not associated with astral-sh, but we thank them for their fantastic work that makes this library possible.

## Next Steps

- Read the [API Reference](API-Reference.md) for detailed API documentation
- Check out [Examples](Examples.md) for more complex usage scenarios
- Review [Architecture](Architecture.md) to understand the design
- See [Error Handling](Error-Handling.md) for exception handling best practices
