# ProcessExecutionResult

Represents the result of executing a process.

## Namespace

`PythonEmbedded.Net.Services`

## Definition

```csharp
public record ProcessExecutionResult(
    int ExitCode,
    string StandardOutput = "",
    string StandardError = "")
```

## Properties

### ExitCode

```csharp
public int ExitCode { get; init; }
```

Gets the exit code of the process execution. 0 typically indicates success.

### StandardOutput

```csharp
public string StandardOutput { get; init; }
```

Gets the standard output from the process execution.

### StandardError

```csharp
public string StandardError { get; init; }
```

Gets the standard error output from the process execution.

## Usage

ProcessExecutionResult is returned by [IProcessExecutor](../Services/IProcessExecutor.md) implementations.

**Example:**

```csharp
var result = await processExecutor.ExecuteAsync(startInfo);

if (result.ExitCode == 0)
{
    Console.WriteLine(result.StandardOutput);
}
else
{
    Console.Error.WriteLine(result.StandardError);
}
```

## Related Types

- [IProcessExecutor](../Services/IProcessExecutor.md) - Returns ProcessExecutionResult
- [ProcessExecutor](../Services/ProcessExecutor.md) - Implementation that returns this type

