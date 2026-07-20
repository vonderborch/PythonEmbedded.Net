# Error Handling

PythonEmbedded.Net 2.x deliberately has **two exception types**. This document covers both, plus how cancellation and nonzero exit codes surface.

## Exception Hierarchy

```
Exception
└── PythonException            // every library failure, tagged with a Kind
      └── PythonProcessException   // Python code that failed (nonzero exit / timeout)
```

`OperationCanceledException` is never wrapped — a cancelled `CancellationToken` always surfaces as itself.

## PythonException

The single exception type for anything that goes wrong outside of running Python code: resolving a version, downloading an interpreter, creating an environment, managing packages, locking, or tool provisioning.

**Properties:**
- `Kind` (`PythonErrorKind`): what category of failure this is
- `Message`: human-readable detail

**`PythonErrorKind` values:**

| Kind | When it's thrown |
| --- | --- |
| `VersionNotFound` | No source could satisfy the requested version |
| `UnsupportedPlatform` | The current OS/architecture has no matching build |
| `DownloadFailed` | A download failed or its checksum didn't match |
| `InstallFailed` | Extraction or install-tree setup failed |
| `EnvironmentFailed` | Virtual environment creation failed, or a fetch tried to reopen an existing environment with a different installer than the one it was created with |
| `PackageOperationFailed` | Install/uninstall/list failed |
| `ToolMissing` | A required external tool (uv, poetry, micromamba) couldn't be resolved or provisioned |
| `Locked` | A cross-process lock could not be acquired within `LockTimeout` |
| `Offline` | An operation needed the network but `Offline = true` |
| `ExecutionFailed` | The runner failed to start or communicate with the Python process |
| `Timeout` | A run exceeded `RunOptions.Timeout` and was killed |

**Example:**

```csharp
try
{
    var env = await PythonEnvironment.GetEnvironmentAsync("99.99.99", "myapp");
}
catch (PythonException ex) when (ex.Kind == PythonErrorKind.VersionNotFound)
{
    Console.WriteLine("No matching Python build was found.");
}
```

## PythonProcessException

Thrown by `RunAsync`/`RunCodeAsync`/`RunModuleAsync` (and their sync twins) when the process exits nonzero or times out, **unless** `RunOptions.ThrowOnError = false`.

**Properties:**
- `Result` (`PythonResult`): the full result — `ExitCode`, `StandardOutput`, `StandardError`, `Duration`
- `Kind`: `PythonErrorKind.Timeout` on timeout, otherwise `ExecutionFailed`
- Inherits `Message` from `PythonException`

**Example:**

```csharp
try
{
    await env.RunAsync("flaky.py");
}
catch (PythonProcessException ex)
{
    Console.WriteLine($"Exit {ex.Result.ExitCode}");
    Console.WriteLine(ex.Result.StandardError);
}
```

## Opting Out of Exceptions

Pass `ThrowOnError = false` to get a `PythonResult` back regardless of outcome:

```csharp
var result = await env.RunAsync("might-fail.py", options: new RunOptions { ThrowOnError = false });
if (!result.Success)
{
    Console.WriteLine($"Failed with exit code {result.ExitCode}: {result.StandardError}");
}
```

`result.EnsureSuccess()` lets you defer the throw:

```csharp
var result = await env.RunAsync("job.py", options: new RunOptions { ThrowOnError = false });
// ... inspect result, log, whatever ...
result.EnsureSuccess();   // throws PythonProcessException now, if it failed
```

## Cancellation

Every async method accepts a `CancellationToken`. A cancelled token surfaces as `OperationCanceledException`, not `PythonException` — catch it separately:

```csharp
using var cts = new CancellationTokenSource(TimeSpan.FromMinutes(5));
try
{
    await env.RunAsync("long-job.py", ct: cts.Token);
}
catch (OperationCanceledException)
{
    Console.WriteLine("Cancelled.");
}
```

For a timeout enforced by the runner itself (which kills the process and throws `PythonProcessException(Kind = Timeout)` rather than just cancelling the await), use `RunOptions.Timeout` instead.

## Best Practices

### Catch by Kind, not by inheritance depth

There are only two types, so branch on `Kind`:

```csharp
try
{
    var env = await PythonEnvironment.GetEnvironmentAsync("3.13", "myapp");
}
catch (PythonException ex)
{
    switch (ex.Kind)
    {
        case PythonErrorKind.Offline:
        case PythonErrorKind.DownloadFailed:
            // retry later, or fall back to a bundled runtime package
            break;
        case PythonErrorKind.Locked:
            // another process is mid-install; retry with backoff
            break;
        default:
            throw;
    }
}
```

### Always check `Success` when `ThrowOnError = false`

```csharp
var result = await env.RunAsync("script.py", options: new RunOptions { ThrowOnError = false });
if (!result.Success)
{
    _logger.LogWarning("script.py exited {ExitCode}: {Error}", result.ExitCode, result.StandardError);
}
```

### Log the Kind alongside the message

```csharp
catch (PythonException ex)
{
    _logger.LogError(ex, "Python operation failed: {Kind}", ex.Kind);
    throw;
}
```

## See Also

- [Quick Reference](Quick-Reference.md)
- [Examples](Examples.md)
- [Troubleshooting](Troubleshooting.md)
