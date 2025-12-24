# Managers

Managers are responsible for downloading, installing, and managing Python instances. They provide the entry point for working with PythonEmbedded.Net.

## Classes

- [BasePythonManager](./Managers/BasePythonManager.md) - Abstract base class for all Python managers
- [PythonManager](./Managers/PythonManager.md) - Subprocess-based Python manager
- [PythonNetManager](./Managers/PythonNetManager.md) - Python.NET-based Python manager

## Overview

Managers handle:
- Downloading Python distributions from GitHub releases
- Installing and extracting Python instances (supports multiple archive formats: zip, tar.gz, tar.bz2, tar.bz, tar.zst)
- Managing multiple Python versions with smart version matching:
  - **Exact versions** (e.g., "3.12.5") match exactly
  - **Partial versions** (e.g., "3.12") automatically find the latest patch version (e.g., "3.12.19")
- Listing available instances and versions
- Validating instance integrity
- Network connectivity testing

## Choosing a Manager

- **PythonManager**: Use for standard subprocess-based Python execution. Best for most use cases.
- **PythonNetManager**: Use for in-process Python execution via Python.NET. Best for high-performance scenarios where you need direct .NET-Python interop.

See [Getting Started](../Getting-Started.md) for examples of using managers.


