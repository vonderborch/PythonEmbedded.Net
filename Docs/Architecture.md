# Architecture

PythonEmbedded.Net 2.x is deliberately small: one core assembly, one namespace, ~17 public types, no reflection, no registries, no service locators. This document explains the moving parts.

## The shape

```
PythonEnvironment (static facade)
  └── PythonHost (internal engine: resolution, locking, disk layout)
        ├── IPythonSource[]    — where interpreters come from
        ├── IPackageInstaller  — how envs are created / packages managed
        └── IPythonRunner      — how code executes
```

Users interact with three sealed handles — `PythonInstallation`, `PythonVirtualEnvironment`, `PythonProcess` — plus records for options and results. Everything else is internal.

## The three interfaces

All extensibility flows through three small interfaces, configured via `PythonEnvironment.Configure` (explicit instances — satellites are never auto-discovered). `Configure` sets process-wide *defaults*; `GetEnvironmentAsync` also accepts optional `installer`/`runner` parameters that override the default for one environment — see below.

- **`IPythonSource.TryInstallAsync(request, targetDir, context, ct)`** — return null to pass, or materialize a full install tree and return its metadata. The `SourceContext` provides shared plumbing (HTTP, checksum-verified download cache, ETag'd JSON cache) so sources stay tiny. Built-in: bundled archives (`python-embedded-runtimes/` beside the app), astral downloads. Satellite-able: compile-from-source, custom mirrors.
- **`IPackageInstaller`** — creates environments and performs package operations. Built-in: pip/venv. Satellites: uv, conda (micromamba), poetry.
- **`IPythonRunner.RunAsync(env, invocation, ct)`** — executes a `PythonInvocation` (script/code/module + args + options) and returns a buffered result. Built-in: subprocess. Satellite: in-process via Python.NET. Runners never throw on nonzero exit; the environment handle applies the `ThrowOnError` policy so all runners behave consistently.

Each interface has a matching abstract base — `PythonSourceBase`, `PackageInstallerBase`, `PythonRunnerBase` — that implementations can extend for shared helpers instead of implementing the raw interface. The interfaces stay public and directly implementable too, for the rare case of one class covering multiple of them. `PackageInstallerBase` is where this actually pays off: it hoists the run-a-subprocess-and-throw-on-failure pattern (`RunOrThrowAsync`), the standard `python -m venv` environment creation (`CreateVenvAsync`), plain pip install/uninstall/list (`PipInstallAsync`/`PipUninstallAsync`/`PipListAsync`), and pinned-tool provisioning into a private venv (`ProvisionPinnedToolAsync`/`ProvisionToolViaPipAsync`) — all four built-in/satellite installers (pip, uv, conda, poetry) extend it. `PythonSourceBase` and `PythonRunnerBase` currently add no shared logic (the built-ins don't overlap enough to be worth hoisting) but exist for symmetry and as a stable base to build on.

`PythonProcess` (live handles from `env.Start`) is deliberately outside `IPythonRunner`: a streaming subprocess is the only sane implementation, and keeping it separate keeps the runner single-purpose.

## How `GetEnvironmentAsync("3.13", name)` resolves

1. Parse the version request; `name` is required — there is no default, since one version can back multiple independent environments.
2. Lock-free scan of `installs/*/install.json` for a match — the warm path, no locks, no network.
3. Miss → acquire `locks/install-3.13.lock` (file lock), re-check, then try each source in order into a staging dir under `tmp/`.
4. Success → atomic `Directory.Move` into `installs/<id>/`, then write `install.json` **last**.
5. Environment: check `envs/<id>/<name>/env.json`; miss → env lock → the effective installer's `CreateEnvironmentAsync` (in place — venvs embed absolute paths) → write `env.json` last, recording the installer's `Name`.

## Per-environment installer/runner overrides

`GetEnvironmentAsync` (facade, `PythonInstallation`, and internally `PythonHost`) accepts optional `installer`/`runner` parameters alongside `version`/`name`. Each falls back to `PythonOptions.Installer`/`.Runner` when omitted, but the two behave asymmetrically:

- **Installer** materially affects how the environment was built on disk (venv layout, lockfile semantics, etc.), so it is recorded in `env.json` the first time the environment is created and fixed for that environment's lifetime. Any later `GetEnvironmentAsync` call — whether it passes an explicit `installer` or falls back to the current `Options.Installer` — must resolve to an installer whose `Name` matches what's recorded, or the call throws `PythonException(EnvironmentFailed)`. This is deliberate: switching installers on an existing environment silently would leave its on-disk state built by one tool but managed by another.
- **Runner** is a pure execution-time concern with no effect on disk state, so it is never recorded or validated. Each `GetEnvironmentAsync` call resolves and captures a runner (override or default) into the returned `PythonVirtualEnvironment` handle independently — two handles for the same environment can legitimately use different runners.

## On-disk layout

```
<root>/                        # app-local by default
  cache/
    http/                      # ETag'd JSON (release metadata)
    downloads/                 # archives, sha256-verified
    astral/                    # SHA256SUMS per release tag
  installs/cpython-3.13.14-astral/{install.json, python/...}
  envs/cpython-3.13.14-astral/{default,myenv}/    # each with env.json
  tools/                       # runtime-local uv, poetry, micromamba
  locks/                       # cross-process file locks
  tmp/                         # staging; GC'd on startup
```

**The filesystem is the index.** There is no global metadata file to corrupt. A directory exists only once its marker file (`install.json` / `env.json`) is written — and the marker is always written last, so a crash mid-install leaves a marker-less directory that is ignored and garbage-collected. Concurrency is per-operation file locks (`FileShare.None` + retry), taken only on the cold path.

## Interpreter acquisition

The astral source resolves the latest [python-build-standalone](https://github.com/astral-sh/python-build-standalone) release via the GitHub API (ETag-cached, `GITHUB_TOKEN` honored), then reads that tag's immutable `SHA256SUMS` asset — one fetch yields every asset name and checksum, sidestepping the paginated asset API entirely. Download URLs are predictable from the file name. Archives are cached and verified by sha256.

Runtime packages (`PythonEmbedded.Net.Runtime.*`) skip all of that: an MSBuild `.targets` in the package copies the archive matching the consumer's platform to `$(OutputPath)/python-embedded-runtimes/`, and the bundled source (first in the default source list) picks it up. Plug-in by filesystem convention — no code, no configuration. Their content is regenerated by a daily CI job (`.github/workflows/refresh-runtimes.yml`) that PRs manifest updates when upstream tags a new release; `source/Runtimes/manifest.json` (URLs + checksums) is the only committed input, and archives are downloaded at pack time.

## Tooling philosophy

External tools (uv, poetry, micromamba) are **strictly runtime-local**: system-installed copies are never used, so builds are reproducible and deleting the runtime root deletes everything. `Tools.EnsureAsync` resolves: `PYEMBED_TOOL_<NAME>` env var (the only escape hatch) → next to the base interpreter → `<root>/tools/` → the satellite's provision callback (pip-install into the base interpreter, or download a static binary), under a cross-process lock. Satellites that expose a `Version` pin (uv, poetry, conda's `MicromambaVersion`) resolve/provision under a version-qualified subdirectory (`<root>/tools/uv-0.5.11/`) instead, so requesting a specific version never reuses — or is shadowed by — a different pinned or "latest" install of the same tool.

## Error philosophy

Two exception types. `PythonException` with a `Kind` enum for every library failure; `PythonProcessException` (a subclass carrying the full `PythonResult`) for Python code that fails. Cancellation surfaces as `OperationCanceledException`, never wrapped. See [Error-Handling.md](Error-Handling.md).

## Sync + async

Async methods are the implementation (`ConfigureAwait(false)` throughout); sync twins are thin `GetAwaiter().GetResult()` wrappers, safe because no continuation ever needs the caller's context.
