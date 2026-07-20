# Built-in Runner

Namespace: `PythonEmbedded.Net.Runners`.

## `ProcessRunner.cs`

`internal sealed class ProcessRunner : PythonRunnerBase` — the default `IPythonRunner`: a buffered one-shot subprocess. This is `PythonOptions.Runner`'s default value (`new ProcessRunner()`).

`RunAsync(PythonVirtualEnvironment env, PythonInvocation invocation, CancellationToken ct)`:

1. Builds the argument list from `invocation.Kind`:
   - `Script` → `[invocation.Target, ...invocation.Args]`
   - `Code` → `["-c", invocation.Target, ...invocation.Args]`
   - `Module` → `["-m", invocation.Target, ...invocation.Args]`
2. When `!env.IsBase`, mirrors venv activation so child tooling resolves the environment correctly: sets `VIRTUAL_ENV = env.Directory` and prepends the venv's bin/Scripts directory to `PATH`.
3. Merges in `invocation.Options.Environment` last, so callers can override the activation variables above.
4. Delegates to `Subprocess.RunAsync(env.PythonExecutable, args, invocation.Options.WorkingDirectory, environment, invocation.Options.Stdin, invocation.Options.Timeout, ct)` (see [Extensibility.md](Extensibility.md) for `Subprocess`'s exact semantics — including that it never throws on nonzero exit; `PythonVirtualEnvironment.ExecuteAsync` decides whether to throw).

No other members. This is the runner used by `RunAsync`/`RunCodeAsync`/`RunModuleAsync` on every `PythonVirtualEnvironment` unless a different `IPythonRunner` was configured or passed per-call; `PythonProcess.Start` always uses its own subprocess logic directly (see [Handles.md](Handles.md)) regardless of which `IPythonRunner` is configured.

## See also

- [Extensibility.md](Extensibility.md) — `Subprocess.RunAsync`, the actual process-execution primitive.
- [../Satellites/PythonNet.md](../Satellites/PythonNet.md) — the alternative in-process `IPythonRunner`.
