# PythonExecutionResult

Represents the result of executing a Python command or script.

## Namespace

`PythonEmbedded.Net`

## Definition

```csharp
public record PythonExecutionResult(
    int ExitCode,
    string StandardOutput = "",
    string StandardError = "")
```

## Properties

### ExitCode

```csharp
public int ExitCode { get; init; }
```

Gets the exit code of the Python execution. 0 typically indicates success.

### StandardOutput

```csharp
public string StandardOutput { get; init; }
```

Gets the standard output from the Python execution.

### StandardError

```csharp
public string StandardError { get; init; }
```

Gets the standard error output from the Python execution.

## Usage

PythonExecutionResult is returned by execution methods in [BasePythonRuntime](../Runtimes/BasePythonRuntime.md).

**Example:**

```csharp
var result = await runtime.ExecuteCommandAsync("-c \"print('Hello')\"");

if (result.ExitCode == 0)
{
    Console.WriteLine(result.StandardOutput); // "Hello"
}
else
{
    Console.Error.WriteLine(result.StandardError);
}
```

## Related Types

- [BasePythonRuntime](../Runtimes/BasePythonRuntime.md) - Returns PythonExecutionResult



