# PythonEmbedded.Net

A .NET library for managing local, embeddable Python instances. Download, install, manage, and execute Python environments directly within .NET applications without requiring system-wide Python installations.

![Logo](https://raw.githubusercontent.com/vonderborch/PythonEmbedded.Net/refs/heads/main/logo.png)

## Installation

### Nuget

[![NuGet version (PythonEmbedded.Net)](https://img.shields.io/nuget/v/PythonEmbedded.Net.svg?style=flat-square)](https://www.nuget.org/packages/PythonEmbedded.Net/)

The recommended installation approach is to use the available nuget
package: [PythonEmbedded.Net](https://www.nuget.org/packages/PythonEmbedded.Net/)

### Clone

Alternatively, you can clone this repo and reference the PythonEmbedded.Net project in your project.

## Features

- ✅ **Automatic Python Distribution Management**: Download and install Python distributions from [python-build-standalone](https://github.com/astral-sh/python-build-standalone)
- ✅ **Multiple Instance Support**: Manage multiple Python versions and build dates simultaneously
- ✅ **Virtual Environment Management**: Create and manage virtual environments for each Python instance
- ✅ **Package Installation**: Install packages via pip (single packages, requirements.txt, pyproject.toml)
- ✅ **Python Execution**: Execute Python code via subprocess or in-process using Python.NET
- ✅ **Two Execution Modes**: 
  - **PythonManager**: Subprocess-based execution (standard Python execution)
  - **PythonNetManager**: Python.NET-based execution (in-process, high-performance)
- ✅ **Cross-Platform**: Supports Windows, Linux, and macOS
- ✅ **Modern C# Design**: Abstract classes for extensibility, dependency injection support, IDisposable for resource management
- ✅ **Structured Logging**: Full support for Microsoft.Extensions.Logging
- ✅ **Performance Optimizations**: Optional caching for GitHub API responses, object pooling for frequently allocated objects

## Installation

Install the NuGet package:

```bash
dotnet add package PythonEmbedded.Net
```

## Requirements

- .NET 9.0 or later
- Octokit (included)
- Python.NET (included, optional - only needed for PythonNetManager)
- Tomlyn (for pyproject.toml support, included)

## Quick Start

### Basic Usage with PythonManager (Subprocess Mode)

```csharp
using PythonEmbedded.Net;
using Octokit;
using Microsoft.Extensions.Logging;

// Create a PythonManager for subprocess execution
var manager = new PythonManager(
    directory: "/path/to/python/instances",
    githubClient: new GitHubClient(new ProductHeaderValue("MyApp")),
    logger: loggerFactory.CreateLogger<PythonManager>());

// Get or create a Python instance (downloads if needed)
var runtime = await manager.GetOrCreateInstanceAsync("3.12.0");

// Cast to root runtime to access virtual environment features
var rootRuntime = (BasePythonRootRuntime)runtime;

// Create a virtual environment
var venv = await rootRuntime.GetOrCreateVirtualEnvironmentAsync("myenv");

// Install packages
await venv.InstallPackageAsync("numpy");
await venv.InstallPackageAsync("requests==2.31.0");

// Execute Python code
var result = await venv.ExecuteCommandAsync("print('Hello, World!')");
Console.WriteLine(result.StandardOutput); // Output: Hello, World!
```

### Using PythonNetManager (Python.NET Mode)

```csharp
// Create a PythonNetManager for in-process Python.NET execution
var netManager = new PythonNetManager(
    directory: "/path/to/python/instances",
    githubClient: new GitHubClient(new ProductHeaderValue("MyApp")),
    logger: loggerFactory.CreateLogger<PythonNetManager>());

// Get or create a Python instance
var runtime = await netManager.GetOrCreateInstanceAsync("3.12.0");

// Create virtual environment
var rootRuntime = (IPythonRootRuntime)runtime;
var venv = await rootRuntime.GetOrCreateVirtualEnvironmentAsync("myenv");

// Install packages and execute code (same API as PythonManager)
await venv.InstallPackageAsync("numpy");
var result = await venv.ExecuteCommandAsync("import numpy; print(numpy.__version__)");
```

### Multiple Instances

```csharp
var manager = new PythonManager("/path/to/python/instances", githubClient);

// Create multiple instances
var runtime312 = await manager.GetOrCreateInstanceAsync("3.12.0", buildDate: "20240115");
var runtime311 = await manager.GetOrCreateInstanceAsync("3.11.5", buildDate: "20240210");

// List all instances
var instances = manager.ListInstances();
foreach (var instance in instances)
{
    Console.WriteLine($"Python {instance.PythonVersion} (Build: {instance.BuildDate})");
}

// Delete an instance
await manager.DeleteInstanceAsync("3.11.5", "20240210");
```

### Package Installation Examples

```csharp
var runtime = await manager.GetOrCreateInstanceAsync("3.12.0");
var rootRuntime = (IPythonRootRuntime)runtime;

// Install a single package to the root Python installation
await runtime.InstallPackageAsync("numpy");

// Install with version constraint
await runtime.InstallPackageAsync("requests==2.31.0");

// Create a virtual environment and install packages
var venv = await rootRuntime.GetOrCreateVirtualEnvironmentAsync("myenv");
await venv.InstallPackageAsync("pandas");

// Install from requirements.txt
await venv.InstallRequirementsAsync("requirements.txt");

// Install from pyproject.toml
await venv.InstallPyProjectAsync("pyproject.toml", editable: true);
```

### Virtual Environment Management

```csharp
var runtime = await manager.GetOrCreateInstanceAsync("3.12.0");
var rootRuntime = (IPythonRootRuntime)runtime;

// Create a virtual environment
var venv = await rootRuntime.GetOrCreateVirtualEnvironmentAsync("myenv");

// Use the virtual environment for operations
await venv.InstallPackageAsync("numpy");
var result = await venv.ExecuteCommandAsync("print('Hello from venv!')");

// List virtual environments
var venvNames = rootRuntime.ListVirtualEnvironments();
foreach (var name in venvNames)
{
    Console.WriteLine($"Virtual environment: {name}");
}

// Delete a virtual environment
await rootRuntime.DeleteVirtualEnvironmentAsync("myenv");
```

### Execution Examples

#### Command Execution

```csharp
var runtime = await manager.GetOrCreateInstanceAsync("3.12.0");

// Execute Python code
var result = await runtime.ExecuteCommandAsync("print('Hello from Python')");

// Access execution results
Console.WriteLine($"Exit Code: {result.ExitCode}");
Console.WriteLine($"Output: {result.StandardOutput}");
Console.WriteLine($"Error: {result.StandardError}");

// With stdin/stdout/stderr handlers
var result2 = await runtime.ExecuteCommandAsync(
    command: "for i in range(3): print(i)",
    stdoutHandler: line => Console.WriteLine($"Received: {line}"),
    stderrHandler: line => Console.Error.WriteLine($"Error: {line}"));
```

#### Script Execution

```csharp
var runtime = await manager.GetOrCreateInstanceAsync("3.12.0");

// Execute a Python script
var scriptResult = await runtime.ExecuteScriptAsync(
    scriptPath: "script.py",
    arguments: new[] { "arg1", "arg2" });

// Execute script in virtual environment
var rootRuntime = (IPythonRootRuntime)runtime;
var venv = await rootRuntime.GetOrCreateVirtualEnvironmentAsync("myenv");
var venvResult = await venv.ExecuteScriptAsync("script.py");
```

#### Synchronous Versions

All async methods have synchronous counterparts:

```csharp
// Synchronous execution
var result = runtime.ExecuteCommand("print('Hello')");
var venv = rootRuntime.GetOrCreateVirtualEnvironment("myenv");
venv.InstallPackage("numpy");
```

### Advanced Usage

#### Using Abstract Classes for Dependency Injection

```csharp
// Register in DI container
services.AddMemoryCache(); // For caching GitHub API responses

services.AddSingleton<PythonManager>(sp =>
{
    var loggerFactory = sp.GetRequiredService<ILoggerFactory>();
    var cache = sp.GetService<IMemoryCache>(); // Optional: improves performance
    var githubClient = new GitHubClient(new ProductHeaderValue("MyApp"));
    return new PythonManager("/path/to/instances", githubClient, 
        loggerFactory.CreateLogger<PythonManager>(), loggerFactory, cache);
});

// Inject and use
public class MyService
{
    private readonly PythonManager _pythonManager;
    
    public MyService(PythonManager pythonManager)
    {
        _pythonManager = pythonManager;
    }
    
    public async Task DoWorkAsync()
    {
        var runtime = await _pythonManager.GetOrCreateInstanceAsync("3.12.0");
        var result = await runtime.ExecuteCommandAsync("print('Hello')");
    }
}
```

#### Resource Management (Python.NET)

When using `PythonNetManager`, dispose of runtimes when done:

```csharp
var netManager = new PythonNetManager("/path/to/instances", githubClient);
var runtime = await netManager.GetOrCreateInstanceAsync("3.12.0");

try
{
    // Use the runtime
    await runtime.ExecuteCommandAsync("print('Hello')");
}
finally
{
    // Dispose if it's a Python.NET runtime (PythonNetRootRuntime implements IDisposable)
    if (runtime is IDisposable disposable)
    {
        disposable.Dispose();
    }
}
```

## Platform Support

The library automatically detects your platform and downloads the appropriate Python distribution from [python-build-standalone](https://github.com/astral-sh/python-build-standalone):

- **Windows**: x64, x86 (Windows 7+)
- **Linux**: x64, ARM64, ARMv7 (GNU libc and musl)
- **macOS**: Intel (x64), Apple Silicon (ARM64)

## Architecture

### Manager Classes

- **`PythonManager`**: Manages Python instances with subprocess-based execution
- **`PythonNetManager`**: Manages Python instances with Python.NET in-process execution

Both extend `BasePythonManager` and provide the same abstract interface for instance management.

### Runtime Classes

- **`BasePythonRuntime`**: Abstract base class providing common execution and package installation functionality
- **`BasePythonRootRuntime`**: Extends `BasePythonRuntime` with virtual environment management
- **`BasePythonVirtualRuntime`**: Represents a virtual environment runtime

Concrete implementations:
- **`PythonRootRuntime`**: Subprocess-based root Python runtime
- **`PythonNetRootRuntime`**: Python.NET-based root Python runtime
- **`PythonRootVirtualEnvironment`**: Subprocess-based virtual environment
- **`PythonNetVirtualEnvironment`**: Python.NET-based virtual environment

### Abstract Classes

The library uses abstract base classes for extensibility:
- `BasePythonManager`: Abstract manager base class
- `BasePythonRuntime`: Base runtime class
- `BasePythonRootRuntime`: Root runtime with virtual environment support
- `BasePythonVirtualRuntime`: Virtual environment runtime base class

### Services

- **`IProcessExecutor`**: Interface for process execution (extracted for testability)
- **`ProcessExecutor`**: Default implementation of process execution

### Directory Structure

```
specifieddir/
├── manager_metadata.json          # Central metadata file
└── python-{version}-{buildDate}/   # Instance directory
    ├── venvs/                      # Virtual environments
    │   └── {venv_name}/
    ├── python_instance/            # Python installation files
    └── instance_metadata.json      # Instance metadata
```

## API Overview

### BasePythonManager

- `GetOrCreateInstanceAsync(pythonVersion, buildDate?)` - Get or create a Python runtime instance
- `GetOrCreateInstance(pythonVersion, buildDate?)` - Synchronous version
- `DeleteInstanceAsync(pythonVersion, buildDate?)` - Delete an instance
- `DeleteInstance(pythonVersion, buildDate?)` - Synchronous version
- `ListInstances()` - List all cached instances
- `ListAvailableVersionsAsync(releaseTag?)` - List available versions from GitHub
- `ListAvailableVersions(releaseTag?)` - Synchronous version

### BasePythonRuntime

- **Execution**:
  - `ExecuteCommandAsync(command, stdinHandler?, stdoutHandler?, stderrHandler?)` - Execute Python command
  - `ExecuteCommand(...)` - Synchronous version
  - `ExecuteScriptAsync(scriptPath, arguments?, stdinHandler?, stdoutHandler?, stderrHandler?)` - Execute Python script
  - `ExecuteScript(...)` - Synchronous version

- **Package Installation**:
  - `InstallPackageAsync(packageSpec, upgrade?)` - Install a single package
  - `InstallPackage(...)` - Synchronous version
  - `InstallRequirementsAsync(requirementsFile, upgrade?)` - Install from requirements.txt
  - `InstallRequirements(...)` - Synchronous version
  - `InstallPyProjectAsync(pyProjectFile, editable?)` - Install from pyproject.toml
  - `InstallPyProject(...)` - Synchronous version

### BasePythonRootRuntime (extends BasePythonRuntime)

- **Virtual Environment Management**:
  - `GetOrCreateVirtualEnvironmentAsync(name, recreateIfExists?)` - Get or create a virtual environment
  - `GetOrCreateVirtualEnvironment(...)` - Synchronous version
  - `DeleteVirtualEnvironmentAsync(name)` - Delete a virtual environment
  - `DeleteVirtualEnvironment(name)` - Synchronous version
  - `ListVirtualEnvironments()` - List all virtual environments

### PythonExecutionResult

A record type containing execution results:
```csharp
public record PythonExecutionResult(
    int ExitCode,
    string StandardOutput = "",
    string StandardError = "");
```

## GitHub API Authentication

For higher rate limits, provide an authenticated GitHub client:

```csharp
var githubClient = new GitHubClient(new ProductHeaderValue("MyApp"))
{
    Credentials = new Credentials("your-github-token")
};

var manager = new PythonManager("/path/to/instances", githubClient);
```

## Error Handling

The library uses custom exceptions for different error scenarios:

- `PythonInstallationException` - Base exception for installation errors
- `PythonNotInstalledException` - Python installation is missing or invalid
- `PlatformNotSupportedException` - Platform not supported
- `PythonExecutionException` - Python execution failed
- `PackageInstallationException` - Package installation failed
- `RequirementsFileException` - Requirements file installation failed
- `VirtualEnvironmentNotFoundException` - Virtual environment not found
- `InstanceNotFoundException` - Instance not found
- `PythonNetInitializationException` - Python.NET initialization failed
- `PythonNetExecutionException` - Python.NET execution failed
- And more...

## Design Principles

This library follows modern C# best practices:

- **Abstract Base Classes**: Extensible architecture using abstract base classes
- **Dependency Injection**: Support for DI containers with logger factories and caching
- **Resource Management**: IDisposable support for Python.NET runtimes
- **Modern C# Features**: Records, file-scoped namespaces, collection expressions, pattern matching, ConfigureAwait(false)
- **Structured Logging**: Full Microsoft.Extensions.Logging integration
- **Separation of Concerns**: Process execution extracted to a service
- **Performance Optimizations**: Optional caching, object pooling for hot paths

## Contributing

Contributions are welcome! Please read the contributing guidelines and submit pull requests.

## License

MIT License - see LICENSE file for details.

## Acknowledgments

This library utilizes [python-build-standalone](https://github.com/astral-sh/python-build-standalone) by [astral-sh](https://github.com/astral-sh) for providing high-quality, redistributable Python distributions. We are not associated with astral-sh, but we thank them for their fantastic work that makes this library possible.

## Links

- [Python Build Standalone](https://github.com/astral-sh/python-build-standalone) - Source of Python distributions
- [Python.NET](https://github.com/pythonnet/pythonnet) - Python.NET integration
- [Octokit](https://github.com/octokit/octokit.net) - GitHub API client
