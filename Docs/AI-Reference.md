# AI Reference

Machine-oriented reference for using PythonEmbedded.Net correctly. Read this before generating code
against the library. It's dense on purpose — prose explanations live in the other `Docs/*.md` files
linked at the bottom; this file exists to prevent the specific mistakes an LLM tends to make.

## Mental model

- One static facade: `PythonEnvironment`. No DI registration, no service locator, no instances to construct.
- Three sealed handles you get back from it: `PythonInstallation` (a base interpreter), `PythonVirtualEnvironment`
  (a named env off an installation), `PythonProcess` (a live, long-running process).
- Three small interfaces are the *only* extension points: `IPythonSource`, `IPackageInstaller`, `IPythonRunner`.
  Everything else is `sealed`. If a task looks like it needs a new abstraction, it doesn't — it needs one of
  these three, or it's not actually extending the library.
- Every `*Async` method has a synchronous twin with the same name minus `Async` (e.g. `RunAsync` / `Run`,
  `GetEnvironmentAsync` / `GetEnvironment`). **Always call the sync twin instead of `.GetAwaiter().GetResult()`
  or `.Result` on an async call** — the sync twins exist precisely so you never hand-roll blocking.

## Golden path

```csharp
using PythonEmbedded.Net;

var env = await PythonEnvironment.GetEnvironmentAsync("3.13", "myapp");
await env.Packages.InstallAsync("requests");
var result = await env.RunAsync("script.py");
```

That's the whole happy path: acquire-or-reuse an interpreter, create-or-reuse a named venv, install packages,
run code. Nothing else needs to happen first — `Configure` is optional and only needed to change defaults.

## Hard rules (violating these produces wrong or broken code)

1. **`name` is required on every environment call, with no default value.** There is no "the" environment for
   a version — `GetEnvironmentAsync("3.13", "myapp")` and `GetEnvironmentAsync("3.13", "worker")` are two
   independent venvs off the same interpreter. Never invent a default like `"default"` unless the calling
   code actually wants a venv literally named `"default"`.
2. **`PythonEnvironment.Configure` must be called before any other member, exactly once, at startup**, and
   only if defaults need changing. Calling it after the first `Get*Async`/`Get*` call throws
   `InvalidOperationException`. Never call it lazily or per-request.
3. **Nonzero exit throws by default.** `RunAsync`/`RunCode`/`RunModule` throw `PythonProcessException`
   (carries `Result` with exit code + stdout/stderr) on nonzero exit. Pass
   `new RunOptions { ThrowOnError = false }` only if the caller genuinely wants to inspect a failing result
   instead of catching. Don't wrap every call in try/catch by reflex — let it throw unless told otherwise.
4. **Calling `GetEnvironmentAsync`/`GetInstallationAsync` again with the same arguments is cheap and correct**
   — it returns the existing installation/environment from disk in milliseconds, does not reinstall, does not
   recreate the venv. Don't build your own caching layer on top; don't guard calls with "if not already
   created" checks — the library already does that.
5. **Cancellation is never wrapped.** A cancelled call surfaces as a plain `OperationCanceledException`, not
   `PythonException`. Don't catch `PythonException` expecting to catch cancellation too.
6. **Installer is recorded per environment; runner is not.** See "Per-environment overrides" below before
   passing `installer`/`runner` arguments — get this backwards and code will throw
   `PythonException(EnvironmentFailed)` on the second call to a shared environment.
7. **`PythonProcess` (from `env.Start(...)`) is for long-lived processes only** (servers, supervised loops):
   streamed output via events, writable stdin, explicit `Kill()`/`DisposeAsync()`. For anything that runs to
   completion and returns output, use `RunAsync`/`RunCodeAsync`/`RunModuleAsync` instead — don't use `Start`
   as a heavier substitute for a one-shot call.
8. **Two exception types only**: `PythonException` (has `Kind` : `PythonErrorKind`) and
   `PythonProcessException : PythonException` (adds `Result`). There is no per-failure-mode exception type —
   branch on `.Kind`, don't invent `catch` blocks for exception types that don't exist in this library.

## Full public surface

```csharp
public static class PythonEnvironment
{
    static void Configure(Action<PythonOptions> configure);                 // once, before first use; throws after
    static Task<PythonVirtualEnvironment> GetEnvironmentAsync(
        string version, string name, CancellationToken ct = default,
        IPackageInstaller? installer = null, IPythonRunner? runner = null);
    static Task<PythonInstallation> GetInstallationAsync(string version, CancellationToken ct = default);
    static Task<IReadOnlyList<PythonInstallation>> ListInstallationsAsync(CancellationToken ct = default);
    static Task RemoveAsync(PythonInstallation install, CancellationToken ct = default);
    // + sync twins: GetEnvironment, GetInstallation, ListInstallations, Remove
}

public sealed class PythonOptions
{
    string RootDirectory { get; set; }                 // default: app-local data folder
    IList<IPythonSource> Sources { get; }               // default: bundled archives, then astral download
    IPackageInstaller Installer { get; set; }           // default: pip + venv
    IPythonRunner Runner { get; set; }                  // default: buffered subprocess
    string? GitHubToken { get; set; }                   // default: GITHUB_TOKEN env var
    TimeSpan ReleaseCacheTtl { get; set; }               // default 24h
    bool Offline { get; set; }                          // default false; no network at all when true
    ILogger? Logger { get; set; }
    HttpClient? HttpClient { get; set; }
    TimeSpan LockTimeout { get; set; }                   // default 10 min
    PythonOptions AddDirectorySource(string directory, string name = "directory");
}

public sealed class PythonInstallation
{
    PythonVersion Version { get; }
    string Directory { get; }
    string PythonExecutable { get; }
    string SourceName { get; }
    Task<PythonVirtualEnvironment> GetEnvironmentAsync(
        string name, CancellationToken ct = default,
        IPackageInstaller? installer = null, IPythonRunner? runner = null);
    // + sync twin: GetEnvironment
}

public sealed class PythonVirtualEnvironment
{
    PythonInstallation Installation { get; }
    string Name { get; }
    string Directory { get; }
    string PythonExecutable { get; }
    bool IsBase { get; }
    PackageManager Packages { get; }

    Task<PythonResult> RunAsync(string scriptPath, string[]? args = null, RunOptions? options = null, CancellationToken ct = default);
    Task<PythonResult> RunCodeAsync(string code, RunOptions? options = null, CancellationToken ct = default);
    Task<PythonResult> RunModuleAsync(string module, string[]? args = null, RunOptions? options = null, CancellationToken ct = default);
    PythonProcess Start(string scriptPath, string[]? args = null, RunOptions? options = null);   // long-lived only
    // + sync twins: Run, RunCode, RunModule
}

public sealed class PackageManager
{
    Task InstallAsync(string package, CancellationToken ct = default);              // "requests" or "requests==2.31"
    Task InstallAsync(PackageRequest request, CancellationToken ct = default);       // multi-package/requirements/index/extras
    Task UninstallAsync(string package, CancellationToken ct = default);
    Task<IReadOnlyList<InstalledPackage>> ListAsync(CancellationToken ct = default);
    // + sync twins: Install, Uninstall, List
}

public sealed class PythonProcess : IAsyncDisposable   // from env.Start(...) — long-lived only
{
    event Action<string>? OutputLine;      // subscribe BEFORE output arrives — no replay of missed lines
    event Action<string>? ErrorLine;
    StreamWriter StandardInput { get; }
    int Id { get; }
    bool HasExited { get; }
    Task<int> WaitForExitAsync(CancellationToken ct = default);   // + sync WaitForExit
    void Kill();
    ValueTask DisposeAsync();               // kills if still running, then releases resources
}

public record PythonResult(int ExitCode, string StandardOutput, string StandardError, TimeSpan Duration)
{
    bool Success => ExitCode == 0;
    PythonResult EnsureSuccess();   // throws PythonProcessException if !Success
}

public record RunOptions
{
    string? WorkingDirectory { get; set; }
    IDictionary<string, string>? Environment { get; set; }
    TimeSpan? Timeout { get; set; }         // kills the process, throws PythonProcessException(Timeout)
    string? Stdin { get; set; }
    bool ThrowOnError { get; set; } = true; // set false to get PythonResult back instead of throwing
}

public record PackageRequest
{
    string[]? Packages { get; set; }
    string? RequirementsFile { get; set; }
    string? IndexUrl { get; set; }
    string[]? ExtraArgs { get; set; }
    string? ProjectDirectory { get; set; }  // pyproject.toml (poetry/uv) or environment.yml (conda) project root
}

public class PythonException : Exception { PythonErrorKind Kind { get; } }
public sealed class PythonProcessException : PythonException { PythonResult Result { get; } }

public enum PythonErrorKind
{
    VersionNotFound, UnsupportedPlatform, DownloadFailed, InstallFailed, EnvironmentFailed,
    PackageOperationFailed, ToolMissing, Locked, Offline, ExecutionFailed, Timeout,
}
```

Version strings accepted wherever `version` appears: `"latest"`, `"3"`, `"3.13"`, `"3.13.2"`, or a full
pre-release like `"3.15.0b3"`. Matching picks the highest installed version satisfying the request.

## The three interfaces (only extension points — do not invent others)

```csharp
public interface IPythonSource        // where interpreters come from
{
    string Name { get; }
    Task<PythonInstallInfo?> TryInstallAsync(PythonVersionRequest request, string targetDirectory, SourceContext context, CancellationToken ct);
    // return null to decline (another source will be tried); non-null commits this source's result
}

public interface IPackageInstaller    // env creation + package ops
{
    string Name { get; }              // identity used for the recorded-installer check — see below
    Task CreateEnvironmentAsync(PythonInstallation install, string envDirectory, CancellationToken ct);
    Task InstallAsync(PythonVirtualEnvironment env, PackageRequest request, CancellationToken ct);
    Task UninstallAsync(PythonVirtualEnvironment env, string package, CancellationToken ct);
    Task<IReadOnlyList<InstalledPackage>> ListAsync(PythonVirtualEnvironment env, CancellationToken ct);
}

public interface IPythonRunner        // how code executes
{
    // Never throws on nonzero exit — the caller (RunAsync etc.) applies RunOptions.ThrowOnError.
    Task<PythonResult> RunAsync(PythonVirtualEnvironment env, PythonInvocation invocation, CancellationToken ct);
}
```

Matching abstract bases exist for shared helpers: `PythonSourceBase`, `PackageInstallerBase`, `PythonRunnerBase`
(all `: I<Name>`). Prefer extending the base over implementing the raw interface unless one class needs to
cover more than one of the three interfaces. `PackageInstallerBase` in particular hoists
`RunOrThrowAsync`/`CreateVenvAsync`/`PipInstallAsync`/`PipUninstallAsync`/`PipListAsync`/
`ProvisionPinnedToolAsync` — a new pip-like installer should extend it, not reimplement subprocess plumbing.

Wiring a custom implementation in is always the same one-liner, global:

```csharp
PythonEnvironment.Configure(o => o.Installer = new MyInstaller());
```

...or scoped to one environment via the per-call override (see next section).

## Per-environment overrides — the installer/runner asymmetry

`GetEnvironmentAsync` (facade, `PythonInstallation`, internal `PythonHost`) takes optional
`IPackageInstaller? installer` and `IPythonRunner? runner` that override `PythonOptions.Installer`/`.Runner`
for one environment. **They are not symmetric — getting this wrong is the most likely mistake:**

- **Installer is recorded.** The `Name` of whichever installer actually created the environment (explicit
  argument, or `Options.Installer` if none was passed) is written into `env.json` and fixed forever. Every
  later `GetEnvironmentAsync` call for that same `(installation, name)` — even one that omits `installer`
  entirely and falls back to the current global default — must resolve to an installer whose `.Name` matches
  what's recorded, or it throws `PythonException(EnvironmentFailed)`. **Do not** call an existing environment
  with a different installer than it was created with, and be aware that changing `Options.Installer` globally
  after environments already exist can itself trigger this on their next fetch.
- **Runner is not recorded, not validated, and safe to vary.** Each call resolves and captures a runner
  (override, or current `Options.Runner`) independently. Two `GetEnvironmentAsync` calls for the same
  environment can legitimately pass different runners with no error — there is no persisted state for it.

Rule of thumb when generating code: if you're choosing an installer per-call, plan for it to be the *same*
choice every time that environment is fetched (or omit it and only set the installer once, at first creation).
If you're choosing a runner per-call, feel free to let it vary by call site.

## On-disk model (don't work around it, don't reimplement it)

- Root: `PythonOptions.RootDirectory` (app-local by default). Everything lives under it:
  `installs/`, `envs/`, `cache/`, `tools/`, `locks/`, `tmp/`.
- **The filesystem is the index.** A directory is only valid once its marker file (`install.json` /
  `env.json`) exists, written last. There is no separate metadata database — don't add one, don't cache
  installation/environment identity yourself, the library's own lock-free warm-path scan is already fast.
- Concurrency is per-operation file locks; this is cross-process safe. Don't add your own locking around
  `GetEnvironmentAsync`/`GetInstallationAsync` calls.
- Never write directly into `<root>/installs/*` or `<root>/envs/*/*` — go through the API. Manual edits will
  desync from the recorded metadata (e.g. the installer-name check above).

## Common mistakes to avoid

- Calling `PythonEnvironment.Configure` more than once, or after any `Get*` call — it throws.
- Assuming a default environment name exists — it doesn't; `name` is always required and explicit.
- Wrapping every `Run*Async` call in try/catch "just in case" — nonzero exit is supposed to throw; only
  catch where the caller actually handles the failure, or pass `ThrowOnError = false` if the result (not the
  exception) is what's needed.
- Using `.Result`/`.Wait()`/`.GetAwaiter().GetResult()` on an `*Async` call instead of calling its sync twin.
- Passing a different `installer` on a later fetch of an environment that already exists with a different one.
- Using `env.Start(...)` for something that just needs to run once and return output (use `RunAsync` instead).
- Building a cache/dictionary of environments keyed by name in application code — redundant, the library
  already resolves warm environments in milliseconds from disk.
- Assuming satellite installers/runners (uv, conda, poetry, Python.NET) are referenced by default — they're
  separate NuGet packages (`PythonEmbedded.Net.PackageManagers.*`, `PythonEmbedded.Net.Runners.*`) that must be
  added and wired via `Configure` (or a per-environment override) before use.

## Where to look for more

- [Getting-Started.md](Getting-Started.md) — install, first environment, configuration walkthrough.
- [Quick-Reference.md](Quick-Reference.md) — same API surface as above, human-oriented, one page.
- [Examples.md](Examples.md) — recipes: servers, offline apps, uv/conda/poetry, in-process interop.
- [Architecture.md](Architecture.md) — why it's built this way: resolution flow, on-disk layout, satellites.
- [Error-Handling.md](Error-Handling.md) — full `PythonErrorKind` table with causes.
- [Troubleshooting.md](Troubleshooting.md) — symptom → fix.
- [Contributing.md](Contributing.md) — repo layout, build/test, adding a satellite.
