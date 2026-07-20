# Handles

The objects you actually hold and call methods on after resolving Python. Namespace: `PythonEmbedded.Net`.

## `PythonInstallation.cs`

`public sealed class PythonInstallation` — a base interpreter on disk. Immutable; constructed only by `PythonHost` (internal constructor).

| Member | Signature | Notes |
| --- | --- | --- |
| `Version` | `PythonVersion Version { get; }` | The concrete resolved version (see [Models.md](Models.md)). |
| `Directory` | `string Directory { get; }` | Root of the install tree, `<root>/installs/<installId>/`. |
| `PythonExecutable` | `string PythonExecutable { get; }` | Full path to the base interpreter binary. |
| `SourceName` | `string SourceName { get; }` | Which `IPythonSource.Name` provided it (e.g. `"astral"`, `"bundled"`, or a custom directory source's name). |
| `InstallId` | `internal string InstallId { get; }` | Directory-safe id, e.g. `cpython-3.13.14-astral`; used to key environments under this install. |
| `Host` | `internal PythonHost Host { get; }` | Back-reference used by `Tools.EnsureAsync` to reach `CreateToolContext`. |
| `GetEnvironmentAsync` | `Task<PythonVirtualEnvironment> GetEnvironmentAsync(string name, CancellationToken ct = default, IPackageInstaller? installer = null, IPythonRunner? runner = null)` | Gets-or-creates a named environment under this installation. Delegates to `PythonHost.GetEnvironmentAsync(this, name, ct, installer, runner)`. |
| `GetEnvironment` | sync twin | |
| `ToString` | override | `"Python {Version} ({SourceName}) at {Directory}"` |

## `PythonVirtualEnvironment.cs`

`public sealed class PythonVirtualEnvironment` — a usable environment: run code, manage packages. Internal constructor takes `(PythonInstallation installation, string name, string directory, string pythonExecutable, bool isBase, IPackageInstaller installer, IPythonRunner runner)`; it stores `runner` in a private `_runner` field and immediately builds `Packages = new PackageManager(this, installer)` — the installer isn't stored directly on this type, only via the `PackageManager` it hands out.

| Member | Signature | Notes |
| --- | --- | --- |
| `Installation` | `PythonInstallation Installation { get; }` | The base install this environment was created from. |
| `Name` | `string Name { get; }` | Unique per installation; always explicit (there's no default name). |
| `Directory` | `string Directory { get; }` | `<root>/envs/<installId>/<name>/`. |
| `PythonExecutable` | `string PythonExecutable { get; }` | Path to the venv's (or conda env's) interpreter. |
| `IsBase` | `bool IsBase { get; }` | Always `false` for environments returned by `GetEnvironmentAsync` today — reserved for a future "use the base interpreter directly" path; runners already branch on it (e.g. `ProcessRunner` skips `VIRTUAL_ENV`/`PATH` mirroring when true). |
| `Packages` | `PackageManager Packages { get; }` | See below. |
| `RunAsync` | `Task<PythonResult> RunAsync(string scriptPath, string[]? args = null, RunOptions? options = null, CancellationToken ct = default)` | Runs a script file. Throws `PythonProcessException` on nonzero exit unless `options.ThrowOnError == false`. |
| `RunCodeAsync` | `Task<PythonResult> RunCodeAsync(string code, RunOptions? options = null, CancellationToken ct = default)` | `python -c` equivalent. |
| `RunModuleAsync` | `Task<PythonResult> RunModuleAsync(string module, string[]? args = null, RunOptions? options = null, CancellationToken ct = default)` | `python -m` equivalent. |
| `Run` / `RunCode` / `RunModule` | sync twins | |
| `Start` | `PythonProcess Start(string scriptPath, string[]? args = null, RunOptions? options = null)` | Starts a long-lived subprocess (see `PythonProcess` below). **Always** a subprocess regardless of the configured `IPythonRunner` — there is no long-lived equivalent for in-process execution. |
| `ToString` | override | `"{Installation.Version}/{Name} at {Directory}"` |

`ExecuteAsync` (private) is the shared implementation behind `RunAsync`/`RunCodeAsync`/`RunModuleAsync`: builds a `PythonInvocation`, calls `_runner.RunAsync(this, invocation, ct)`, and if the result is unsuccessful and `ThrowOnError` (default `true`) is set, wraps it in `PythonProcessException` with a message naming the script/module/code and including trimmed stderr when present.

## `PackageManager.cs`

`public sealed class PackageManager` — package operations for one environment, bound to the `IPackageInstaller` that created it. Internal constructor: `(PythonVirtualEnvironment env, IPackageInstaller installer)`.

| Member | Signature | Notes |
| --- | --- | --- |
| `InstallAsync(string)` | `Task InstallAsync(string package, CancellationToken ct = default)` | Shorthand for `InstallAsync(new PackageRequest { Packages = [package] })`. Accepts version specifiers, e.g. `"requests==2.31"`. |
| `InstallAsync(PackageRequest)` | `Task InstallAsync(PackageRequest request, CancellationToken ct = default)` | Full request: multiple packages, requirements file, index URL, extra args, project directory (poetry/conda). Delegates straight to `_installer.InstallAsync`. |
| `UninstallAsync` | `Task UninstallAsync(string package, CancellationToken ct = default)` | |
| `ListAsync` | `Task<IReadOnlyList<InstalledPackage>> ListAsync(CancellationToken ct = default)` | |
| `Install` / `Install` (overload) / `Uninstall` / `List` | sync twins | |

## `PythonProcess.cs`

`public sealed class PythonProcess : IAsyncDisposable` — a live, long-running process handle (servers, workers, supervised loops). Constructed only via the internal static `Start(PythonVirtualEnvironment env, PythonInvocation invocation)` factory, called from `PythonVirtualEnvironment.Start`.

Process setup performed by `Start`:
- Redirects stdout/stderr/stdin; `CreateNoWindow = true`, `UseShellExecute = false`.
- Sets `PYTHONUNBUFFERED=1` unconditionally (line streaming would otherwise stall behind Python's stdio buffering).
- When `!env.IsBase`, mirrors venv activation: sets `VIRTUAL_ENV` and prepends the venv's bin/Scripts directory to `PATH`.
- Applies `invocation.Options.Environment` entries last (can override the above).
- Wires `OutputDataReceived`/`ErrorDataReceived` to raise `OutputLine`/`ErrorLine` per non-null line, then calls `BeginOutputReadLine`/`BeginErrorReadLine`.
- Wraps a `process.Start()` failure in `PythonException(ExecutionFailed)`.

| Member | Signature | Notes |
| --- | --- | --- |
| `OutputLine` | `event Action<string>?` | One event per stdout line. **Not replayed** — subscribe before lines you care about are written (i.e. immediately after `Start` returns). |
| `ErrorLine` | `event Action<string>?` | Same, for stderr. |
| `StandardInput` | `StreamWriter StandardInput { get; }` | Write to the process's stdin directly. |
| `Id` | `int Id { get; }` | OS process id. |
| `HasExited` | `bool HasExited { get; }` | |
| `WaitForExitAsync` | `Task<int> WaitForExitAsync(CancellationToken ct = default)` | Awaits exit, returns the exit code. |
| `WaitForExit` | sync twin | |
| `Kill` | `void Kill()` | Kills the entire process tree if still running; swallows `InvalidOperationException` (already exited race). |
| `DisposeAsync` | `ValueTask DisposeAsync()` | Kills (if running), awaits exit, disposes the underlying `Process`. Safe to call even if the process never started or already exited. |

## See also

- [Extensibility.md](Extensibility.md) — `IPackageInstaller`/`IPythonRunner`, what `Packages` and the runner actually call into.
- [Models.md](Models.md) — `RunOptions`, `PackageRequest`, `PythonResult`, `InstalledPackage`.
- [Internals.md](Internals.md) — `PythonHost`, which constructs both `PythonInstallation` and `PythonVirtualEnvironment`.
