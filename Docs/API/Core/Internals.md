# Internals

Namespace: `PythonEmbedded.Net.Internals`. All types here are `internal` — the engine behind the public facade and handles. Nothing on this page is part of the public API surface; it's documented for contributors and for understanding on-disk behavior.

## `PythonHost.cs`

`internal sealed class PythonHost` — the engine behind the static `PythonEnvironment` facade: owns the on-disk layout, resolves installations through the configured sources, and creates environments. The filesystem is the index — a directory is valid only once its marker file (`install.json` / `env.json`) exists, and that marker is always written **last**.

**Construction:** `PythonHost(PythonOptions options)` — stores `options`, resolves `_logger` (`options.Logger ?? NullLogger.Instance`), and wraps first-use directory creation + garbage collection in a `Lazy<bool> _initialized`.

**Directory layout properties** (all computed from `Options.RootDirectory`): `InstallsDirectory` (private), `EnvsDirectory` (private), `CacheDirectory` (private), `TmpDirectory` (private), `LocksDirectory` (public), `ToolsDirectory` (public).

**Public members:**

| Member | Signature | Behavior |
| --- | --- | --- |
| `Options` | `PythonOptions Options { get; }` | The configuration this host was built from. |
| `CreateToolContext` | `ToolContext CreateToolContext(PythonInstallation install)` | Ensures `ToolsDirectory` exists, returns a fresh `ToolContext` wrapping it and a new `SourceContext`. |
| `GetInstallationAsync` | `Task<PythonInstallation> GetInstallationAsync(string version, CancellationToken ct)` | Parses `version` into a `PythonVersionRequest`; lock-free scan for an existing match (`FindInstallation`); on miss, acquires `install-<sanitized-request>.lock`, re-checks under lock, then tries each `Options.Sources` entry in order against a fresh staging directory under `TmpDirectory` until one returns non-null `PythonInstallInfo` (committing it — see `CommitInstallation`) or all are exhausted, throwing `PythonException(VersionNotFound)`. A source throwing mid-attempt deletes its staging directory and the exception propagates (no fallthrough to the next source on a hard failure — only a `null` return continues the loop). |
| `GetEnvironmentAsync` (version overload) | `Task<PythonVirtualEnvironment> GetEnvironmentAsync(string version, string name, CancellationToken ct, IPackageInstaller? installer = null, IPythonRunner? runner = null)` | Resolves the installation via `GetInstallationAsync`, then delegates to the installation overload. |
| `GetEnvironmentAsync` (installation overload) | `Task<PythonVirtualEnvironment> GetEnvironmentAsync(PythonInstallation install, string name, CancellationToken ct, IPackageInstaller? installer = null, IPythonRunner? runner = null)` | Validates `name` (ASCII letters/digits/`-`/`_`/`.` only, else `ArgumentException`). Lock-free `TryLoadEnvironment` check; on miss, acquires `env-<installId>-<sanitized-name>.lock`, re-checks under lock. If a marker-less directory already exists at the target path, it's a dead leftover from a crashed creation and is deleted before retrying. Resolves `effectiveInstaller`/`effectiveRunner` (parameter, else `Options.Installer`/`Options.Runner`), calls `effectiveInstaller.CreateEnvironmentAsync`, probes for the resulting interpreter via `VenvExecutableCandidates`, and — only after that succeeds — writes `env.json` with an `EnvMetadata` recording the **installer's name** (not the runner's). Deletes the environment directory if creation or probing fails. |
| `ListInstallationsAsync` | `Task<IReadOnlyList<PythonInstallation>> ListInstallationsAsync(CancellationToken ct)` | Enumerates `InstallsDirectory` subdirectories, keeping only those `TryLoadInstallation` accepts (valid marker + existing executable). |
| `RemoveAsync` | `Task RemoveAsync(PythonInstallation install, CancellationToken ct)` | Under an `install-remove-<installId>.lock`, deletes `<envs>/<installId>/` (all environments) and then the installation directory itself, both with `throwOnFailure: true`. |

**Private resolution helpers:**

- `CommitInstallation(PythonInstallInfo info, string staging)` — determines the relative python path (from `info.RelativePythonPath`, or by probing `staging` with `InstallExecutableCandidates`; throws `PythonException(InstallFailed)` if neither works), computes `installId = "cpython-{version}-{sanitized-source-name}"`, deletes any marker-less leftover at the final directory, atomically `Directory.Move`s staging into place, runs `SysconfigPatcher.Patch`, writes `install.json`, and returns the new `PythonInstallation`.
- `FindInstallation(PythonVersionRequest request)` — scans install directories, loads each via `TryLoadInstallation`, filters by `request.Matches`, returns the highest `Version` match.
- `TryLoadInstallation(string directory)` — reads/parses `install.json` into the private `InstallMetadata` record; returns `null` if the marker is missing/corrupt or the recorded python path doesn't exist.
- `TryLoadEnvironment(PythonInstallation install, string name, string envDirectory, IPackageInstaller? installer, IPythonRunner? runner)` — reads/parses `env.json` into the private `EnvMetadata` record; returns `null` on missing marker or missing executable. **Enforces the installer-recording rule**: resolves `effectiveInstaller` (parameter or `Options.Installer`) and throws `PythonException(EnvironmentFailed)` if its `Name` doesn't match `metadata.Installer` — an environment's installer is fixed for its lifetime. The runner is resolved the same way (parameter or `Options.Runner`) but never checked against anything, since it isn't recorded.

**Plumbing helpers:**

- `CreateSourceContext()` — builds a `SourceContext` from `Options` plus the shared/overridden `HttpClient`, current `PlatformTriple`, and `_logger`.
- `EnsureInitialized()` / `Initialize()` — first-use directory creation (`installs/`, `envs/`, `cache/`, `locks/`, `tmp/`) plus `CollectGarbage()`, run exactly once via `Lazy<bool>`.
- `CollectGarbage()` — deletes `tmp/` subdirectories older than 1 day, and `installs/` subdirectories older than 1 day that still lack `install.json` (crashed mid-install leftovers), logging a warning for the latter.
- `InstallExecutableCandidates` / `VenvExecutableCandidates` (static readonly `string[]`, OS-dependent) — relative paths tried when probing for an interpreter inside a fresh install tree vs. a fresh venv. The Windows venv list includes both `Scripts/python.exe` and a bare `python.exe` at the env root (the second covers conda-style environments).
- `ProbeExecutable(string root, string[] candidates)` — returns the first candidate (resolved to a full path) that exists as a file.
- `TryReadJson<T>(string path)` — returns `null` on missing file, `IOException`, or `JsonException` rather than throwing.
- `TryDeleteDirectory(string directory, bool throwOnFailure = false)` — best-effort recursive delete; swallows `IOException`/`UnauthorizedAccessException` unless `throwOnFailure`.
- `EnumerateDirectoriesSafe(string root)` — empty sequence instead of throwing if `root` doesn't exist.
- `Sanitize(string value)` — replaces any character that isn't an ASCII letter/digit/`-`/`_`/`.` with `-`, for building lock/directory names from arbitrary version-request or environment-name strings.
- `CreateSharedHttpClient()` — one process-wide `HttpClient` with a `User-Agent: PythonEmbedded.Net` header, used when `Options.HttpClient` is null.

**Private records:** `InstallMetadata(string Version, string SourceName, string Triple, DateTimeOffset InstalledAt, string? Checksum, string PythonExecutable)` — the `install.json` schema. `EnvMetadata(string Name, string InstallId, string Installer, DateTimeOffset CreatedAt, string PythonExecutable)` — the `env.json` schema; `Installer` is the field the installer-recording rule reads and writes.

## `DiskLock.cs`

`internal sealed class DiskLock : IDisposable` — a cross-process exclusive lock backed by a file opened with `FileShare.None` and `FileOptions.DeleteOnClose`.

- `static Task<DiskLock> AcquireAsync(string lockFilePath, TimeSpan timeout, CancellationToken ct)` — creates the lock file's parent directory, then loops opening the file exclusively; on `IOException` (already held) retries every 100ms until `timeout` elapses, then throws `PythonException(Locked)`. Observes `ct` between attempts.
- `Dispose()` — disposes the underlying `FileStream`, which deletes the lock file (via `DeleteOnClose`).

## `SysconfigPatcher.cs`

`internal static partial class SysconfigPatcher` — python-build-standalone's `install_only` archives bake the build machine's absolute install path (`/install`) into `_sysconfigdata_*.py`. Left unpatched, `sysconfig.get_config_var(...)` (and anything that reads it — building C extensions from source, some build backends) resolves paths that don't exist on the target machine.

- `Patch(string installDirectory)` — no-op on Windows (that platform's builds don't carry this file). Finds the sysconfig data file (`FindSysconfigDataFile`, searching recursively since archive layouts vary), rewrites every string literal token that starts with `/install` to `installDirectory` instead, and rewrites the file only if something actually changed. Safe to call unconditionally, including on installs that don't need it.
- `FindSysconfigDataFile(string installDirectory)` (private) — recursive search for `_sysconfigdata_*.py`, skipping reparse points (symlinks).
- Mirrors what astral's own tooling does after extraction; see the [python-build-standalone quirks doc](https://gregoryszorc.com/docs/python-build-standalone/main/quirks.html) referenced in the source comment.

## `ArchiveExtractor.cs`

`internal static class ArchiveExtractor` — extracts python-build-standalone archives (`.tar.gz`/`.tgz` or `.zip`) preserving Unix permissions.

- `Task ExtractAsync(string archivePath, string targetDirectory, CancellationToken ct)` — `.zip` via `ZipFile.ExtractToDirectory`; `.tar.gz`/`.tgz` via `System.Formats.Tar.TarFile.ExtractToDirectoryAsync` over a `GZipStream` (this is what preserves Unix file-mode bits, unlike a naive zip-style extraction). Any other extension throws `PythonException(InstallFailed)`. Any unexpected exception during extraction is caught and rethrown as `PythonException(InstallFailed)` (cancellation and existing `PythonException`s pass through unchanged).

## `ArchiveName.cs`

`internal sealed partial record ArchiveName(string FileName, PythonVersion Version, string Tag, string Triple)` — a parsed python-build-standalone `install_only` archive file name, e.g. `cpython-3.13.14+20260623-aarch64-apple-darwin-install_only.tar.gz`.

- `TryParse(string fileName)` — regex match (`^cpython-(?<version>...)\+(?<tag>\d+)-(?<triple>...)-install_only(_stripped)?\.(tar\.gz|tgz|zip)$`) plus `PythonVersion.TryParse` on the captured version; returns `null` on any failure.
- `SelectBest(IEnumerable<string> fileNames, PythonVersionRequest request, PlatformTriple platform)` — parses every file name, filters to those that parse, match `request`, and match `platform.Value` exactly, then picks the entry with the highest `Version`, tie-broken by the newest `Tag` (ordinal string compare — tags are date-like, e.g. `20260623`, so ordinal comparison orders them correctly). Used by both `DirectorySource` and `AstralSource`.

## See also

- [Facade.md](Facade.md) — the public surface `PythonHost` sits behind.
- [Extensibility.md](Extensibility.md) — `IPythonSource`/`IPackageInstaller`/`IPythonRunner`, `SourceContext`, `Tools` — all consumed by `PythonHost`.
- [BuiltInSources.md](BuiltInSources.md) — `DirectorySource`/`AstralSource`, both consumers of `ArchiveName`/`ArchiveExtractor`.
- [../../Troubleshooting.md](../../Troubleshooting.md#inspect-the-on-disk-layout-directly) — the on-disk layout from a user's perspective.
