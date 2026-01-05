# ProcessExecutor

Default implementation of [IProcessExecutor](./IProcessExecutor.md) for executing external processes.

## Namespace

`PythonEmbedded.Net.Services`

## Inheritance

```
IProcessExecutor
└── ProcessExecutor
```

## Constructors

### ProcessExecutor

```csharp
public ProcessExecutor(ILogger<ProcessExecutor>? logger = null)
```

Initializes a new instance of the ProcessExecutor class.

**Parameters:**
- `logger` - Optional logger for this executor

## Methods

### ExecuteAsync

```csharp
public async Task<ProcessExecutionResult> ExecuteAsync(
    ProcessStartInfo startInfo,
    Func<string?>? stdinHandler = null,
    Action<string>? stdoutHandler = null,
    Action<string>? stderrHandler = null,
    CancellationToken cancellationToken = default)
```

Executes a process with the specified arguments and handles stdin/stdout/stderr. Implements [IProcessExecutor.ExecuteAsync](./IProcessExecutor.md#executeasync).

## Usage

ProcessExecutor is used internally by [BasePythonRuntime](../Runtimes/BasePythonRuntime.md) for executing Python processes. It can also be used directly for custom process execution.

**Example:**

```csharp
var executor = new ProcessExecutor(logger);

var startInfo = new ProcessStartInfo
{
    FileName = "python",
    Arguments = "-c \"print('Hello')\"",
    RedirectStandardOutput = true,
    RedirectStandardError = true,
    UseShellExecute = false
};

var result = await executor.ExecuteAsync(startInfo);
Console.WriteLine(result.StandardOutput);
```

## Related Types

- [IProcessExecutor](./IProcessExecutor.md) - Interface
- [ProcessExecutionResult](../Records/ProcessExecutionResult.md) - Execution result record





