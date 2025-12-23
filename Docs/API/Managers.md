# Managers

Managers are responsible for downloading, installing, and managing Python instances. They provide the entry point for working with PythonEmbedded.Net.

## Classes

- [BasePythonManager](./Managers/BasePythonManager.md) - Abstract base class for all Python managers
- [PythonManager](./Managers/PythonManager.md) - Subprocess-based Python manager
- [PythonNetManager](./Managers/PythonNetManager.md) - Python.NET-based Python manager

## Overview

Managers handle:
- Downloading Python distributions from GitHub releases
- Installing and extracting Python instances
- Managing multiple Python versions
- Listing available instances and versions
- Validating instance integrity
- Network connectivity testing

## Choosing a Manager

- **PythonManager**: Use for standard subprocess-based Python execution. Best for most use cases.
- **PythonNetManager**: Use for in-process Python execution via Python.NET. Best for high-performance scenarios where you need direct .NET-Python interop.

See [Getting Started](../Getting-Started.md) for examples of using managers.

