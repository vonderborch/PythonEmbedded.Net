# Architecture

This document describes the architecture and design of PythonEmbedded.Net.

## Overview

PythonEmbedded.Net is designed with modern C# best practices, emphasizing:
- **Interface-based design** for testability
- **Separation of concerns** with clear responsibilities
- **Dependency injection** support
- **Resource management** with IDisposable
- **Asynchronous-first** API design

## Class Hierarchy

### Manager Layer

```
IPythonManager (interface)
├── BasePythonManager (abstract)
    ├── PythonManager (subprocess execution)
    └── PythonNetManager (Python.NET execution)
```

**Responsibilities:**
- Instance lifecycle management (create, delete, list)
- GitHub API integration for downloading Python distributions
- Metadata management
- Directory structure organization

### Runtime Layer

```
IPythonRuntime (interface)
├── BasePythonRuntime (abstract)
    ├── BasePythonRootRuntime (abstract) - manages virtual environments
    │   ├── PythonRootRuntime (subprocess)
    │   └── PythonNetRootRuntime (Python.NET)
    └── BasePythonVirtualRuntime (abstract) - represents a virtual environment
        ├── PythonRootVirtualEnvironment (subprocess)
        └── PythonNetVirtualEnvironment (Python.NET)
```

**Responsibilities:**
- Python code execution
- Package installation
- Virtual environment management (root runtimes only)

### Service Layer

```
IProcessExecutor (interface)
└── ProcessExecutor (implementation)
```

**Responsibilities:**
- Process execution abstraction
- Stream handling (stdin, stdout, stderr)
- Cancellation support

## Design Patterns

### Factory Pattern

Managers act as factories for runtime instances:

```csharp
var runtime = await manager.GetOrCreateInstanceAsync("3.12.0");
```

The manager creates the appropriate runtime type (`PythonRootRuntime` or `PythonNetRootRuntime`) based on the manager type.

### Strategy Pattern

Two execution strategies:
- **Subprocess execution**: Uses `ProcessExecutor` to launch Python processes
- **Python.NET execution**: Uses Python.NET for in-process execution

Both strategies implement the same `IPythonRuntime` interface, allowing transparent switching.

### Template Method Pattern

Base classes define the algorithm structure while allowing derived classes to customize specific steps:

```csharp
// BasePythonRuntime defines the ExecuteCommandAsync algorithm
// Derived classes customize PythonExecutablePath and ValidateInstallation
```

## Key Components

### BasePythonManager

Central manager that handles:
- **Instance Discovery**: Finds existing instances from metadata
- **Download Coordination**: Uses `GitHubReleaseHelper` to find and download distributions from [python-build-standalone](https://github.com/astral-sh/python-build-standalone)
- **Extraction Management**: Uses `ArchiveHelper` to extract downloaded archives
- **Metadata Tracking**: Maintains in-memory `ManagerMetadata` collection loaded from individual instance metadata files

> **Note**: This library utilizes [python-build-standalone](https://github.com/astral-sh/python-build-standalone) by [astral-sh](https://github.com/astral-sh) for providing redistributable Python distributions. We are not associated with astral-sh, but we thank them for their fantastic work.

### BasePythonRuntime

Provides common functionality for all runtimes:
- **Command Execution**: `ExecuteCommandAsync` / `ExecuteCommand`
- **Script Execution**: `ExecuteScriptAsync` / `ExecuteScript`
- **Package Installation**: `InstallPackageAsync`, `InstallRequirementsAsync`, `InstallPyProjectAsync`

Delegates actual process execution to `IProcessExecutor` service.

### BasePythonRootRuntime

Extends `BasePythonRuntime` with virtual environment management:
- **Virtual Environment Creation**: Uses Python's `venv` module
- **Virtual Environment Discovery**: Lists and validates virtual environments
- **Virtual Environment Runtime Creation**: Factory method for creating `IPythonVirtualRuntime` instances

### Process Executor

Extracted service for process execution:
- **Abstraction**: Allows mocking for testing
- **Reusability**: Can be used independently
- **Testability**: Interface enables test doubles

## Data Flow

### Instance Creation Flow

```
GetOrCreateInstanceAsync()
    ├── Check metadata for existing instance
    ├── If not found:
    │   ├── Find release asset (GitHubReleaseHelper)
    │   ├── Download asset (GitHubReleaseHelper)
    │   ├── Extract archive (ArchiveHelper)
    │   ├── Verify installation (ArchiveHelper)
    │   ├── Find Python install path
    │   └── Save metadata
    └── Create runtime instance (factory method)
```

### Command Execution Flow

```
ExecuteCommandAsync()
    ├── Validate installation
    ├── Build ProcessStartInfo
    ├── ProcessExecutor.ExecuteAsync()
    │   ├── Start process
    │   ├── Handle stdin (if provided)
    │   ├── Capture stdout/stderr
    │   └── Wait for completion
    └── Return PythonExecutionResult
```

### Virtual Environment Creation Flow

```
GetOrCreateVirtualEnvironmentAsync()
    ├── Validate installation
    ├── Check if venv exists
    ├── If not exists or recreate:
    │   ├── Execute: python -m venv <path>
    │   └── Verify creation
    └── Create IPythonVirtualRuntime instance
```

## Directory Structure

```
manager_directory/
└── python-{version}-{buildDate}/   # Instance directory
    ├── python/                      # Python installation files
    ├── venvs/                       # Virtual environments
    │   └── {venv_name}/
    │       ├── bin/ (or Scripts/)
    │       ├── lib/
    │       └── pyvenv.cfg
    └── instance_metadata.json       # Instance-specific metadata
```

**Note**: The `ManagerMetadata` class is an in-memory collection that loads instance metadata from individual `instance_metadata.json` files in each instance directory. There is no central metadata file.

## Resource Management

### Python.NET Runtimes

Python.NET runtimes implement `IDisposable`:

```csharp
if (runtime is IDisposable disposable)
{
    disposable.Dispose();
}
```

**Note:** Python.NET uses a singleton `PythonEngine`, so disposal tracks instance counts but doesn't necessarily shut down Python.NET (to avoid affecting other instances).

### Process Resources

Process execution uses `using` statements and async disposal patterns to ensure proper cleanup.

## Error Handling Strategy

### Exception Hierarchy

Exceptions are organized hierarchically:
- **Base exceptions** for broad categories
- **Specific exceptions** with additional context properties
- **Custom properties** for debugging information

### Validation Strategy

- **Early validation**: Validate inputs at API boundaries
- **Installation validation**: Verify Python installation before operations
- **Graceful degradation**: Provide meaningful error messages

## Logging Strategy

### Structured Logging

All logging uses `Microsoft.Extensions.Logging` with structured logging:
- Event IDs for key operations
- Log levels: Trace, Debug, Information, Warning, Error, Critical
- Contextual information in log messages

### Log Levels

- **Trace**: Detailed diagnostic information
- **Debug**: Diagnostic information for debugging
- **Information**: General informational messages
- **Warning**: Warning messages (e.g., non-zero exit codes)
- **Error**: Error messages with exceptions
- **Critical**: Critical failures

## Testing Strategy

### Interfaces Enable Testing

All major components implement interfaces, enabling:
- **Mocking**: Use mocking frameworks to create test doubles
- **Isolation**: Test components in isolation
- **Faster tests**: Avoid real Python installations in unit tests

### Test Structure

- **Unit Tests**: Test individual components with mocks
- **Integration Tests**: Test with real Python installations (marked with `[Ignore]` for CI)

## Extension Points

### Custom Process Executor

Implement `IProcessExecutor` to customize process execution:

```csharp
public class CustomProcessExecutor : IProcessExecutor
{
    public Task<ProcessExecutionResult> ExecuteAsync(...)
    {
        // Custom implementation
    }
}
```

### Custom Runtime Types

Extend base classes to add custom behavior:

```csharp
public class CustomRuntime : BasePythonRootRuntime
{
    // Override methods as needed
}
```

## Performance Considerations

### Async/Await

- All I/O operations are asynchronous
- `ConfigureAwait(false)` used throughout to avoid deadlocks
- Synchronous wrappers available for compatibility

### Caching

- Instance metadata cached in memory
- Python installations persist on disk
- No redundant downloads

### Resource Pooling

- Process executor can be shared across operations
- Python.NET initialization is singleton-based

## Security Considerations

### Process Execution

- No shell execution (`UseShellExecute = false`)
- Explicit argument lists (prevents injection)
- Working directory isolation

### File System

- Validated paths
- No arbitrary file access
- Controlled directory structure

## Future Enhancements

Potential areas for extension:
- **Caching**: Add caching layer for GitHub API responses
- **Parallel Execution**: Support for parallel package installations
- **Custom Distributions**: Support for custom Python distributions
- **Cross-Platform Tools**: Better handling of platform-specific tools (tar, zstd)

## See Also

- [Getting Started](Getting-Started.md)
- [API Reference](API-Reference.md)
- [Examples](Examples.md)

