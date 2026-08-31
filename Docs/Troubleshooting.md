# Troubleshooting

Common issues with PythonEmbedded.Net 2.x and how to resolve them.

## Table of Contents

- [Version Resolution Issues](#version-resolution-issues)
- [Download / Network Issues](#download--network-issues)
- [Locking Issues](#locking-issues)
- [Environment Issues](#environment-issues)
- [Package Installation Issues](#package-installation-issues)
- [Tool Provisioning (uv / conda / poetry)](#tool-provisioning-uv--conda--poetry)
- [Source Builds](#source-builds)
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

### `Kind = EnvironmentFailed`: "cannot be reopened with a different installer"

An environment's installer is recorded when it's first created and fixed for its lifetime (see [Architecture.md](Architecture.md#per-environment-installerrunner-overrides)) — this is not the same restriction as above. It's thrown when a `GetEnvironmentAsync` call resolves to an installer (an explicit `installer` argument, or the current `Options.Installer` default) whose `Name` doesn't match the one recorded for that environment, which usually means either `Options.Installer` changed after the environment was created, or the wrong `installer` argument was passed. Pass the matching installer explicitly, or remove and recreate the environment if you actually want to switch tools.

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

## Source Builds

Issues specific to `PythonEmbedded.Net.Sources.SourceBuild` (`SourceBuildSource`). Every build writes a full transcript to `<root>/cache/source-build/logs/<version>-<timestamp>.log`, and every failure message ends with that path — read it first; the exception carries only the last 40 lines.

### The first call takes tens of minutes

Expected. `Optimize`/`Lto` default to on (PGO + LTO), which is 15–40 minutes for a first build. The result is cached like any other installation, so later calls return in milliseconds. Pass an `IProgress<InstallProgress>` to `GetEnvironmentAsync` to see `Building` with `configure` / `make` / `make install` in `Detail`, and use `new SourceBuildSource { Optimize = false, Lto = false }` while iterating.

### `Kind = InstallFailed`: missing build dependencies

Thrown *before* any compilation when the toolchain probe finds nothing usable and `ProvisionDependencies` is off (the default). The message names the exact command for the platform. Either run it yourself, or opt in:

```csharp
new SourceBuildSource
{
    ProvisionDependencies = true,   // brew (macOS) or a conda-forge prefix under <root>/tools/ (Linux)
    AllowElevation = true,          // additionally allow `sudo -n <pm> install …` / the winget UAC prompt
}
```

`ProvisionDependencies` alone never touches system-wide state; the system package manager and the Visual Studio Build Tools installer both additionally require `AllowElevation`. `sudo` is invoked as `sudo -n`, so it fails immediately rather than hanging on a password prompt — prime the credential cache first if you need it.

### macOS: "the Xcode Command Line Tools are required"

The one prerequisite that cannot be automated (`xcode-select --install` is a GUI flow, and `softwareupdate -i` needs root). Run it manually, then retry:

```bash
xcode-select --install
xcode-select -p          # should print a path once installed
```

### `Kind = InstallFailed`: "the interpreter compiled, but these required modules are missing"

CPython silently omits a module whose dependency was absent at *configure* time, so a clean `make` still yields a crippled interpreter. `ssl`, `zlib`, `ctypes`, `sqlite3`, and `ensurepip` are treated as fatal; `readline`, `lzma`, `bz2`, `tkinter`, and `dbm.gnu` are logged as warnings and the install is kept. Install the named development packages and rebuild. The build log also contains configure's own `The necessary bits to build these optional modules were not found` line, which names modules more precisely than the import probe.

Rebuilding means deleting the existing install first, since a completed install is what makes later calls skip the source entirely:

```csharp
var installs = await PythonEnvironment.ListInstallationsAsync();
await PythonEnvironment.RemoveAsync(installs.First(i => i.SourceName == "source-build"));
```

### A source-built interpreter stops working after deleting `<root>/tools/`

Linux-only, and only when `ProvisionDependencies` provisioned a conda-forge prefix: the interpreter links against `<root>/tools/build-deps-<X.Y>/lib` for its whole life (that is why the prefix lives in `tools/` rather than the sweepable `cache/`). Deleting it breaks the interpreter with loader errors like `libssl.so.3: cannot open shared object file`. Either restore the prefix by rebuilding, or install the dependencies system-wide and rebuild so the interpreter links against those instead.

### Only one variant of a version ever gets built

Installs are keyed `cpython-<version>-<sourceName>`, so two `SourceBuildSource` instances that differ only in build options resolve to the same install — the first one built wins and the second is never compiled. Give each variant its own `Name` (`FreeThreaded` does this for you; nothing else does):

```csharp
o.Sources.Insert(0, new SourceBuildSource { Name = "py-debug", ConfigureArguments = ["--with-pydebug"] });
```

### The source is skipped and another one wins

`TryInstallAsync` returns `null` — deliberately, so the next source gets a turn — when python.org has no matching version, or when metadata lookup fails while `Offline = true`. Build *failures* are always thrown, never swallowed. Enable logging (see below) to see which case it was at `Debug` level, and `o.Sources.Clear()` before inserting if you want the source build to be the only option.

### Windows builds fail with no network

Not fixable by caching the tarball: `PCbuild\build.bat` runs `get_externals.bat`, which downloads OpenSSL/tcl/tk and the rest from the network on every fresh build tree. Windows source builds always need network, even when everything else is offline.

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
