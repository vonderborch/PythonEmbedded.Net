# Examples

This document provides comprehensive examples for using PythonEmbedded.Net.

## Table of Contents

- [Basic Usage](#basic-usage)
- [Virtual Environments](#virtual-environments)
- [Package Management](#package-management)
- [Script Execution](#script-execution)
- [Input/Output Handling](#inputoutput-handling)
- [Error Handling](#error-handling)
- [Dependency Injection](#dependency-injection)
- [Multiple Python Versions](#multiple-python-versions)
- [Resource Management](#resource-management)

## Basic Usage

### Simple Command Execution

```csharp
using PythonEmbedded.Net;
using Octokit;

var manager = new PythonManager(
    "./python-instances",
    new GitHubClient(new ProductHeaderValue("MyApp")));

var runtime = await manager.GetOrCreateInstanceAsync("3.12.0");
var result = await runtime.ExecuteCommandAsync("print('Hello, World!')");

Console.WriteLine($"Exit Code: {result.ExitCode}");
Console.WriteLine($"Output: {result.StandardOutput}");
```

### Getting Python Version

```csharp
var runtime = await manager.GetOrCreateInstanceAsync("3.12.0");
var result = await runtime.ExecuteCommandAsync("import sys; print(sys.version)");
Console.WriteLine(result.StandardOutput);
```

### Listing Installed Packages

```csharp
var runtime = await manager.GetOrCreateInstanceAsync("3.12.0");
var result = await runtime.ExecuteCommandAsync("import pkg_resources; [print(d.project_name) for d in pkg_resources.working_set]");
// Note: This requires setuptools. Use pip list for a more reliable approach:
var pipResult = await runtime.ExecuteCommandAsync("-m pip list");
Console.WriteLine(pipResult.StandardOutput);
```

## Virtual Environments

### Creating and Using Virtual Environments

```csharp
var runtime = await manager.GetOrCreateInstanceAsync("3.12.0");
var rootRuntime = (IPythonRootRuntime)runtime;

// Create a virtual environment
var venv = await rootRuntime.GetOrCreateVirtualEnvironmentAsync("myproject");

// Install packages in the virtual environment
await venv.InstallPackageAsync("numpy");
await venv.InstallPackageAsync("pandas");

// Execute code in the virtual environment
var result = await venv.ExecuteCommandAsync(
    "import numpy as np; import pandas as pd; print(f'NumPy: {np.__version__}, Pandas: {pd.__version__}')");
Console.WriteLine(result.StandardOutput);
```

### Listing Virtual Environments

```csharp
var rootRuntime = (IPythonRootRuntime)runtime;
var venvNames = rootRuntime.ListVirtualEnvironments();

foreach (var name in venvNames)
{
    Console.WriteLine($"Virtual environment: {name}");
}
```

### Recreating Virtual Environments

```csharp
// Recreate an existing virtual environment
var venv = await rootRuntime.GetOrCreateVirtualEnvironmentAsync(
    "myproject", 
    recreateIfExists: true);
```

### Deleting Virtual Environments

```csharp
// Delete a virtual environment
var deleted = await rootRuntime.DeleteVirtualEnvironmentAsync("myproject");
```

### Cloning Virtual Environments

```csharp
// Clone an existing virtual environment
var clonedVenv = await rootRuntime.CloneVirtualEnvironmentAsync("source_venv", "cloned_venv");

// The cloned environment will have all the same packages
Assert.That(await clonedVenv.IsPackageInstalledAsync("numpy"), Is.True);
```

### Exporting and Importing Virtual Environments

```csharp
// Export a virtual environment to an archive
var exportPath = await rootRuntime.ExportVirtualEnvironmentAsync("myproject", "./backups/myproject_venv.zip");

// Later, import it back
var importedVenv = await rootRuntime.ImportVirtualEnvironmentAsync("restored_project", "./backups/myproject_venv.zip");
```
if (deleted)
{
    Console.WriteLine("Virtual environment deleted successfully");
}
```

## Package Management

### Installing Single Packages

```csharp
var runtime = await manager.GetOrCreateInstanceAsync("3.12.0");

// Install latest version
await runtime.InstallPackageAsync("requests");

// Install specific version
await runtime.InstallPackageAsync("requests==2.31.0");

// Install with version constraint
await runtime.InstallPackageAsync("numpy>=1.20.0");

// Upgrade package
await runtime.InstallPackageAsync("requests", upgrade: true);
```

### Installing from requirements.txt

```csharp
// Create a requirements.txt file
var requirementsPath = "requirements.txt";
await File.WriteAllTextAsync(requirementsPath, @"
numpy>=1.20.0
pandas==2.0.0
requests
");

// Install packages
var runtime = await manager.GetOrCreateInstanceAsync("3.12.0");
await runtime.InstallRequirementsAsync(requirementsPath);

// Or with upgrade
await runtime.InstallRequirementsAsync(requirementsPath, upgrade: true);
```

### Installing from pyproject.toml

```csharp
var runtime = await manager.GetOrCreateInstanceAsync("3.12.0");

// Install in normal mode
await runtime.InstallPyProjectAsync("./my-python-project");

// Install in editable mode
await runtime.InstallPyProjectAsync("./my-python-project", editable: true);
```

### Installing to Virtual Environment

```csharp
var runtime = await manager.GetOrCreateInstanceAsync("3.12.0");
var rootRuntime = (IPythonRootRuntime)runtime;

var venv = await rootRuntime.GetOrCreateVirtualEnvironmentAsync("myproject");

// Install packages in the virtual environment
await venv.InstallPackageAsync("flask");
await venv.InstallPackageAsync("sqlalchemy");
```

## Script Execution

### Executing a Python Script

```csharp
var runtime = await manager.GetOrCreateInstanceAsync("3.12.0");

// Execute a script without arguments
var result = await runtime.ExecuteScriptAsync("script.py");

// Execute a script with arguments
var result2 = await runtime.ExecuteScriptAsync(
    "script.py", 
    arguments: new[] { "arg1", "arg2", "arg3" });
```

### Creating and Executing a Script

```csharp
// Create a simple script
var scriptContent = @"
import sys
print(f'Arguments: {sys.argv[1:]}')
print('Hello from Python script!')
";
await File.WriteAllTextAsync("hello.py", scriptContent);

// Execute it
var runtime = await manager.GetOrCreateInstanceAsync("3.12.0");
var result = await runtime.ExecuteScriptAsync("hello.py", arguments: new[] { "world", "test" });
Console.WriteLine(result.StandardOutput);
```

## Input/Output Handling

### Providing Input via stdin

```csharp
var runtime = await manager.GetOrCreateInstanceAsync("3.12.0");

var inputLines = new[] { "line1", "line2", "line3" };
int lineIndex = 0;

var result = await runtime.ExecuteCommandAsync(
    "import sys; [print(f'Received: {line}') for line in sys.stdin]",
    stdinHandler: () => lineIndex < inputLines.Length ? inputLines[lineIndex++] : null);

Console.WriteLine(result.StandardOutput);
```

### Processing Output Line by Line

```csharp
var runtime = await manager.GetOrCreateInstanceAsync("3.12.0");

var outputLines = new List<string>();

var result = await runtime.ExecuteCommandAsync(
    "for i in range(5): print(f'Line {i}')",
    stdoutHandler: line => 
    {
        outputLines.Add(line);
        Console.WriteLine($"[STDOUT] {line}");
    });

Console.WriteLine($"Captured {outputLines.Count} lines");
```

### Processing Errors

```csharp
var runtime = await manager.GetOrCreateInstanceAsync("3.12.0");

var errors = new List<string>();

var result = await runtime.ExecuteCommandAsync(
    "import sys; sys.stderr.write('Error message 1\\n'); sys.stderr.write('Error message 2\\n')",
    stderrHandler: line =>
    {
        errors.Add(line);
        Console.Error.WriteLine($"[STDERR] {line}");
    });

Console.WriteLine($"Captured {errors.Count} error lines");
```

### Complete I/O Example

```csharp
var runtime = await manager.GetOrCreateInstanceAsync("3.12.0");

var inputData = new[] { "5", "10", "15" };
int inputIndex = 0;
var outputs = new List<string>();
var errors = new List<string>();

var result = await runtime.ExecuteCommandAsync(
    @"
import sys
for line in sys.stdin:
    num = int(line.strip())
    result = num * 2
    print(f'{num} * 2 = {result}')
    sys.stderr.write(f'Processed: {num}\n')
",
    stdinHandler: () => inputIndex < inputData.Length ? inputData[inputIndex++] : null,
    stdoutHandler: line => outputs.Add(line),
    stderrHandler: line => errors.Add(line));

Console.WriteLine($"Outputs: {string.Join(", ", outputs)}");
Console.WriteLine($"Errors: {string.Join(", ", errors)}");
```

## Error Handling

### Handling Package Installation Errors

```csharp
try
{
    var runtime = await manager.GetOrCreateInstanceAsync("3.12.0");
    await runtime.InstallPackageAsync("nonexistent-package-xyz");
}
catch (PackageInstallationException ex)
{
    Console.WriteLine($"Package installation failed: {ex.Message}");
    Console.WriteLine($"Package: {ex.PackageSpecification}");
    if (!string.IsNullOrEmpty(ex.InstallationOutput))
    {
        Console.WriteLine($"Output: {ex.InstallationOutput}");
    }
}
```

### Handling Execution Errors

```csharp
try
{
    var runtime = await manager.GetOrCreateInstanceAsync("3.12.0");
    var result = await runtime.ExecuteCommandAsync("raise ValueError('Test error')");
    
    if (result.ExitCode != 0)
    {
        Console.WriteLine($"Non-zero exit code: {result.ExitCode}");
        Console.WriteLine($"Error output: {result.StandardError}");
    }
}
catch (PythonExecutionException ex)
{
    Console.WriteLine($"Execution failed: {ex.Message}");
    Console.WriteLine($"Exit Code: {ex.ExitCode}");
    Console.WriteLine($"Error: {ex.StandardError}");
}
```

### Handling Instance Not Found

```csharp
try
{
    var runtime = await manager.GetOrCreateInstanceAsync("99.99.99");
}
catch (InstanceNotFoundException ex)
{
    Console.WriteLine($"Python version not found: {ex.PythonVersion}");
    Console.WriteLine($"Build date: {ex.BuildDate ?? "latest"}");
}
```

### Checking for Virtual Environment

```csharp
var rootRuntime = (IPythonRootRuntime)runtime;

try
{
    var venv = await rootRuntime.GetOrCreateVirtualEnvironmentAsync("myenv");
}
catch (PythonInstallationException ex)
{
    Console.WriteLine($"Virtual environment creation failed: {ex.Message}");
}
```

## Dependency Injection

### ASP.NET Core Integration

```csharp
// In Startup.cs or Program.cs
services.AddMemoryCache(); // Optional: enables caching of GitHub API responses

services.AddSingleton<GitHubClient>(sp =>
{
    var token = configuration["GitHub:Token"];
    return new GitHubClient(new ProductHeaderValue("MyApp"))
    {
        Credentials = token != null ? new Credentials(token) : null
    };
});

services.AddSingleton<IPythonManager>(sp =>
{
    var githubClient = sp.GetRequiredService<GitHubClient>();
    var loggerFactory = sp.GetRequiredService<ILoggerFactory>();
    var cache = sp.GetService<IMemoryCache>(); // Optional: improves performance
    var logger = loggerFactory.CreateLogger<PythonManager>();
    
    return new PythonManager(
        configuration["Python:InstancesDirectory"],
        githubClient,
        logger,
        loggerFactory,
        cache);
});

// In a service
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

## Multiple Python Versions

### Managing Multiple Versions

```csharp
var manager = new PythonManager("./python-instances", githubClient);

// Get different Python versions
var python312 = await manager.GetOrCreateInstanceAsync("3.12.0");
var python311 = await manager.GetOrCreateInstanceAsync("3.11.0");
var python310 = await manager.GetOrCreateInstanceAsync("3.10.0");

// Use different versions for different tasks
var result312 = await python312.ExecuteCommandAsync("print('Python 3.12')");
var result311 = await python311.ExecuteCommandAsync("print('Python 3.11')");
```

### Listing All Instances

```csharp
var manager = new PythonManager("./python-instances", githubClient);
var instances = manager.ListInstances();

foreach (var instance in instances)
{
    Console.WriteLine($"Version: {instance.PythonVersion}");
    Console.WriteLine($"Build Date: {instance.BuildDate}");
    Console.WriteLine($"Directory: {instance.Directory}");
    Console.WriteLine($"Installed: {instance.InstallationDate}");
    Console.WriteLine();
}
```

### Finding Available Versions

```csharp
var manager = new PythonManager("./python-instances", githubClient);

// List all available versions
var versions = await manager.ListAvailableVersionsAsync();
foreach (var version in versions)
{
    Console.WriteLine($"Available: {version}");
}
```

## Resource Management

### Disposing Python.NET Runtimes

```csharp
var netManager = new PythonNetManager("./python-instances", githubClient);

IPythonRuntime runtime = null;
try
{
    runtime = await netManager.GetOrCreateInstanceAsync("3.12.0");
    
    // Use the runtime
    var result = await runtime.ExecuteCommandAsync("print('Hello')");
}
finally
{
    // Dispose Python.NET runtimes
    if (runtime is IDisposable disposable)
    {
        disposable.Dispose();
    }
}
```

### Using Statement Pattern

```csharp
var netManager = new PythonNetManager("./python-instances", githubClient);
var runtime = await netManager.GetOrCreateInstanceAsync("3.12.0");

if (runtime is IDisposable disposable)
{
    using (disposable)
    {
        var result = await runtime.ExecuteCommandAsync("print('Hello')");
    }
}
```

## Advanced Examples

### Building a Python Script Runner Service

```csharp
public class PythonScriptRunner
{
    private readonly IPythonManager _pythonManager;
    private readonly ILogger<PythonScriptRunner> _logger;
    
    public PythonScriptRunner(
        IPythonManager pythonManager,
        ILogger<PythonScriptRunner> logger)
    {
        _pythonManager = pythonManager;
        _logger = logger;
    }
    
    public async Task<ScriptExecutionResult> RunScriptAsync(
        string scriptPath,
        string pythonVersion = "3.12.0",
        IEnumerable<string>? arguments = null)
    {
        try
        {
            var runtime = await _pythonManager.GetOrCreateInstanceAsync(pythonVersion);
            var result = await runtime.ExecuteScriptAsync(scriptPath, arguments);
            
            return new ScriptExecutionResult
            {
                Success = result.ExitCode == 0,
                Output = result.StandardOutput,
                Error = result.StandardError,
                ExitCode = result.ExitCode
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Script execution failed: {ScriptPath}", scriptPath);
            throw;
        }
    }
}

public class ScriptExecutionResult
{
    public bool Success { get; set; }
    public string Output { get; set; } = "";
    public string Error { get; set; } = "";
    public int ExitCode { get; set; }
}
```

### Isolated Package Installation

```csharp
public async Task<IPythonVirtualRuntime> SetupProjectEnvironmentAsync(
    string projectName,
    string requirementsPath)
{
    var runtime = await _pythonManager.GetOrCreateInstanceAsync("3.12.0");
    var rootRuntime = (IPythonRootRuntime)runtime;
    
    // Create isolated virtual environment
    var venv = await rootRuntime.GetOrCreateVirtualEnvironmentAsync(
        projectName, 
        recreateIfExists: true);
    
    // Install requirements
    await venv.InstallRequirementsAsync(requirementsPath);
    
    return venv;
}
```

## See Also

- [Getting Started](Getting-Started.md)
- [API Reference](API-Reference.md)
- [Architecture](Architecture.md)
- [Error Handling](Error-Handling.md)

