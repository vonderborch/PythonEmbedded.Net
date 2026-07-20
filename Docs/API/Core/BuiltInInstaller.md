# Built-in Installer

Namespace: `PythonEmbedded.Net.PackageManagers`.

## `PipInstaller.cs`

`internal sealed class PipInstaller : PackageInstallerBase` — the default `IPackageInstaller`: `python -m venv` + `python -m pip`. This is `PythonOptions.Installer`'s default value (`new PipInstaller()`).

Every method is a one-line delegation to a protected helper on `PackageInstallerBase` (see [Extensibility.md](Extensibility.md) for their exact behavior):

| Member | Delegates to |
| --- | --- |
| `Name` → `"pip"` | — |
| `CreateEnvironmentAsync(PythonInstallation install, string envDirectory, CancellationToken ct)` | `CreateVenvAsync(install, envDirectory, ct)` — `python -m venv <envDirectory>` |
| `InstallAsync(PythonVirtualEnvironment env, PackageRequest request, CancellationToken ct)` | `PipInstallAsync(env, request, ct)` |
| `UninstallAsync(PythonVirtualEnvironment env, string package, CancellationToken ct)` | `PipUninstallAsync(env, package, ct)` |
| `ListAsync(PythonVirtualEnvironment env, CancellationToken ct)` | `PipListAsync(env, ct)` |

There is no `PipInstaller`-specific logic beyond this wiring — all real behavior lives in `PackageInstallerBase`, shared with `PoetryInstaller`'s ad-hoc (non-project) fallback path.

## See also

- [Extensibility.md](Extensibility.md) — `PackageInstallerBase`'s shared helpers.
- [../Satellites/Poetry.md](../Satellites/Poetry.md) — the other installer that reuses these same pip helpers directly.
