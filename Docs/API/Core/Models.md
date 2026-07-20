# Models

Plain data types, all under `PythonEmbedded.Net.Models`. Mostly sealed records/readonly record structs.

## `PythonVersion.cs`

`public readonly partial record struct PythonVersion(int Major, int Minor, int Patch, string? Suffix = null) : IComparable<PythonVersion>` — a concrete version, e.g. `3.13.14` or `3.15.0b3`.

- `Parse(string value)` — throws `FormatException` via `TryParse` on failure.
- `TryParse(string? value, out PythonVersion version)` — regex-based (`^(?<major>\d+)\.(?<minor>\d+)\.(?<patch>\d+)(?<suffix>[a-z].*)?$`, generated via `[GeneratedRegex]`).
- `CompareTo(PythonVersion other)` — numeric `major.minor.patch` first; at equal patch, a `null` suffix (final release) sorts **greater than** any non-null suffix (pre-release); otherwise ordinal string comparison of suffixes.
- `ToString()` → `"{Major}.{Minor}.{Patch}{Suffix}"`.

## `PythonVersionRequest.cs`

`public sealed record PythonVersionRequest(int? Major, int? Minor, int? Patch, string? Suffix, string Raw)` — a version constraint parsed from user input (`"latest"`, `"3"`, `"3.13"`, `"3.13.2"`, or a full pre-release like `"3.15.0b3"`).

- `Parse(string value)` — `"latest"` → all-null request. A full `major.minor.patch[suffix]` string parses via `PythonVersion.TryParse` and pins everything including the suffix. A bare `major` or `major.minor` (both plain integers) leaves the rest null. Anything else throws `FormatException`.
- `Matches(PythonVersion version)` — `Major`/`Minor` compared when non-null; when `Patch` is non-null, **both** `Patch` and `Suffix` must match exactly — a request for `"3.15.0"` deliberately does not match `3.15.0b3` (a fully-specified request pins the suffix too, so pre-releases are never silently substituted for a final release).
- `ToString()` → `Raw`.

## `PythonInstallInfo.cs`

`public sealed record PythonInstallInfo(PythonVersion Version, string SourceName, string Triple, DateTimeOffset InstalledAt, string? Checksum = null, string? RelativePythonPath = null)` — what `IPythonSource.TryInstallAsync` returns on success. `RelativePythonPath`, when the source knows it, avoids `PythonHost` having to probe candidate executable paths itself.

## `PlatformTriple.cs`

`public readonly record struct PlatformTriple(string Value)` — the current machine's platform triple in python-build-standalone's naming (e.g. `aarch64-apple-darwin`, `x86_64-pc-windows-msvc`, `x86_64-unknown-linux-gnu`, `aarch64-unknown-linux-gnu`).

- `Current` — static, lazily computed once via `Detect()`.
- `Detect()` (private) — maps `OperatingSystem`/`RuntimeInformation.OSArchitecture` to the triple string; throws `PythonException(UnsupportedPlatform)` for an unrecognized OS or architecture.
- `ToString()` → `Value`.

## `PackageRequest.cs`

`public sealed record PackageRequest` — init-only properties, all optional:

| Property | Type / default | Meaning |
| --- | --- | --- |
| `Packages` | `string[]`, `[]` | Package specifiers, e.g. `["requests==2.31", "numpy"]`. |
| `RequirementsFile` | `string?` | Passed through to the installer (`-r <file>` for pip/uv). |
| `IndexUrl` | `string?` | Custom package index. |
| `ExtraArgs` | `string[]`, `[]` | Appended verbatim to the underlying tool's command line. |
| `ProjectDirectory` | `string?` | Honored by the Poetry and Conda installers (project-based install); ignored by pip and uv's ad-hoc install path. |

## `InstalledPackage.cs`

`public sealed record InstalledPackage(string Name, string Version);` — one row from `PackageManager.ListAsync`.

## `InvocationKind.cs`

`public enum InvocationKind { Script, Code, Module }` — what kind of target a `PythonInvocation` names.

## `PythonInvocation.cs`

`public sealed record PythonInvocation(InvocationKind Kind, string Target, string[] Args, RunOptions Options)` — describes *what* to run; an `IPythonRunner` decides *how*. `Target` is a script path, inline code, or a module name depending on `Kind`.

## `RunOptions.cs`

`public sealed record RunOptions` — init-only properties, all optional:

| Property | Type / default | Meaning |
| --- | --- | --- |
| `WorkingDirectory` | `string?` | |
| `Environment` | `IReadOnlyDictionary<string,string>?` | Merged in last by runners, so it can override the venv-activation variables they set (`VIRTUAL_ENV`, `PATH`). |
| `Timeout` | `TimeSpan?` | Enforced by the runner itself; on expiry the process is killed and `PythonProcessException(Kind = Timeout)` is thrown (not a plain cancellation). |
| `Stdin` | `string?` | Written to the process's stdin then closed (buffered runs only; `PythonProcess.Start` exposes a live `StreamWriter` instead). |
| `ThrowOnError` | `bool`, `true` | When `false`, buffered runs return a `PythonResult` with `Success == false` instead of throwing. |

## `PythonResult.cs`

`public sealed record PythonResult(int ExitCode, string StandardOutput, string StandardError, TimeSpan Duration)` — the outcome of a buffered run.

- `Success` → `ExitCode == 0`.
- `EnsureSuccess()` — returns `this` if `Success`; otherwise throws `PythonProcessException` (message includes trimmed `StandardError` when non-blank). Used to defer the throw when `RunOptions.ThrowOnError = false` was used to inspect the result first.

## See also

- [Exceptions.md](Exceptions.md) — `PythonException`/`PythonProcessException`, thrown from several of the members above.
- [Handles.md](Handles.md) — where these models are consumed (`RunAsync` parameters, `PackageManager` return types).
- [Extensibility.md](Extensibility.md) — `IPythonSource`/`IPackageInstaller`/`IPythonRunner`, whose contracts are expressed in these types.
