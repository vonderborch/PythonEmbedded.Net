# Error Handling

This document describes the exception hierarchy and best practices for error handling in PythonEmbedded.Net.

## Exception Hierarchy

PythonEmbedded.Net uses a hierarchical exception structure to provide clear, actionable error information:

```
Exception
├── PythonInstallationException (base for installation issues)
│   ├── InstanceNotFoundException
│   ├── InvalidPythonVersionException
│   ├── MetadataCorruptedException
│   ├── PlatformNotSupportedException
│   └── PythonNotInstalledException
├── PackageInstallationException (base for package issues)
│   ├── InvalidPackageSpecificationException
│   └── RequirementsFileException
├── PythonExecutionException (base for execution issues)
│   └── PythonNetExecutionException
├── PythonNetInitializationException
└── VirtualEnvironmentNotFoundException
```

## Exception Reference

### PythonInstallationException

Base exception for all Python installation-related errors.

**Properties:** None specific to base class.

**When thrown:**
- General installation failures
- Base class for more specific installation exceptions

### InstanceNotFoundException

Thrown when a requested Python instance cannot be found.

**Properties:**
- `PythonVersion` (string?): The Python version that was requested
- `BuildDate` (DateTime?): The build date that was requested

**When thrown:**
- `GetOrCreateInstanceAsync` cannot find a matching release on GitHub
- Requested version/build date combination doesn't exist

**Example:**
```csharp
try
{
    var runtime = await manager.GetOrCreateInstanceAsync("99.99.99");
}
catch (InstanceNotFoundException ex)
{
    Console.WriteLine($"Python {ex.PythonVersion} not found");
    Console.WriteLine($"Build date: {ex.BuildDate?.ToString("yyyy-MM-dd") ?? "latest"}");
}
```

### InvalidPythonVersionException

Thrown when an invalid Python version string is provided.

**Properties:**
- `InvalidVersion` (string?): The invalid version string

**When thrown:**
- Version string cannot be parsed
- Version format is invalid

**Example:**
```csharp
try
{
    var runtime = await manager.GetOrCreateInstanceAsync("invalid-version");
}
catch (InvalidPythonVersionException ex)
{
    Console.WriteLine($"Invalid version: {ex.InvalidVersion}");
}
```

### MetadataCorruptedException

Thrown when instance metadata is corrupted or unreadable.

**Properties:**
- `MetadataFilePath` (string?): Path to the corrupted metadata file

**When thrown:**
- Metadata file is corrupted
- Metadata file cannot be parsed

**Example:**
```csharp
try
{
    var runtime = await manager.GetOrCreateInstanceAsync("3.12.0");
}
catch (MetadataCorruptedException ex)
{
    Console.WriteLine($"Corrupted metadata at: {ex.MetadataFilePath}");
    // May need to delete the instance directory and reinstall
}
```

### PlatformNotSupportedException

Thrown when the current platform is not supported.

**Properties:**
- `Platform` (string?): The platform that was requested or detected

**When thrown:**
- Current platform cannot be detected
- Platform is not supported by python-build-standalone

**Example:**
```csharp
try
{
    var runtime = await manager.GetOrCreateInstanceAsync("3.12.0");
}
catch (PlatformNotSupportedException ex)
{
    Console.WriteLine($"Platform not supported: {ex.Platform}");
}
```

### PythonNotInstalledException

Thrown when Python installation is missing or invalid.

**Properties:** None specific.

**When thrown:**
- Python executable not found after installation
- Installation verification fails

**Example:**
```csharp
try
{
    var runtime = await manager.GetOrCreateInstanceAsync("3.12.0");
    await runtime.ExecuteCommandAsync("print('test')");
}
catch (PythonNotInstalledException ex)
{
    Console.WriteLine("Python installation is invalid");
}
```

### PackageInstallationException

Base exception for package installation failures.

**Properties:**
- `PackageSpecification` (string?): The package that failed to install
- `InstallationOutput` (string?): Output from pip installation

**When thrown:**
- Package installation fails
- Base class for more specific package exceptions

**Example:**
```csharp
try
{
    await runtime.InstallPackageAsync("invalid-package-name");
}
catch (PackageInstallationException ex)
{
    Console.WriteLine($"Package installation failed: {ex.PackageSpecification}");
    Console.WriteLine($"Output: {ex.InstallationOutput}");
}
```

### InvalidPackageSpecificationException

Thrown when a package specification is invalid.

**Properties:** Inherits from `PackageInstallationException`

**When thrown:**
- Package specification format is invalid
- Package name is empty or null

**Example:**
```csharp
try
{
    await runtime.InstallPackageAsync("");
}
catch (InvalidPackageSpecificationException ex)
{
    Console.WriteLine("Package specification is invalid");
}
```

### RequirementsFileException

Thrown when installation from requirements.txt fails.

**Properties:**
- `RequirementsFilePath` (string?): Path to the requirements file
- Inherits `PackageSpecification` and `InstallationOutput` from base

**When thrown:**
- Requirements file not found
- Installation from requirements.txt fails

**Example:**
```csharp
try
{
    await runtime.InstallRequirementsAsync("nonexistent.txt");
}
catch (RequirementsFileException ex)
{
    Console.WriteLine($"Requirements file failed: {ex.RequirementsFilePath}");
    Console.WriteLine($"Output: {ex.InstallationOutput}");
}
```

### PythonExecutionException

Base exception for Python execution failures.

**Properties:**
- `ExitCode` (int?): Exit code from the Python process
- `StandardError` (string?): Standard error output

**When thrown:**
- Process execution fails (not just non-zero exit code)
- Process cannot be started

**Example:**
```csharp
try
{
    var result = await runtime.ExecuteCommandAsync("invalid syntax here");
}
catch (PythonExecutionException ex)
{
    Console.WriteLine($"Execution failed: {ex.Message}");
    Console.WriteLine($"Exit code: {ex.ExitCode}");
    Console.WriteLine($"Error: {ex.StandardError}");
}
```

**Note:** A non-zero exit code does not always throw an exception. Check `result.ExitCode` for execution status.

### PythonNetExecutionException

Thrown when Python.NET code execution fails.

**Properties:**
- `PythonExceptionType` (string?): Type of the Python exception
- `PythonTraceback` (string?): Python traceback information
- Inherits `ExitCode` and `StandardError` from base

**When thrown:**
- Python.NET execution raises an exception
- Python code execution fails in-process

**Example:**
```csharp
try
{
    // Using PythonNetManager
    var runtime = await netManager.GetOrCreateInstanceAsync("3.12.0");
    await runtime.ExecuteCommandAsync("raise ValueError('test')");
}
catch (PythonNetExecutionException ex)
{
    Console.WriteLine($"Python exception type: {ex.PythonExceptionType}");
    Console.WriteLine($"Traceback: {ex.PythonTraceback}");
}
```

### PythonNetInitializationException

Thrown when Python.NET initialization fails.

**Properties:**
- `PythonInstallPath` (string?): Path to the Python installation

**When thrown:**
- Python.NET cannot initialize
- Python DLL not found
- Python.NET initialization error

**Example:**
```csharp
try
{
    var netManager = new PythonNetManager("./instances", githubClient);
    var runtime = await netManager.GetOrCreateInstanceAsync("3.12.0");
}
catch (PythonNetInitializationException ex)
{
    Console.WriteLine($"Python.NET initialization failed: {ex.Message}");
    Console.WriteLine($"Python path: {ex.PythonInstallPath}");
}
```

### VirtualEnvironmentNotFoundException

Thrown when a virtual environment is not found.

**Properties:**
- `VirtualEnvironmentName` (string?): Name of the virtual environment

**When thrown:**
- Virtual environment doesn't exist when expected
- Virtual environment path is invalid

**Example:**
```csharp
try
{
    var rootRuntime = (IPythonRootRuntime)runtime;
    await rootRuntime.DeleteVirtualEnvironmentAsync("nonexistent");
}
catch (VirtualEnvironmentNotFoundException ex)
{
    Console.WriteLine($"Virtual environment not found: {ex.VirtualEnvironmentName}");
}
```

## Best Practices

### Always Check Exit Codes

Even when exceptions aren't thrown, check exit codes:

```csharp
var result = await runtime.ExecuteCommandAsync("some-command");
if (result.ExitCode != 0)
{
    Console.WriteLine($"Command failed with exit code {result.ExitCode}");
    Console.WriteLine($"Error: {result.StandardError}");
}
```

### Handle Specific Exceptions

Catch specific exceptions for better error handling:

```csharp
try
{
    await runtime.InstallPackageAsync("package-name");
}
catch (PackageInstallationException ex) when (ex is InvalidPackageSpecificationException)
{
    // Handle invalid specification
}
catch (PackageInstallationException ex)
{
    // Handle other package installation errors
    Console.WriteLine($"Installation output: {ex.InstallationOutput}");
}
```

### Log Exception Details

Use structured logging to capture exception details:

```csharp
try
{
    var runtime = await manager.GetOrCreateInstanceAsync(version);
}
catch (InstanceNotFoundException ex)
{
    _logger.LogError(
        "Python instance not found: Version={Version}, BuildDate={BuildDate}",
        ex.PythonVersion,
        ex.BuildDate?.ToString("yyyy-MM-dd") ?? "latest");
    throw;
}
```

### Provide User-Friendly Messages

Transform exceptions into user-friendly messages:

```csharp
try
{
    await runtime.InstallPackageAsync(packageName);
}
catch (PackageInstallationException ex)
{
    var userMessage = $"Failed to install {ex.PackageSpecification}. " +
                     $"Please check the package name and try again.";
    throw new UserFriendlyException(userMessage, ex);
}
```

### Handle Metadata Corruption

If metadata is corrupted, provide recovery options:

```csharp
try
{
    var runtime = await manager.GetOrCreateInstanceAsync("3.12.0");
}
catch (MetadataCorruptedException ex)
{
    _logger.LogWarning(ex, "Metadata corrupted, attempting recovery");
    
    // Option 1: Delete and reinstall
    await manager.DeleteInstanceAsync("3.12.0");
    var runtime = await manager.GetOrCreateInstanceAsync("3.12.0");
    
    // Option 2: Manual cleanup
    // Directory.Delete(ex.MetadataFilePath, true);
}
```

### Handle Network Issues

GitHub API calls can fail due to network issues:

```csharp
try
{
    var runtime = await manager.GetOrCreateInstanceAsync("3.12.0");
}
catch (HttpRequestException ex)
{
    _logger.LogError(ex, "Network error while downloading Python distribution");
    // Implement retry logic or fallback
}
catch (InstanceNotFoundException ex)
{
    // Handle case where version doesn't exist
}
```

## Common Error Scenarios

### Python Version Not Available

```csharp
try
{
    var runtime = await manager.GetOrCreateInstanceAsync("99.99.99");
}
catch (InstanceNotFoundException)
{
    // Check available versions
    var versions = await manager.ListAvailableVersionsAsync();
    Console.WriteLine("Available versions:");
    foreach (var v in versions.Take(10))
    {
        Console.WriteLine($"  {v}");
    }
}
```

### Package Installation Failure

```csharp
var result = await runtime.InstallPackageAsync("package-name");
if (result.ExitCode != 0)
{
    Console.WriteLine($"Installation failed: {result.StandardError}");
    
    // Check if package exists on PyPI
    var packageInfo = await runtime.GetPackageMetadataAsync("package-name");
    if (packageInfo == null)
    {
        Console.WriteLine("Package not found on PyPI");
    }
}
```

### Virtual Environment Creation Failure

```csharp
try
{
    var venv = await rootRuntime.GetOrCreateVirtualEnvironmentAsync("myenv");
}
catch (PythonInstallationException ex)
{
    // Verify Python installation
    var versionResult = await runtime.ExecuteCommandAsync("--version");
    if (versionResult.ExitCode != 0)
    {
        // Python installation is corrupted
        await manager.DeleteInstanceAsync("3.12.0");
        var newRuntime = await manager.GetOrCreateInstanceAsync("3.12.0");
    }
}
```

## See Also

- [API Reference](API-Reference.md)
- [Examples](Examples.md)
- [Troubleshooting](Troubleshooting.md)

