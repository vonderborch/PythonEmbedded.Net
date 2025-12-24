# Runtimes

Runtimes provide the interface for executing Python code, managing packages, and working with virtual environments.

## Classes

### Base Classes
- [BasePythonRuntime](./Runtimes/BasePythonRuntime.md) - Abstract base class for all Python runtimes
- [BasePythonRootRuntime](./Runtimes/BasePythonRootRuntime.md) - Abstract base class for root Python runtimes (can manage virtual environments)
- [BasePythonVirtualRuntime](./Runtimes/BasePythonVirtualRuntime.md) - Abstract base class for virtual environment runtimes

### Root Runtime Implementations
- [PythonRootRuntime](./Runtimes/PythonRootRuntime.md) - Subprocess-based root Python runtime
- [PythonNetRootRuntime](./Runtimes/PythonNetRootRuntime.md) - Python.NET-based root Python runtime

### Virtual Environment Implementations
- [PythonRootVirtualEnvironment](./Runtimes/PythonRootVirtualEnvironment.md) - Subprocess-based virtual environment
- [PythonNetVirtualEnvironment](./Runtimes/PythonNetVirtualEnvironment.md) - Python.NET-based virtual environment

## Overview

Runtimes provide:
- Python code execution (commands, scripts, files)
- Package management (install, uninstall, list, search)
- Virtual environment management (create, delete, clone, export, import)
- Health checks and validation
- Version information

## Choosing a Runtime

- **Root Runtimes**: Use for executing Python code in the base Python installation. Can create and manage virtual environments.
- **Virtual Environment Runtimes**: Use for isolated Python environments with their own package installations.

## Execution Modes

- **Subprocess-based** (PythonRootRuntime, PythonRootVirtualEnvironment): Executes Python via subprocess. Best for most use cases.
- **Python.NET-based** (PythonNetRootRuntime, PythonNetVirtualEnvironment): Executes Python in-process. Best for high-performance scenarios with direct .NET-Python interop.

See [Examples](../Examples.md) for usage examples.



