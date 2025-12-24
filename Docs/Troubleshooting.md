# Troubleshooting

This guide helps you diagnose and resolve common issues with PythonEmbedded.Net.

## Table of Contents

- [Installation Issues](#installation-issues)
- [Download Issues](#download-issues)
- [Execution Issues](#execution-issues)
- [Virtual Environment Issues](#virtual-environment-issues)
- [Package Installation Issues](#package-installation-issues)
- [Python.NET Issues](#pythonnet-issues)
- [Performance Issues](#performance-issues)
- [Platform-Specific Issues](#platform-specific-issues)

## Installation Issues

### Python Instance Not Found

**Symptom:** `InstanceNotFoundException` when trying to get a Python instance.

**Possible Causes:**
1. Python version doesn't exist in python-build-standalone releases
2. Network connectivity issues preventing download
3. GitHub API rate limiting

**Solutions:**

```csharp
// Check available versions first
var versions = await manager.ListAvailableVersionsAsync();
foreach (var version in versions.Take(20))
{
    Console.WriteLine($"Available: {version}");
}

// Use a valid version
var runtime = await manager.GetOrCreateInstanceAsync("3.12.0");
```

**For GitHub API rate limiting:**
```csharp
// Use an authenticated GitHub client
var githubClient = new GitHubClient(new ProductHeaderValue("MyApp"))
{
    Credentials = new Credentials("your-github-token")
};

var manager = new PythonManager("./instances", githubClient);
```

### Installation Verification Fails

**Symptom:** Python installation completes but verification fails.

**Possible Causes:**
1. Archive extraction incomplete
2. Corrupted download
3. Platform-specific executable path issues

**Solutions:**

```csharp
// Delete and reinstall
try
{
    await manager.DeleteInstanceAsync("3.12.0");
    var runtime = await manager.GetOrCreateInstanceAsync("3.12.0");
}
catch (Exception ex)
{
    // Check logs for detailed error information
    _logger.LogError(ex, "Reinstallation failed");
}
```

**Manual cleanup:**
- Delete the instance directory manually
- Clear any partial downloads from temp directories
- Retry installation

## Download Issues

### Slow Downloads

**Symptom:** Python distribution downloads are very slow.

**Solutions:**
- Use an authenticated GitHub client for higher rate limits
- Check network connectivity
- Consider using a proxy if in a restricted network

```csharp
var githubClient = new GitHubClient(new ProductHeaderValue("MyApp"))
{
    Credentials = new Credentials("github-token")
};
```

### Download Interrupted

**Symptom:** Download fails partway through.

**Solutions:**
- The library should handle partial downloads, but if issues persist:
- Clear temporary download directories
- Check disk space availability
- Verify network stability

### Archive Extraction Fails

**Symptom:** `NotSupportedException` for `.tar.zst` or `.tar` archives.

**Possible Causes:**
- Missing system tools (zstd, tar)

**Solutions:**

**Windows:**
- `.zip` archives should work without additional tools
- For `.tar.zst`, install 7-Zip or use WSL

**Linux/macOS:**
```bash
# Install required tools
sudo apt-get install zstd tar  # Ubuntu/Debian
brew install zstd              # macOS
```

**Alternative:**
- The library will prefer `.zip` when available
- Check available archive formats for your platform

## Execution Issues

### Command Execution Returns Non-Zero Exit Code

**Symptom:** `result.ExitCode != 0` but no exception thrown.

**Note:** This is expected behavior. Python commands can fail without throwing exceptions.

**Solutions:**

```csharp
var result = await runtime.ExecuteCommandAsync("some-command");
if (result.ExitCode != 0)
{
    Console.WriteLine($"Command failed: {result.StandardError}");
    // Handle failure appropriately
}
```

### Process Execution Timeout

**Symptom:** Long-running Python commands don't complete.

**Solutions:**

```csharp
using var cts = new CancellationTokenSource(TimeSpan.FromMinutes(5));
try
{
    var result = await runtime.ExecuteCommandAsync(
        "long-running-script.py",
        cancellationToken: cts.Token);
}
catch (OperationCanceledException)
{
    Console.WriteLine("Command was cancelled due to timeout");
}
```

### Python Executable Not Found

**Symptom:** `PythonNotInstalledException` or process fails to start.

**Possible Causes:**
1. Python installation incomplete
2. Wrong executable path for platform
3. Missing executable permissions (Unix)

**Solutions:**

```csharp
// Verify installation
var versionResult = await runtime.ExecuteCommandAsync("--version");
if (versionResult.ExitCode != 0)
{
    // Reinstall
    await manager.DeleteInstanceAsync("3.12.0");
    var newRuntime = await manager.GetOrCreateInstanceAsync("3.12.0");
}
```

**Unix permissions:**
```bash
# Ensure executable has proper permissions
chmod +x python-instances/python-3.12.0-*/bin/python3
```

## Virtual Environment Issues

### Virtual Environment Creation Fails

**Symptom:** `PythonInstallationException` when creating venv.

**Possible Causes:**
1. Python installation missing `venv` module
2. Insufficient permissions
3. Path issues

**Solutions:**

```csharp
// Verify Python installation
var runtime = await manager.GetOrCreateInstanceAsync("3.12.0");
var venvResult = await runtime.ExecuteCommandAsync("-m venv --help");
if (venvResult.ExitCode != 0)
{
    // venv module missing - reinstall Python
    await manager.DeleteInstanceAsync("3.12.0");
    runtime = await manager.GetOrCreateInstanceAsync("3.12.0");
}

// Try creating venv
try
{
    var rootRuntime = (IPythonRootRuntime)runtime;
    var venv = await rootRuntime.GetOrCreateVirtualEnvironmentAsync("testenv");
}
catch (PythonInstallationException ex)
{
    // Check logs for specific error
    _logger.LogError(ex, "Venv creation failed");
}
```

### Virtual Environment Not Recognized

**Symptom:** Existing virtual environment not found or invalid.

**Solutions:**

```csharp
// List existing venvs
var rootRuntime = (IPythonRootRuntime)runtime;
var venvs = rootRuntime.ListVirtualEnvironments();
foreach (var name in venvs)
{
    Console.WriteLine($"Found venv: {name}");
}

// Recreate if needed
var venv = await rootRuntime.GetOrCreateVirtualEnvironmentAsync(
    "myenv",
    recreateIfExists: true);
```

### Packages Not Found in Virtual Environment

**Symptom:** Packages installed but not found when executing.

**Possible Causes:**
1. Package installed to wrong environment
2. Virtual environment not activated properly

**Solutions:**

```csharp
// Ensure you're using the venv runtime, not root runtime
var rootRuntime = (IPythonRootRuntime)runtime;
var venv = await rootRuntime.GetOrCreateVirtualEnvironmentAsync("myenv");

// Install to venv
await venv.InstallPackageAsync("numpy");

// Execute in venv context
var result = await venv.ExecuteCommandAsync("import numpy; print(numpy.__version__)");
```

## Package Installation Issues

### Package Installation Fails

**Symptom:** `PackageInstallationException` or non-zero exit code.

**Solutions:**

```csharp
try
{
    await runtime.InstallPackageAsync("package-name");
}
catch (PackageInstallationException ex)
{
    // Check installation output
    Console.WriteLine($"Installation output: {ex.InstallationOutput}");
    
    // Check if package exists
    var searchResult = await runtime.ExecuteCommandAsync(
        $"-m pip search {ex.PackageSpecification}");
    
    // Try with upgrade flag
    await runtime.InstallPackageAsync(ex.PackageSpecification, upgrade: true);
}
```

### Package Version Conflicts

**Symptom:** Package installation fails due to dependency conflicts.

**Solutions:**

```csharp
// Use virtual environments to isolate dependencies
var rootRuntime = (IPythonRootRuntime)runtime;
var venv = await rootRuntime.GetOrCreateVirtualEnvironmentAsync("isolated-env");
await venv.InstallPackageAsync("conflicting-package");
```

### Requirements File Installation Fails

**Symptom:** `RequirementsFileException` when installing from requirements.txt.

**Solutions:**

```csharp
try
{
    await runtime.InstallRequirementsAsync("requirements.txt");
}
catch (RequirementsFileException ex)
{
    // Check file path
    if (!File.Exists(ex.RequirementsFilePath))
    {
        Console.WriteLine($"Requirements file not found: {ex.RequirementsFilePath}");
    }
    
    // Check installation output
    Console.WriteLine($"Output: {ex.InstallationOutput}");
    
    // Install packages individually to identify problematic ones
    var requirements = await File.ReadAllLinesAsync(ex.RequirementsFilePath);
    foreach (var requirement in requirements)
    {
        try
        {
            await runtime.InstallPackageAsync(requirement.Trim());
        }
        catch (Exception e)
        {
            Console.WriteLine($"Failed to install {requirement}: {e.Message}");
        }
    }
}
```

## Python.NET Issues

### Python.NET Initialization Fails

**Symptom:** `PythonNetInitializationException` when using `PythonNetManager`.

**Possible Causes:**
1. Python DLL not found
2. Architecture mismatch (x64 vs x86)
3. Missing dependencies

**Solutions:**

```csharp
try
{
    var netManager = new PythonNetManager("./instances", githubClient);
    var runtime = await netManager.GetOrCreateInstanceAsync("3.12.0");
}
catch (PythonNetInitializationException ex)
{
    Console.WriteLine($"Python path: {ex.PythonInstallPath}");
    
    // Verify Python installation
    var manager = new PythonManager("./instances", githubClient);
    var standardRuntime = await manager.GetOrCreateInstanceAsync("3.12.0");
    var versionResult = await standardRuntime.ExecuteCommandAsync("--version");
    
    if (versionResult.ExitCode == 0)
    {
        // Python works, but Python.NET can't initialize
        // May need to reinstall or check architecture
    }
}
```

### Python.NET Execution Errors

**Symptom:** `PythonNetExecutionException` with Python traceback.

**Solutions:**

```csharp
try
{
    var result = await runtime.ExecuteCommandAsync("some-python-code");
}
catch (PythonNetExecutionException ex)
{
    // Check Python exception details
    Console.WriteLine($"Python exception type: {ex.PythonExceptionType}");
    Console.WriteLine($"Python traceback:\n{ex.PythonTraceback}");
    
    // Fix the Python code based on error
}
```

### Memory Issues with Python.NET

**Symptom:** Out of memory errors or crashes.

**Solutions:**

- Use subprocess mode (`PythonManager`) instead of Python.NET for memory-intensive operations
- Dispose Python.NET runtimes when done
- Limit concurrent Python.NET instances

```csharp
// Dispose properly
if (runtime is IDisposable disposable)
{
    disposable.Dispose();
}
```

## Performance Issues

### Slow Package Installation

**Symptom:** Package installation takes a very long time.

**Solutions:**

```csharp
// Use virtual environments to avoid reinstalling packages
var rootRuntime = (IPythonRootRuntime)runtime;
var venv = await rootRuntime.GetOrCreateVirtualEnvironmentAsync("myenv");

// Install packages in parallel (if supported)
// Or use requirements.txt for batch installation
await venv.InstallRequirementsAsync("requirements.txt");
```

### Slow Command Execution

**Symptom:** Simple Python commands take too long.

**Possible Causes:**
1. Python startup overhead
2. Large Python installation
3. Network operations in Python code

**Solutions:**

- For simple operations, consider Python.NET mode (faster startup)
- Cache frequently used runtimes
- Use virtual environments to reduce package loading time

## Platform-Specific Issues

### Windows Issues

**Executable Not Found:**
- Ensure using `python.exe` (not `python3`)
- Check PATH environment variables

**Permissions:**
- Run with appropriate permissions
- Check antivirus isn't blocking Python execution

### Linux Issues

**Missing System Libraries:**
```bash
# Check for missing dependencies
ldd python-instances/python-*/bin/python3

# Install missing libraries (Ubuntu/Debian)
sudo apt-get install -f
```

**Permissions:**
```bash
# Ensure executable permissions
chmod +x python-instances/python-*/bin/python3
```

### macOS Issues

**Code Signing:**
- Python distributions may need to be signed
- Check Gatekeeper settings

**Architecture Mismatch:**
- Ensure using correct architecture (Intel vs Apple Silicon)
- Use Rosetta if needed for x64 on Apple Silicon

## Debugging Tips

### Enable Detailed Logging

```csharp
using var loggerFactory = LoggerFactory.Create(builder =>
{
    builder
        .AddConsole()
        .SetMinimumLevel(LogLevel.Debug); // Enable debug logging
});

var manager = new PythonManager(
    "./instances",
    githubClient,
    logger: loggerFactory.CreateLogger<PythonManager>(),
    loggerFactory: loggerFactory);
```

### Verify Installation Manually

```csharp
// Check Python version
var result = await runtime.ExecuteCommandAsync("--version");
Console.WriteLine($"Python version: {result.StandardOutput}");

// Check pip
var pipResult = await runtime.ExecuteCommandAsync("-m pip --version");
Console.WriteLine($"Pip: {pipResult.StandardOutput}");

// List installed packages
var packagesResult = await runtime.ExecuteCommandAsync("-m pip list");
Console.WriteLine($"Installed packages:\n{packagesResult.StandardOutput}");
```

### Check Metadata

```csharp
// List all instances
var instances = manager.ListInstances();
foreach (var instance in instances)
{
    Console.WriteLine($"Version: {instance.PythonVersion}");
    Console.WriteLine($"Build Date: {instance.BuildDate:yyyy-MM-dd}");
    Console.WriteLine($"Directory: {instance.Directory}");
    Console.WriteLine($"Installed: {instance.InstallationDate}");
}
```

## Getting Help

If you encounter issues not covered here:

1. Check the [Error Handling](Error-Handling.md) documentation
2. Review the [Examples](Examples.md) for usage patterns
3. Check GitHub issues for similar problems
4. Create a new issue with:
   - PythonEmbedded.Net version
   - .NET version
   - Platform (Windows/Linux/macOS)
   - Steps to reproduce
   - Error messages and logs

## See Also

- [Error Handling](Error-Handling.md) - Exception reference
- [Examples](Examples.md) - Usage examples
- [API Reference](API-Reference.md) - Complete API documentation
- [Architecture](Architecture.md) - Understanding the design

