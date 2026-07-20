# PythonNet (`PythonEmbedded.Net.Runners.PythonNet`)

Namespace: `PythonEmbedded.Net.Runners.PythonNet`. Activate with:

```csharp
var env = await PythonEnvironment.GetEnvironmentAsync("3.13", "myapp");
PythonNetHost.Initialize(env);   // must run before any RunAsync via InProcessRunner
PythonEnvironment.Configure(o => o.Runner = new InProcessRunner());
```

Two types, tightly coupled: `InProcessRunner` is the public `IPythonRunner`; `PythonNetHost` is the process-global engine it drives.

## `InProcessRunner.cs`

`public sealed class InProcessRunner : PythonRunnerBase` — an `IPythonRunner` that executes Python in-process via [Python.NET](https://github.com/pythonnet/pythonnet) — roughly an order of magnitude faster than a subprocess for short scripts, at the cost of process-lifetime binding to one environment and no mid-run cancellation or timeout (CPython cannot be interrupted from outside once running).

- `private const string Trampoline` — a Python source string (`_pyembed_run(kind, target, args, stdin_text, cwd)`) that mirrors `python -c`/`-m`/script semantics inside the shared engine: sets up `sys.argv`, redirects `sys.stdout`/`stderr`/`stdin` to in-memory buffers, optionally `os.chdir`s, dispatches on `kind` (`"code"` → `exec(compile(...))`; `"script"` → reads and `exec`s the file with `__file__` set; anything else → `runpy.run_module(..., alter_sys=True)`), catches `SystemExit` (mapping `None`→0, `int`→that code, anything else → prints it to stderr and uses exit code 1) and any other `BaseException` (prints traceback, exit code 1), restores all the swapped globals in a `finally`, and returns `(exit_code, stdout_text, stderr_text)`.
- `RunAsync(PythonVirtualEnvironment env, PythonInvocation invocation, CancellationToken ct)` (`override`) — checks `ct` up front (cancellation is only observed *before* execution starts, never during), calls `PythonNetHost.Initialize(env)` (idempotent for the same environment), then runs the actual execution on a `Task.Run` worker thread so the async caller isn't blocked. Inside that thread: `PythonNetHost.RunInScope` executes the `Trampoline` in a fresh scope, maps `invocation.Kind` to the trampoline's `"code"`/`"script"`/`"module"` strings, invokes `_pyembed_run` with the invocation's target/args/stdin/working-directory, and converts the returned Python tuple into a `PythonResult` (using `PyObject.As<T>()` for exit code and captured stdout/stderr, and measuring elapsed time via `Stopwatch`).

## `PythonNetHost.cs`

`public static class PythonNetHost` — the process-global Python.NET engine, bound to **one** environment for the lifetime of the process (a CPython limitation, not this library's). Initialized lazily by `InProcessRunner`, or explicitly via `Initialize`.

| Member | Signature | Notes |
| --- | --- | --- |
| `BoundEnvironment` | `static PythonVirtualEnvironment? BoundEnvironment { get; }` | `null` until `Initialize` has run once. |
| `Initialize` | `static void Initialize(PythonVirtualEnvironment env)` | Idempotent for the same environment (compares `Directory` by ordinal string equality). Throws `PythonException(ExecutionFailed)` if already bound to a **different** environment — CPython cannot be re-initialized in-process; use one environment per process with this runner, or fall back to the subprocess runner for others. On first call: resolves `libpython` via `FindLibPython`, sets `Runtime.PythonDLL`, calls `PythonEngine.Initialize()` + `PythonEngine.BeginAllowThreads()`, then under `Py.GIL()` calls `ActivateEnvironment(env)`. All guarded by a `lock (SyncRoot)`. |
| `RunInScope(Action<PyModule>)` | `static void RunInScope(Action<PyModule> action)` | Requires prior `Initialize` (throws `PythonException(ExecutionFailed)` via `EnsureInitialized` otherwise); runs `action` under `Py.GIL()` in a fresh `Py.CreateScope()`. |
| `RunInScope<T>(Func<PyModule, T>)` | `static T RunInScope<T>(Func<PyModule, T> func)` | Same, returning a value. Used by `InProcessRunner.RunAsync`. |
| `AcquireGil` | `static IDisposable AcquireGil()` | For manual interop outside the trampoline pattern; requires prior `Initialize`; dispose the returned handle to release the GIL. |
| `EnsureInitialized` | `private static void EnsureInitialized()` | Throws `PythonException(ExecutionFailed)` with a message pointing at `PythonNetHost.Initialize(env)` if `_bound` is null. |
| `ActivateEnvironment` | `private static void ActivateEnvironment(PythonVirtualEnvironment env)` | No-op when `env.IsBase`. Otherwise computes the venv's site-packages path (`Lib/site-packages` on Windows; `lib/python{major}.{minor}/site-packages` on POSIX) and, in a scope, runs Python that sets `sys.prefix`/`sys.exec_prefix` to the env directory and `site.addsitedir`s the site-packages path if not already on `sys.path` — mirrors what activating a venv does, without a subprocess. |
| `FindLibPython` | `private static string FindLibPython(PythonInstallation install)` | Candidate paths by OS: Windows → `python{major}{minor}.dll` next to the interpreter; macOS → `libpython{major}.{minor}.dylib` under `<root>/python/lib/` or `<root>/lib/`; Linux → `libpython{major}.{minor}.so[.1.0]` under the same two candidate roots. Returns the first existing candidate; throws `PythonException(ExecutionFailed)` if none exist, noting that the in-process runner requires a full installation with a shared libpython (which python-build-standalone `install_only` builds have). |

## Known limitations (from the source's own doc comments)

- One Python.NET engine per process — switching to a different environment after the engine is bound requires the subprocess runner instead.
- No mid-run cancellation or timeout: `ct` is only checked before execution starts; `RunOptions.Timeout` has no effect under this runner (unlike `ProcessRunner`, which enforces it via `Subprocess.RunAsync`).

## See also

- [Core/BuiltInRunner.md](../Core/BuiltInRunner.md) — `ProcessRunner`, the default this replaces.
- [../../Troubleshooting.md](../../Troubleshooting.md#pythonnet-issues) — user-facing troubleshooting for `PythonNetHost` initialization failures.
