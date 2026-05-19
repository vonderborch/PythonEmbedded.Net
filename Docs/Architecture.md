# Architecture

This document describes the architecture and design of PythonEmbedded.Net (version **1.4.x**, targeting **net9.0** and **net10.0**).

## Overview

PythonEmbedded.Net is designed with modern C# best practices, emphasizing:
- **Abstract base classes** for extensibility and shared behavior
- **Separation of concerns** with clear responsibilities
- **Dependency injection** support (register concrete managers or base types)
- **Resource management** with `IDisposable` where applicable (Python.NET)
- **Asynchronous-first** API design
- **uv by default** for package and venv operations, with optional **pip/venv** fallback via `useUv: false`

## Class Hierarchy

### Manager Layer

```
BasePythonManager (abstract)
├── PythonManager (subprocess execution)
└── PythonNetManager (Python.NET execution)
```

**Responsibilities:**
- Instance lifecycle management (create, delete, list)
- GitHub API integration for downloading Python distributions
- Metadata management
- Directory structure organization
- Optional `useUv` on `GetOrCreateInstanceAsync` (default `true`) to install/detect uv on new instances

### Runtime Layer

```
BasePythonRuntime (abstract)
├── BasePythonRootRuntime (abstract) — manages virtual environments
│   ├── PythonRootRuntime (subprocess)
│   └── PythonNetRootRuntime (Python.NET, IDisposable)
└── BasePythonVirtualRuntime (abstract) — represents a virtual environment
    ├── PythonRootVirtualEnvironment (subprocess)
    └── PythonNetVirtualEnvironment (Python.NET, IDisposable)
```

There are **no** `IPythonRuntime`, `IPythonRootRuntime`, or `IPythonVirtualRuntime` interfaces. Use the abstract base classes above (or concrete types such as `PythonRootRuntime`) for typing and extension.

**Responsibilities:**
- Python code execution
- Package installation (`useUv: true` → uv; `useUv: false` → `python -m pip`)
- Virtual environment management (root runtimes only; `useUv: true` → `uv venv`, `useUv: false` → `python -m venv`)

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
// runtime is BasePythonRuntime; subprocess managers return BasePythonRootRuntime
```

The manager creates the appropriate runtime type (`PythonRootRuntime` or `PythonNetRootRuntime`) based on the manager type.

### Strategy Pattern

Two execution strategies:
- **Subprocess execution**: Uses `ProcessExecutor` to launch Python processes
- **Python.NET execution**: Uses Python.NET for in-process execution

Both strategies extend the same `BasePythonRuntime` hierarchy, allowing similar APIs with different execution backends.

### Template Method Pattern

Base classes define the algorithm structure while allowing derived classes to customize specific steps:

```csharp
// BasePythonRuntime defines ExecuteCommandAsync / package manager flow
// Derived classes customize PythonExecutablePath and ValidateInstallation
```

## Key Components

### BasePythonManager

Central manager that handles:
- **Instance Discovery**: Finds existing instances from metadata
- **Download Coordination**: Uses `GitHubReleaseHelper` to find and download distributions from [python-build-standalone](https://github.com/astral-sh/python-build-standalone)
- **Extraction Management**: Uses `ArchiveHelper` to extract downloaded archives
- **Metadata Tracking**: Maintains in-memory `ManagerMetadata` collection loaded from individual instance metadata files
- **uv bootstrap**: When `useUv` is true (default), calls `EnsureUvInstalledAsync()` on the runtime after instance creation

> **Note**: This library utilizes [python-build-standalone](https://github.com/astral-sh/python-build-standalone) by [astral-sh](https://github.com/astral-sh) for providing redistributable Python distributions. We are not associated with astral-sh, but we thank them for their fantastic work.

### BasePythonRuntime

Provides common functionality for all runtimes:
- **Command Execution**: `ExecuteCommandAsync` / `ExecuteCommand`
- **Script Execution**: `ExecuteScriptAsync` / `ExecuteScript`
- **Package Management**: `InstallPackageAsync`, `InstallRequirementsAsync`, `InstallPyProjectAsync`, etc., with `useUv` (default `true`)
- **uv detection**: `UvPath`, `IsUvAvailable`, `DetectUvAsync`, `EnsureUvInstalledAsync`
- **pip fallback**: When `useUv: false`, uses `python -m pip` via `EnsurePipAvailableAsync`

Delegates subprocess execution to `IProcessExecutor`.

### BasePythonRootRuntime

Extends `BasePythonRuntime` with virtual environment management:
- **Virtual Environment Creation**: `uv venv` when `useUv: true` (default), or `python -m venv` when `useUv: false`
- **External Virtual Environments**: Supports creating venvs at arbitrary paths via `externalPath`
- **Virtual Environment Discovery**: Lists and validates virtual environments, including external paths
- **Virtual Environment Runtime Creation**: Returns `BasePythonVirtualRuntime` instances
- **Metadata Management**: Tracks virtual environments in `InstanceMetadata.VirtualEnvironments`

### BasePythonVirtualRuntime

Extends `BasePythonRuntime` for a single venv:
- Resolves the venv’s `python` executable under `bin/` or `Scripts/`
- **uv sharing**: Venvs created with `uv venv` typically do not ship a local `uv` binary. The runtime reads `pyvenv.cfg` (`home = ...`) and resolves uv from the **base** interpreter that created the venv, then runs `uv pip ... --python <venv-python>` for package operations

### Process Executor

Extracted service for process execution:
- **Abstraction**: Allows mocking for testing
- **Reusability**: Can be used independently
- **Testability**: `IProcessExecutor` enables test doubles

## Data Flow

### Instance Creation Flow

```
GetOrCreateInstanceAsync(version, buildDate, useUv: true)
    ├── Check metadata for existing instance
    ├── If not found:
    │   ├── Find release asset (GitHubReleaseHelper)
    │   ├── Download asset (GitHubReleaseHelper)
    │   ├── Extract archive (ArchiveHelper)
    │   ├── Verify installation (ArchiveHelper)
    │   ├── Find Python install path
    │   └── Save metadata
    ├── Create runtime instance (GetPythonRuntimeForInstance)
    └── If useUv: EnsureUvInstalledAsync()
```

### Command Execution Flow

```
ExecuteCommandAsync()
    ├── ValidateInstallation()
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
GetOrCreateVirtualEnvironmentAsync(name, recreateIfExists, externalPath, useUv)
    ├── Validate installation
    ├── Check metadata / path for existing venv
    ├── If not exists or recreate:
    │   ├── Determine path (external or default)
    │   ├── If useUv: EnsureUvInstalledAsync() then uv venv <path>
    │   └── Else: python -m venv <path>
    │   ├── Add VirtualEnvironmentMetadata
    │   └── Save InstanceMetadata
    ├── Create BasePythonVirtualRuntime instance
    └── If useUv: EnsureUvInstalledAsync() on venv runtime (pyvenv.cfg → base uv)
```

## Directory Structure

```
manager_directory/
└── python-{version}-{buildDate}/   # Instance directory
    ├── python/                      # Python installation files
    ├── venvs/                       # Virtual environments (default location)
    │   └── {venv_name}/
    │       ├── bin/ (or Scripts/)
    │       ├── lib/
    │       └── pyvenv.cfg           # home = base interpreter (used for uv resolution)
    └── instance_metadata.json       # Instance-specific metadata (includes VirtualEnvironments array)
```

**Notes**:
- The `ManagerMetadata` class is an in-memory collection that loads instance metadata from individual `instance_metadata.json` files in each instance directory. There is no central metadata file.
- Virtual environments can also be created at external paths. The venv files live at the external location; `VirtualEnvironmentMetadata` is stored in `instance_metadata.json` with `ExternalPath` set when applicable.
- Example of `instance_metadata.json` with virtual environments:

```json
{
  "PythonVersion": "3.12.0",
  "BuildDate": "2024-01-15T00:00:00Z",
  "VirtualEnvironments": [
    { "Name": "default_venv", "ExternalPath": null, "CreatedDate": "2024-06-01T10:00:00Z" },
    { "Name": "project_venv", "ExternalPath": "/path/to/project/.venv", "CreatedDate": "2024-06-02T14:30:00Z" }
  ]
}
```

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

## Testing Strategy

### Abstractions for Testing

- **`IProcessExecutor`**: Mock process execution in unit tests
- **Concrete managers/runtimes**: Integration tests use real Python installations (often `[Category("Integration")]`)

### Test Structure

- **Unit Tests**: Test helpers and components with mocks where possible
- **Integration Tests**: Test with real Python installations and uv/pip paths

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

## Package Manager (uv vs pip)

**Default:** [uv](https://github.com/astral-sh/uv) for venv creation and package operations.

| Operation | `useUv: true` (default) | `useUv: false` |
|-----------|-------------------------|----------------|
| New instance | `EnsureUvInstalledAsync()` on runtime | No uv install |
| Create venv | `uv venv` | `python -m venv` |
| Install package | `uv pip install ... --python <exe>` | `python -m pip install` |
| List packages | `uv pip list` | `python -m pip list` |

**Virtual environments and uv:** `uv venv` does not copy `uv` into the venv. `BasePythonVirtualRuntime` reads `pyvenv.cfg` and finds uv next to the base interpreter’s `home` path, then passes `--python` pointing at the venv interpreter.

**Configuration:** `ManagerConfiguration.UvPath` sets a custom uv executable path for detection/install flows.

**Opting out globally for an instance:**

```csharp
var runtime = await manager.GetOrCreateInstanceAsync("3.12.0", useUv: false);
var venv = await ((BasePythonRootRuntime)runtime)
    .GetOrCreateVirtualEnvironmentAsync("legacy", useUv: false);
await venv.InstallPackageAsync("requests", useUv: false);
```

## Performance Considerations

### Async/Await

- All I/O operations are asynchronous
- `ConfigureAwait(false)` used throughout to avoid deadlocks
- Synchronous wrappers available for compatibility

### Caching

- Instance metadata cached in memory
- Python installations persist on disk
- Optional `IMemoryCache` for GitHub API responses

### Fast Package Operations (uv)

- Package installation uses uv for much faster I/O than pip alone
- Virtual environment creation uses `uv venv` by default
- Package queries can use `importlib.metadata` for fast local lookups where applicable

## Security Considerations

### Process Execution

- No shell execution (`UseShellExecute = false`)
- Explicit argument lists (prevents injection)
- Working directory isolation

### File System

- Validated paths
- No arbitrary file access
- Controlled directory structure

## See Also

- [Getting Started](Getting-Started.md)
- [API Reference](API-Reference.md)
- [Examples](Examples.md)
- [Troubleshooting](Troubleshooting.md)
