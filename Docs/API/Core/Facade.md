# Facade

The two types that make up the library's public entry point. Namespace: `PythonEmbedded.Net`.

## `PythonEnvironment.cs`

`public static class PythonEnvironment` — the entry point. Holds a lazily-created, process-wide `PythonHost` (internal engine, see [Internals.md](Internals.md)) behind a `Lock`-protected singleton (`_host`), plus a mutable `PythonOptions` instance (`_options`) used to construct it.

| Member | Signature | Behavior |
| --- | --- | --- |
| `Configure` | `static void Configure(Action<PythonOptions> configure)` | Runs `configure` against the shared `PythonOptions`. Must be called before the first `Get*`/`List*`/`Remove` call — throws `InvalidOperationException` once the internal host has been created. Not idempotent-safe to call twice with conflicting settings after first use; there's no re-configuration path once frozen. |
| `GetEnvironmentAsync` | `static Task<PythonVirtualEnvironment> GetEnvironmentAsync(string version, string name, CancellationToken ct = default, IPackageInstaller? installer = null, IPythonRunner? runner = null)` | The one-liner golden path. Resolves/installs the requested `version` (see `PythonHost.GetInstallationAsync`), then gets-or-creates the named environment under it. `installer`/`runner` override `Options.Installer`/`Options.Runner` for this call only (see [Extensibility.md](Extensibility.md) and [Handles.md](Handles.md) for the recorded-vs-not-recorded asymmetry). |
| `GetInstallationAsync` | `static Task<PythonInstallation> GetInstallationAsync(string version, CancellationToken ct = default)` | Resolves/installs a base interpreter without creating any environment. |
| `ListInstallationsAsync` | `static Task<IReadOnlyList<PythonInstallation>> ListInstallationsAsync(CancellationToken ct = default)` | Scans `<root>/installs/*/install.json` and returns every installation with a valid marker and executable. |
| `RemoveAsync` | `static Task RemoveAsync(PythonInstallation install, CancellationToken ct = default)` | Deletes an installation's directory **and** its `<root>/envs/<installId>/` tree (all environments built on it). Under a per-installation lock. |
| `GetEnvironment` / `GetInstallation` / `ListInstallations` / `Remove` | sync twins | `.GetAwaiter().GetResult()` wrappers over the above. |
| `Reset` | `internal static void Reset()` | Test-only: clears `_host` and re-creates `_options`, letting `Configure` run again in-process. Used by the test suite to isolate `PythonHost` instances across test cases. |

`Host` (private property) lazily constructs `new PythonHost(_options)` inside the same lock used by `Configure`, so the first `Get*`/`List*`/`Remove` call freezes configuration from that point on.

## `PythonOptions.cs`

`public sealed class PythonOptions` — mutable configuration, read once when `PythonHost` is constructed. All defaults require zero setup.

| Member | Type / default | Notes |
| --- | --- | --- |
| `RootDirectory` | `string`, app-local (`%LocalAppData%/<entry-assembly-name>/python-embedded` or platform equivalent via `Environment.SpecialFolder.LocalApplicationData`) | Everything (`installs/`, `envs/`, `cache/`, `locks/`, `tmp/`, `tools/`) lives under here. |
| `Sources` | `IList<IPythonSource>`, `[DirectorySource("bundled", <exe-dir>/python-embedded-runtimes), AstralSource()]` | Tried in order by `PythonHost.GetInstallationAsync` until one returns non-null. `AddDirectorySource` inserts at index 0 (highest priority). |
| `Installer` | `IPackageInstaller`, `new PipInstaller()` | Default for new environments; overridable per call. See [Handles.md](Handles.md) for the recorded-installer rule. |
| `Runner` | `IPythonRunner`, `new ProcessRunner()` | Default execution strategy; overridable per call, never recorded. |
| `GitHubToken` | `string?`, `GITHUB_TOKEN` env var | Used by `AstralSource` for GitHub API requests (rate-limit avoidance). |
| `ReleaseCacheTtl` | `TimeSpan`, 24h | How long `AstralSource`'s cached "latest release" lookup stays fresh before re-checking (ETag-conditional). |
| `Offline` | `bool`, `false` | When true, no network access anywhere; only bundled/cached data is used. Sources/`SourceContext` throw `PythonException(Offline)` if nothing cached satisfies a request. |
| `Logger` | `ILogger?`, `null` (→ `NullLogger.Instance` inside `PythonHost`) | |
| `HttpClient` | `HttpClient?`, `null` (→ a shared internally-constructed client with a `User-Agent` header) | Override for proxies/custom handlers. |
| `LockTimeout` | `TimeSpan`, 10 minutes | How long `DiskLock.AcquireAsync` retries before throwing `PythonException(Locked)`. |

**Method:**

- `AddDirectorySource(string directory, string name = "directory")` — inserts a `DirectorySource` at the front of `Sources`, returns `this` for chaining. The highest-priority place to look for archives, ahead of the bundled-runtime-package convention.

Constructor wiring: the parameterless constructor is what makes every default work — it builds the default `Sources` list, `Installer`, `Runner`, and reads `RootDirectory`/`GitHubToken` from environment/assembly info at construction time (not lazily), so mutating environment variables after construction has no effect.

## See also

- [Handles.md](Handles.md) — what `GetEnvironmentAsync` returns and what you do with it.
- [Extensibility.md](Extensibility.md) — the three interfaces `Sources`/`Installer`/`Runner` are typed against.
- [Internals.md](Internals.md) — `PythonHost`, the engine both facade methods delegate to.
