# IProcessExecutor

Interface for executing external processes. Provides abstraction for process execution with stdin/stdout/stderr handling.

## Namespace

`PythonEmbedded.Net.Services`

## Methods

### ExecuteAsync

```csharp
Task<ProcessExecutionResult> ExecuteAsync(
    ProcessStartInfo startInfo,
    Func<string?>? stdinHandler = null,
    Action<string>? stdoutHandler = null,
    Action<string>? stderrHandler = null,
    CancellationToken cancellationToken = default)
```

Executes a process with the specified arguments and handles stdin/stdout/stderr.

**Parameters:**
- `startInfo` - Process start information (file name, arguments, etc.)
- `stdinHandler` - Optional handler for providing stdin input line by line. Return null to end input.
- `stdoutHandler` - Optional handler for processing stdout output line by line.
- `stderrHandler` - Optional handler for processing stderr output line by line.
- `cancellationToken` - Cancellation token

**Returns:**
- `Task<ProcessExecutionResult>` - The execution result. See [ProcessExecutionResult](../Records/ProcessExecutionResult.md)

## Implementations

- [ProcessExecutor](./ProcessExecutor.md) - Default implementation

## Related Types

- [ProcessExecutor](./ProcessExecutor.md) - Default implementation
- [ProcessExecutionResult](../Records/ProcessExecutionResult.md) - Execution result record




