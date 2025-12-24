# PythonEmbedded.Net API Reference

Complete API documentation for the PythonEmbedded.Net library. This documentation covers all public classes, interfaces, models, and exceptions.

## Table of Contents

### [Managers](./Managers.md)
Classes responsible for managing Python installations and instances.

- [BasePythonManager](./Managers/BasePythonManager.md) - Abstract base class for Python managers
- [PythonManager](./Managers/PythonManager.md) - Subprocess-based Python manager
- [PythonNetManager](./Managers/PythonNetManager.md) - Python.NET-based Python manager

### [Runtimes](./Runtimes.md)
Classes for executing Python code and managing Python environments.

- [BasePythonRuntime](./Runtimes/BasePythonRuntime.md) - Abstract base class for Python runtimes
- [BasePythonRootRuntime](./Runtimes/BasePythonRootRuntime.md) - Abstract base class for root Python runtimes
- [BasePythonVirtualRuntime](./Runtimes/BasePythonVirtualRuntime.md) - Abstract base class for virtual environment runtimes
- [PythonRootRuntime](./Runtimes/PythonRootRuntime.md) - Subprocess-based root Python runtime
- [PythonNetRootRuntime](./Runtimes/PythonNetRootRuntime.md) - Python.NET-based root Python runtime
- [PythonRootVirtualEnvironment](./Runtimes/PythonRootVirtualEnvironment.md) - Subprocess-based virtual environment
- [PythonNetVirtualEnvironment](./Runtimes/PythonNetVirtualEnvironment.md) - Python.NET-based virtual environment

### [Models](./Models.md)
Data models and configuration classes.

- [InstanceMetadata](./Models/InstanceMetadata.md) - Metadata for Python instances
- [ManagerConfiguration](./Models/ManagerConfiguration.md) - Configuration for managers
- [ManagerMetadata](./Models/ManagerMetadata.md) - Metadata for managers
- [PlatformInfo](./Models/PlatformInfo.md) - Platform information
- [PackageInfo](./Models/PackageInfo.md) - Package information records
- [PipConfiguration](./Models/PipConfiguration.md) - Pip configuration record
- [PyPIPackageInfo](./Models/PyPIPackageInfo.md) - PyPI package information

### [Exceptions](./Exceptions.md)
Exception classes for error handling.

- [PythonExecutionException](./Exceptions/PythonExecutionException.md) - Base exception for Python execution errors
- [PythonInstallationException](./Exceptions/PythonInstallationException.md) - Base exception for installation errors
- [PythonNotInstalledException](./Exceptions/PythonNotInstalledException.md) - Python not installed error
- [PythonNetInitializationException](./Exceptions/PythonNetInitializationException.md) - Python.NET initialization error
- [PythonNetExecutionException](./Exceptions/PythonNetExecutionException.md) - Python.NET execution error
- [PackageInstallationException](./Exceptions/PackageInstallationException.md) - Package installation error
- [InstanceNotFoundException](./Exceptions/InstanceNotFoundException.md) - Instance not found error
- [VirtualEnvironmentNotFoundException](./Exceptions/VirtualEnvironmentNotFoundException.md) - Virtual environment not found error
- [MetadataCorruptedException](./Exceptions/MetadataCorruptedException.md) - Metadata corruption error
- [InvalidPythonVersionException](./Exceptions/InvalidPythonVersionException.md) - Invalid Python version error
- [PlatformNotSupportedException](./Exceptions/PlatformNotSupportedException.md) - Platform not supported error
- [RequirementsFileException](./Exceptions/RequirementsFileException.md) - Requirements file error
- [InvalidPackageSpecificationException](./Exceptions/InvalidPackageSpecificationException.md) - Invalid package specification error

### [Services](./Services.md)
Service interfaces and implementations.

- [IProcessExecutor](./Services/IProcessExecutor.md) - Interface for process execution
- [ProcessExecutor](./Services/ProcessExecutor.md) - Process execution implementation

### [Records](./Records.md)
Record types for data transfer.

- [PythonExecutionResult](./Records/PythonExecutionResult.md) - Result of Python code execution
- [ProcessExecutionResult](./Records/ProcessExecutionResult.md) - Result of process execution

## Namespace

All classes are in the `PythonEmbedded.Net` namespace, with models in `PythonEmbedded.Net.Models` and exceptions in `PythonEmbedded.Net.Exceptions`.

## Quick Links

- [Getting Started](../Getting-Started.md) - Installation and basic usage
- [Examples](../Examples.md) - Code examples
- [Architecture](../Architecture.md) - System architecture overview
- [Error Handling](../Error-Handling.md) - Error handling guide


