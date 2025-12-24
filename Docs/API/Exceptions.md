# Exceptions

Exception classes for error handling in PythonEmbedded.Net.

## Base Exceptions

- [PythonExecutionException](./Exceptions/PythonExecutionException.md) - Base exception for Python execution errors
- [PythonInstallationException](./Exceptions/PythonInstallationException.md) - Base exception for installation errors

## Derived Exceptions

### Execution Exceptions
- [PythonNetInitializationException](./Exceptions/PythonNetInitializationException.md) - Python.NET initialization error
- [PythonNetExecutionException](./Exceptions/PythonNetExecutionException.md) - Python.NET execution error

### Installation Exceptions
- [PythonNotInstalledException](./Exceptions/PythonNotInstalledException.md) - Python not installed error
- [InstanceNotFoundException](./Exceptions/InstanceNotFoundException.md) - Instance not found error
- [InvalidPythonVersionException](./Exceptions/InvalidPythonVersionException.md) - Invalid Python version error
- [PlatformNotSupportedException](./Exceptions/PlatformNotSupportedException.md) - Platform not supported error
- [MetadataCorruptedException](./Exceptions/MetadataCorruptedException.md) - Metadata corruption error

### Package Exceptions
- [PackageInstallationException](./Exceptions/PackageInstallationException.md) - Package installation error
- [RequirementsFileException](./Exceptions/RequirementsFileException.md) - Requirements file error
- [InvalidPackageSpecificationException](./Exceptions/InvalidPackageSpecificationException.md) - Invalid package specification error

### Virtual Environment Exceptions
- [VirtualEnvironmentNotFoundException](./Exceptions/VirtualEnvironmentNotFoundException.md) - Virtual environment not found error

## Exception Hierarchy

```
Exception
├── PythonExecutionException
│   └── PythonNetExecutionException
├── PythonInstallationException
│   ├── PythonNotInstalledException
│   ├── InstanceNotFoundException
│   ├── InvalidPythonVersionException
│   ├── PlatformNotSupportedException
│   ├── MetadataCorruptedException
│   └── PackageInstallationException
│       ├── RequirementsFileException
│       └── InvalidPackageSpecificationException
└── VirtualEnvironmentNotFoundException
└── PythonNetInitializationException
```

## Usage

All exceptions provide detailed error messages and context information. See individual exception pages for specific properties and usage.



