# PythonExecutionException

Exception thrown when Python execution fails.

## Namespace

`PythonEmbedded.Net.Exceptions`

## Inheritance

```
Exception
└── PythonExecutionException
    └── PythonNetExecutionException
```

## Properties

### ExitCode

```csharp
public int? ExitCode { get; set; }
```

Gets or sets the exit code from the Python process, if available.

### StandardError

```csharp
public string? StandardError { get; set; }
```

Gets or sets the standard error output from the Python process, if available.

## Constructors

### PythonExecutionException()

```csharp
public PythonExecutionException()
```

Initializes a new instance with default message.

### PythonExecutionException(string)

```csharp
public PythonExecutionException(string message)
```

Initializes a new instance with a specified error message.

### PythonExecutionException(string, Exception)

```csharp
public PythonExecutionException(string message, Exception innerException)
```

Initializes a new instance with a specified error message and inner exception.

## Related Types

- [PythonNetExecutionException](./PythonNetExecutionException.md) - Derived exception for Python.NET execution errors





