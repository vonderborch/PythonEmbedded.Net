# PythonInstallationException

Base exception for installation-related errors in PythonEmbedded.Net.

## Namespace

`PythonEmbedded.Net.Exceptions`

## Inheritance

```
Exception
└── PythonInstallationException
    ├── PythonNotInstalledException
    ├── InstanceNotFoundException
    ├── InvalidPythonVersionException
    ├── PlatformNotSupportedException
    ├── MetadataCorruptedException
    └── PackageInstallationException
        ├── RequirementsFileException
        └── InvalidPackageSpecificationException
```

## Constructors

### PythonInstallationException()

```csharp
public PythonInstallationException()
```

Initializes a new instance with default message.

### PythonInstallationException(string)

```csharp
public PythonInstallationException(string message)
```

Initializes a new instance with a specified error message.

### PythonInstallationException(string, Exception)

```csharp
public PythonInstallationException(string message, Exception innerException)
```

Initializes a new instance with a specified error message and inner exception.

## Derived Exceptions

- [PythonNotInstalledException](./PythonNotInstalledException.md)
- [InstanceNotFoundException](./InstanceNotFoundException.md)
- [InvalidPythonVersionException](./InvalidPythonVersionException.md)
- [PlatformNotSupportedException](./PlatformNotSupportedException.md)
- [MetadataCorruptedException](./MetadataCorruptedException.md)
- [PackageInstallationException](./PackageInstallationException.md)



