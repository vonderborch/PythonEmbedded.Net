# Troubleshooting

Common issues with PythonEmbedded.Net 2.x and how to resolve them.

## Table of Contents

- [Version Resolution Issues](#version-resolution-issues)
- [Download / Network Issues](#download--network-issues)
- [Locking Issues](#locking-issues)
- [Environment Issues](#environment-issues)
- [Package Installation Issues](#package-installation-issues)
- [Tool Provisioning (uv / conda / poetry)](#tool-provisioning-uv--conda--poetry)
- [Python.NET Issues](#pythonnet-issues)
- [Platform-Specific Issues](#platform-specific-issues)
- [Debugging Tips](#debugging-tips)

## Version Resolution Issues

### `PythonException` with `Kind = VersionNotFound`

**Possible causes:**
1. The requested version doesn't exist in any configured source.
2. `Offline = true` but no bundled runtime package or directory source has a matching archive.
3. GitHub API rate limiting is preventing the astral source from resolving releases.

**Solutions:**

```csharp
// List what's already installed locally
var installs = await PythonEnvironment.ListInstallationsAsync();
foreach (var i in installs) Console.WriteLine(i.Version);

// Use a broader version request
var env = await PythonEnvironment.GetEnvironmentAsync("3.13", "myapp");   // instead of an exact patch
```

For rate limiting, set a token:

```csharp
PythonEnvironment.Configure(o => o.GitHubToken = "ghp_...");
// or set the GITHUB_TOKEN environment variable
```

### `PythonException` with `Kind = UnsupportedPlatform`

The current OS/architecture has no matching python-build-standalone asset. Check [python-build-standalone releases](https://github.com/astral-sh/python-build-standalone/releases) for supported triples, or supply your own archive via `o.AddDirectorySource(path)`.

## Download / Network Issues

### `Kind = DownloadFailed`

**Causes:** network interruption, or a checksum mismatch against `SHA256SUMS`.

**Solutions:**
- Clear `<root>/cache/downloads/` and retry — a corrupted cached archive is re-verified on next use, but a manual clear rules it out.
- Check disk space; extraction happens under `<root>/tmp/` before the atomic move into `installs/`.

### `Kind = Offline`

An operation needed the network but `PythonOptions.Offline = true`. Either set `Offline = false`, or add a `PythonEmbedded.Net.Runtime.*` package / `AddDirectorySource` so the bundled/directory source can satisfy the request without network access.

## Locking Issues

### `Kind = Locked`

Another process (or another `PythonHost` in the same process against the same root) is installing the same version and didn't finish within `LockTimeout` (default 10 minutes).

**Solutions:**
- Increase `o.LockTimeout` if installs are slow (e.g. large archives over a slow link).
- If a process crashed mid-install, its lock file is stale; `<root>/locks/` files are safe to delete manually once you've confirmed no process holds them — the marker-file-last design means an interrupted install leaves no `install.json`, so it's simply retried.

## Environment Issues

### `Kind = EnvironmentFailed`

**Possible causes:**
1. The default `pip`/`venv` installer failed to run `python -m venv` (interpreter missing or corrupted).
2. A satellite installer (uv/conda/poetry) failed to provision its tool.
3. Insufficient permissions on `<root>/envs/`.

**Solutions:**

```csharp
// Verify the base interpreter still runs
var install = await PythonEnvironment.GetInstallationAsync("3.13");
var result = await Subprocess.RunAsync(install.PythonExecutable, ["--version"]);
Console.WriteLine(result.StandardOutput);

// If corrupted, remove and reinstall
await PythonEnvironment.RemoveAsync(install);
var env = await PythonEnvironment.GetEnvironmentAsync("3.13", "myapp");
```

### Environment not recreated when expected

Environments are cached by `(installation, name)` — calling `GetEnvironmentAsync` again with the same version and name returns the existing one. To start clean, remove the installation (which removes all its environments) via `PythonEnvironment.RemoveAsync`, or delete `<root>/envs/<install-id>/<name>/` directly and retry.

## Package Installation Issues

### `Kind = PackageOperationFailed`

```csharp
try
{
    await env.Packages.InstallAsync("nonexistent-package-xyz");
}
catch (PythonProcessException ex)
{
    // pip/uv/conda output lands in the process result
    Console.WriteLine(ex.Result.StandardError);
}
catch (PythonException ex) when (ex.Kind == PythonErrorKind.PackageOperationFailed)
{
    Console.WriteLine(ex.Message);
}
```

Package installers run as subprocesses and surface failures as `PythonProcessException` when the underlying tool exits nonzero — check `ex.Result.StandardError` first; it usually names the real cause (bad package name, network failure, unsatisfiable dependency).

### Requirements file not found

`PackageRequest.RequirementsFile` is passed straight through to the installer; verify the path exists and is relative to the working directory you expect before calling `InstallAsync`.

## Tool Provisioning (uv / conda / poetry)

### `Kind = ToolMissing`

`Tools.EnsureAsync` couldn't resolve or provision the tool. Resolution order:

1. `PYEMBED_TOOL_<NAME>` environment variable (e.g. `PYEMBED_TOOL_UV`, `PYEMBED_TOOL_MICROMAMBA`) — the escape hatch to point at a system or custom binary.
2. Next to the base interpreter.
3. `<root>/tools/`.
4. The satellite's provision callback (pip-install into the base interpreter for uv/poetry; static binary download for micromamba).

**Solutions:**

```csharp
// Point directly at an existing binary
Environment.SetEnvironmentVariable("PYEMBED_TOOL_UV", "/opt/homebrew/bin/uv");

// Or let auto-provisioning run — requires network unless already cached under <root>/tools/
```

If provisioning fails in an offline/sandboxed environment, pre-populate `<root>/tools/` yourself or set the `PYEMBED_TOOL_*` override.

### uv-created venvs and tool resolution

uv doesn't copy `uv` itself into the venv it creates. `UvInstaller` resolves `uv` from the **base interpreter**, not the venv — this is transparent to callers, but if you're inspecting `pyvenv.cfg` manually, note that `home` points at the base install, not at a bundled uv.

## Python.NET Issues

### `Kind = ExecutionFailed` from `InProcessRunner`

**Possible causes:**
1. `PythonNetHost.Initialize` was never called.
2. libpython couldn't be found next to the base interpreter.
3. A second, different environment was targeted after the engine already bound to the first (Python.NET is one engine per process).

**Solutions:**

```csharp
var env = await PythonEnvironment.GetEnvironmentAsync("3.13", "myapp");
PythonNetHost.Initialize(env);   // must run before any RunAsync via InProcessRunner

var result = await env.RunCodeAsync("print('ok')");
```

Because the engine is process-global, you cannot switch to a different interpreter's `InProcessRunner` later in the same process — use the subprocess runner (the default) for that environment instead.

### Traceback in `PythonProcessException`

`InProcessRunner` captures the Python traceback into `Result.StandardError` the same way the subprocess runner does — inspect `ex.Result.StandardError` for the failing Python code's stack trace.

## Platform-Specific Issues

### Windows

- Executable is `python.exe` at the install/env root, not `bin/python3`.
- Antivirus / SmartScreen can slow first-run of a freshly extracted interpreter; this is a one-time cost.

### Linux

Missing system shared libraries under the extracted interpreter:

```bash
ldd <root>/installs/*/python/bin/python3
sudo apt-get install -f
```

### macOS

- Gatekeeper may prompt on first execution of a freshly downloaded interpreter; this is expected for unsigned python-build-standalone binaries.
- Apple Silicon vs Intel: `PlatformTriple` detection picks the matching build automatically; cross-architecture runs (e.g. under Rosetta) require explicitly targeting the other triple via a directory source.

## Debugging Tips

### Enable logging

```csharp
using var loggerFactory = LoggerFactory.Create(b => b.AddConsole().SetMinimumLevel(LogLevel.Debug));

PythonEnvironment.Configure(o => o.Logger = loggerFactory.CreateLogger("Python"));
```

### Inspect the on-disk layout directly

```
<root>/
  installs/<id>/install.json     # presence = install is complete
  envs/<id>/<name>/env.json      # presence = env is complete
  locks/                         # stale lock files are safe to remove once confirmed unheld
  tmp/                           # GC'd on next startup
```

A directory under `installs/` or `envs/` with no marker file is a leftover from an interrupted operation — it's ignored by the warm path and cleaned up automatically on the next `Python` startup in that root.

### Verify an installation manually

```csharp
var install = await PythonEnvironment.GetInstallationAsync("3.13");
Console.WriteLine(install.PythonExecutable);
Console.WriteLine(install.Directory);

var result = await Subprocess.RunAsync(install.PythonExecutable, ["--version"]);
Console.WriteLine(result.StandardOutput);
```

## Getting Help

1. Check [Error-Handling.md](Error-Handling.md) for the exception model.
2. Review [Examples.md](Examples.md) for working patterns.
3. Check GitHub issues for similar problems.
4. When filing a new issue, include: PythonEmbedded.Net version, .NET version, platform, the `PythonErrorKind` (if applicable), and steps to reproduce.

## See Also

- [Error Handling](Error-Handling.md)
- [Examples](Examples.md)
- [Quick Reference](Quick-Reference.md)
- [Architecture](Architecture.md)
