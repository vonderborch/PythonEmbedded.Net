# Extensibility

Namespace: `PythonEmbedded.Net.Extensibility`. The three interfaces are the only sanctioned extension points; everything else in the library is sealed. This page also covers their optional abstract bases and the shared plumbing types handed to implementations.

## The three interfaces

### `IPythonSource.cs`

`public interface IPythonSource` — where Python installations come from (bundled archives, astral downloads, user directories, a future compile-from-source).

- `string Name { get; }` — short id used in install directory names and diagnostics, e.g. `"astral"`.
- `Task<PythonInstallInfo?> TryInstallAsync(PythonVersionRequest request, string targetDirectory, SourceContext context, CancellationToken ct)` — returns `null` if this source can't satisfy `request`; otherwise materializes a complete installation into `targetDirectory` and returns its metadata. Sources are tried in `PythonOptions.Sources` order by `PythonHost` until one returns non-null.

### `IPackageInstaller.cs`

`public interface IPackageInstaller` — how environments are created and packages managed (pip by default; uv/conda/poetry via satellites).

- `string Name { get; }` — recorded in `env.json` and fixed for that environment's lifetime (see [Handles.md](Handles.md) and [Facade.md](Facade.md)).
- `Task CreateEnvironmentAsync(PythonInstallation install, string envDirectory, CancellationToken ct)` — creates the environment **in place** at `envDirectory`; it is never moved afterward (venvs embed absolute paths).
- `Task InstallAsync(PythonVirtualEnvironment env, PackageRequest request, CancellationToken ct)`
- `Task UninstallAsync(PythonVirtualEnvironment env, string package, CancellationToken ct)`
- `Task<IReadOnlyList<InstalledPackage>> ListAsync(PythonVirtualEnvironment env, CancellationToken ct)`

### `IPythonRunner.cs`

`public interface IPythonRunner` — how Python code executes (subprocess by default; in-process via the PythonNet satellite).

- `Task<PythonResult> RunAsync(PythonVirtualEnvironment env, PythonInvocation invocation, CancellationToken ct)` — never throws on nonzero exit; the caller (`PythonVirtualEnvironment.ExecuteAsync`) applies `RunOptions.ThrowOnError`.

## Optional base classes

None of these are required — implementing an interface directly remains fully supported, including implementing more than one interface on the same class. They exist to hoist shared logic and for symmetry across the three roles.

### `PythonSourceBase.cs`

`public abstract class PythonSourceBase : IPythonSource` — `Name` and `TryInstallAsync` are both `abstract`. No shared logic (the built-in sources share little beyond the contract); kept mainly for symmetry with the other two bases.

### `PythonRunnerBase.cs`

`public abstract class PythonRunnerBase : IPythonRunner` — `RunAsync` is `abstract`. Same rationale as `PythonSourceBase`: the subprocess and in-process runners share no logic beyond the contract.

### `PackageInstallerBase.cs`

`public abstract class PackageInstallerBase : IPackageInstaller` — the one base with real shared logic, since pip/uv/conda/poetry all repeat the same patterns.

- `protected static readonly JsonSerializerOptions JsonOptions` — web-casing, for parsing tool JSON output (`pip list --format=json`, etc.).
- Abstract members: `Name`, `CreateEnvironmentAsync`, `InstallAsync`, `UninstallAsync`, `ListAsync` (re-declares the interface contract as `abstract`).
- `protected static Task<PythonResult> RunOrThrowAsync(string executable, IReadOnlyList<string> arguments, PythonErrorKind kind, string what, string? workingDirectory = null, IReadOnlyDictionary<string,string>? environment = null, CancellationToken ct = default)` — runs via `Subprocess.RunAsync`; throws `PythonException(kind, ...)` with trimmed stderr on nonzero exit.
- `protected static Task CreateVenvAsync(PythonInstallation install, string envDirectory, CancellationToken ct)` — the standard `python -m venv <dir>`, shared by installers that don't need a custom env-creation step (pip, Poetry).
- `protected static Task PipInstallAsync(...)` / `PipUninstallAsync(...)` / `PipListAsync(...)` — plain `python -m pip install/uninstall/list --format=json`, honoring `PackageRequest`'s `IndexUrl`/`RequirementsFile`/`ExtraArgs`/`Packages`. Used directly by `PipInstaller` and as Poetry's fallback for ad-hoc (non-project) installs/uninstalls/lists.
- `protected static string GetVenvPythonExecutable(string venvDirectory)` — `Scripts/python.exe` (Windows) or `bin/python` (POSIX) under a venv directory.
- `protected static Task ProvisionToolViaPipAsync(ToolContext context, string toolName, CancellationToken ct)` — pip-installs `toolName` into the **base interpreter** — the unpinned ("latest") tool-provisioning path shared by uv and Poetry.
- `protected static Task ProvisionPinnedToolAsync(ToolContext context, string toolName, string version, CancellationToken ct)` — creates a private venv at `<toolsDirectory>/<toolName>-<version>/` and pip-installs `toolName==version` into it, so multiple pinned versions (and the unpinned install) never clobber each other.

## Shared plumbing

### `SourceContext.cs`

`public sealed class SourceContext` — handed to every `IPythonSource.TryInstallAsync` call. Internal constructor only (built by `PythonHost`).

| Member | Notes |
| --- | --- |
| `Http` (`HttpClient`) | Shared client (or `PythonOptions.HttpClient` override). |
| `GitHubToken` (`string?`) | For GitHub API requests. |
| `CacheDirectory` (`string`) | Root of the cache dir (`downloads/`, `http/`, plus source-specific subfolders like `astral/`). |
| `Platform` (`PlatformTriple`) | Current machine's triple. |
| `Logger` (`ILogger`) | |
| `Offline` (`bool`) | |
| `ReleaseCacheTtl` (`TimeSpan`) | From `PythonOptions.ReleaseCacheTtl`. |
| `DownloadAsync(Uri uri, string? sha256 = null, CancellationToken ct = default)` → `Task<string>` | Downloads into `<cache>/downloads/`, skipping the download if already cached and checksum-matching. Verifies `sha256` when given, throwing `PythonException(DownloadFailed)` on mismatch. Throws `PythonException(Offline)` if offline and not cached. Downloads to a `.partial` temp file, verifies, then atomically `File.Move`s into place. |
| `GetCachedJsonAsync<T>(string key, Uri uri, TimeSpan ttl, CancellationToken ct = default)` → `Task<T?>` | Disk-cached JSON fetch: within `ttl` the cached copy is returned with zero network access; past it, an ETag-conditional GET refreshes the cache (304 → just bumps `FetchedAt`). Offline mode always returns any cached copy regardless of age, and throws `PythonException(Offline)` if nothing is cached. A failed fetch with an existing cache logs a warning and falls back to the stale copy instead of throwing. Adds a GitHub bearer token automatically when `GitHubToken` is set and the URI host ends with `github.com`. |
| `ChecksumMatchesAsync(string filePath, string sha256, CancellationToken ct)` (`internal static`) | SHA-256 comparison, case-insensitive hex. |

Private nested `CacheEnvelope` record (`ETag`, `FetchedAt`, `Body`) is the on-disk JSON cache format for `GetCachedJsonAsync`.

### `Subprocess.cs`

`public static class Subprocess` — runs an external process with buffered output. Used by the built-in runner and pip installer, and public so satellite installers/runners can drive their own tools without reimplementing process plumbing.

- `Task<PythonResult> RunAsync(string executable, IReadOnlyList<string> arguments, string? workingDirectory = null, IReadOnlyDictionary<string,string>? environment = null, string? stdin = null, TimeSpan? timeout = null, CancellationToken ct = default)`
- Redirects stdout/stderr (UTF-8), optionally stdin; merges `environment` into the child's env.
- On `timeout` elapsing, kills the entire process tree and throws `PythonProcessException(Kind = Timeout)` with whatever output was captured before the kill.
- Returns a `PythonResult` **regardless of exit code** otherwise — nonzero exit is not itself an error at this layer; callers (`RunOrThrowAsync`, `PythonVirtualEnvironment.ExecuteAsync`) decide whether to throw.
- Wraps a process-start failure in `PythonException(ExecutionFailed)`.

### `ToolContext.cs`

`public sealed class ToolContext` — what a tool-provisioning callback (passed to `Tools.EnsureAsync`) gets to work with. Internal constructor only.

- `Installation` (`PythonInstallation`) — pip-install into its base interpreter, etc.
- `ToolsDirectory` (`string`) — the runtime-local tools directory, `<root>/tools/`.
- `Sources` (`SourceContext`) — download/caching plumbing.
- `Logger` (`ILogger`) — convenience passthrough to `Sources.Logger`.

### `Tools.cs`

`public static class Tools` — resolves external tools (uv, poetry, micromamba, ...) **strictly runtime-locally**; system-installed copies are deliberately never used, so deleting the runtime root deletes its tooling.

Resolution order (in `Resolve`, private): `PYEMBED_TOOL_<NAME>` environment variable (the only escape hatch; the env var name uppercases and replaces `-` with `_`) → (version-specific requests only) `<toolsDirectory>/<name>-<version>/<name>` or its `Scripts`/`bin` subdirectory → (unversioned requests only) next to the installation's interpreter, or directly under `<toolsDirectory>`.

- `Task<string> EnsureAsync(PythonInstallation install, string name, Func<ToolContext, CancellationToken, Task> provision, CancellationToken ct = default, string? version = null)` — returns the resolved path, provisioning via `provision` under a cross-process `DiskLock` (`<root>/locks/tool-<name>[-<version>].lock`) when not yet present. Re-checks resolution after acquiring the lock (another process may have provisioned it while waiting) and again after `provision` runs. Throws `PythonException(ToolMissing)` if still unresolvable after provisioning, with a hint about the `PYEMBED_TOOL_*` override.
- `string Ensure(...)` — sync twin.

When `version` is specified, resolution is keyed under a version-specific directory so different pinned versions of the same tool coexist; the ambient "next to the interpreter" location is only consulted for unversioned requests, since an ambient binary's version can't be trusted to satisfy a pin.

## See also

- [Core/Internals.md](Internals.md) — `PythonHost`, which owns `Options.Sources`/`Installer`/`Runner` resolution and calls into these types.
- [Core/BuiltInSources.md](BuiltInSources.md), [BuiltInInstaller.md](BuiltInInstaller.md), [BuiltInRunner.md](BuiltInRunner.md) — the built-in implementations of these three interfaces.
- [../Satellites/](../README.md#satellite-packages) — satellite implementations (Uv, Conda, Poetry, PythonNet).
