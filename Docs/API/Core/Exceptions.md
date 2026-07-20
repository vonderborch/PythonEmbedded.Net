# Exceptions

Namespace: `PythonEmbedded.Net.Exceptions`. Exactly two exception types by design — see [Error-Handling.md](../../Error-Handling.md) for the narrative version; this page is the member-level reference.

## `PythonErrorKind.cs`

`public enum PythonErrorKind` — categorizes a `PythonException` for programmatic `switch`/`when` handling.

| Value | Thrown when |
| --- | --- |
| `VersionNotFound` | No configured `IPythonSource` could satisfy the requested version (`PythonHost.GetInstallationAsync`). |
| `UnsupportedPlatform` | Current OS/architecture has no matching build (`PlatformTriple.Detect`, or Conda's micromamba asset selection). |
| `DownloadFailed` | A download failed, returned a non-success HTTP status, or its checksum didn't match (`SourceContext.DownloadAsync`/`GetCachedJsonAsync`, `AstralSource`). |
| `InstallFailed` | Archive extraction failed, or the install tree had no recognizable python executable (`ArchiveExtractor`, `PythonHost.CommitInstallation`). |
| `EnvironmentFailed` | Venv/env creation failed, no python executable was found in a freshly created environment, or a fetch tried to reopen an environment with a different installer than the one recorded for it (`PythonHost.GetEnvironmentAsync`/`TryLoadEnvironment`). |
| `PackageOperationFailed` | Install/uninstall/list failed (any `IPackageInstaller`). |
| `ToolMissing` | A required external tool (uv, poetry, micromamba) couldn't be resolved or provisioned (`Tools.EnsureAsync`). |
| `Locked` | `DiskLock.AcquireAsync` couldn't acquire within `PythonOptions.LockTimeout`. |
| `Offline` | An operation needed the network but `PythonOptions.Offline = true` and nothing usable was cached (`SourceContext`, `AstralSource`). |
| `ExecutionFailed` | The runner failed to start or communicate with the Python process (`Subprocess.RunAsync`, `PythonProcess.Start`, `PythonNetHost`). |
| `Timeout` | A run exceeded `RunOptions.Timeout` and was killed (`Subprocess.RunAsync`). |

## `PythonException.cs`

`public class PythonException : Exception` — the single exception type for anything that goes wrong outside of running Python code.

- Constructor: `PythonException(PythonErrorKind kind, string message, Exception? innerException = null)`.
- `Kind` (`PythonErrorKind { get; }`) — the category above.

Not sealed — `PythonProcessException` derives from it. `OperationCanceledException` is never wrapped in either type; a cancelled token always surfaces as itself.

## `PythonProcessException.cs`

`public sealed class PythonProcessException : PythonException` — Python code that failed (nonzero exit or timeout).

- Constructor: `PythonProcessException(string message, PythonResult result, PythonErrorKind kind = PythonErrorKind.ExecutionFailed)`.
- `Result` (`PythonResult { get; }`) — the full result: `ExitCode`, `StandardOutput`, `StandardError`, `Duration`.
- Thrown with `Kind = PythonErrorKind.Timeout` specifically when `Subprocess.RunAsync`'s `timeout` elapses; otherwise `Kind = ExecutionFailed` (the default).
- Also thrown by `PythonResult.EnsureSuccess()` and by `PythonVirtualEnvironment.ExecuteAsync` when a buffered run is unsuccessful and `RunOptions.ThrowOnError` is `true` (the default).

## See also

- [Error-Handling.md](../../Error-Handling.md) — usage patterns, catch-by-`Kind` examples.
- [Models.md](Models.md) — `PythonResult`, the type carried by `PythonProcessException`.
- [Extensibility.md](Extensibility.md) — `Subprocess.RunAsync`, the lowest-level thrower of both exception types.
