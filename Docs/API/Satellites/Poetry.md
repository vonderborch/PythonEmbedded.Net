# Poetry (`PythonEmbedded.Net.PackageManagers.Poetry`)

Namespace: `PythonEmbedded.Net.PackageManagers.Poetry`. Activate with:

```csharp
PythonEnvironment.Configure(o => o.Installer = new PoetryInstaller());
```

## `PoetryInstaller.cs`

`public sealed class PoetryInstaller : PackageInstallerBase` — an `IPackageInstaller` that installs a `pyproject.toml` project's dependencies with Poetry (via `PackageRequest.ProjectDirectory`); everything else (ad-hoc installs, uninstall, list) passes straight through to the environment's pip, which matches how Poetry itself treats environments it doesn't manage directly. Poetry is provisioned runtime-locally (pip-installed into the base interpreter, or a private pinned venv) on first use.

| Member | Type / signature | Notes |
| --- | --- | --- |
| `Name` | `override string` → `"poetry"` | Recorded in `env.json`. |
| `WithDevDependencies` | `bool { get; init; } = false` | When `false` (default), passes `--only main` to `poetry install`, excluding dev/group dependencies. |
| `Version` | `string? { get; init; } = null` | Poetry version to provision, e.g. `"1.8.3"`. Same unpinned-vs-pinned provisioning split as `UvInstaller`. |
| `CreateEnvironmentAsync` | `override Task CreateEnvironmentAsync(PythonInstallation install, string envDirectory, CancellationToken ct)` | Delegates to `CreateVenvAsync` (`PackageInstallerBase`'s plain `python -m venv`) — Poetry doesn't create the venv itself here; it installs into one this library already created. |
| `InstallAsync` | `override Task InstallAsync(PythonVirtualEnvironment env, PackageRequest request, CancellationToken ct)` | If `request.ProjectDirectory is null`, falls back entirely to `PipInstallAsync` (ad-hoc package installs don't go through Poetry). Otherwise resolves `poetry` via `Tools.EnsureAsync` and runs `poetry install [--only main] [...extra args]` with `workingDirectory = request.ProjectDirectory` and environment variables `POETRY_VIRTUALENVS_CREATE=false`, `VIRTUAL_ENV=<env.Directory>`, and `PATH` prefixed with the env's bin/Scripts directory — this combination makes Poetry install into the already-created environment instead of creating its own. Throws `PythonException(PackageOperationFailed)` on failure. |
| `UninstallAsync` | `override Task UninstallAsync(PythonVirtualEnvironment env, string package, CancellationToken ct)` | Delegates to `PipUninstallAsync` — Poetry has no separate uninstall path used here. |
| `ListAsync` | `override Task<IReadOnlyList<InstalledPackage>> ListAsync(PythonVirtualEnvironment env, CancellationToken ct)` | Delegates to `PipListAsync`. |
| `ProvisionAsync` | `private Task ProvisionAsync(ToolContext context, CancellationToken ct)` | Same unpinned/pinned split as `UvInstaller.ProvisionAsync`: `Version is null` → `ProvisionToolViaPipAsync(context, "poetry", ct)`; otherwise `ProvisionPinnedToolAsync(context, "poetry", Version, ct)`. |

## See also

- [Core/BuiltInInstaller.md](../Core/BuiltInInstaller.md) — `PipInstaller`/`PackageInstallerBase`'s pip helpers, reused directly here for the non-project path.
- [Core/Models.md](../Core/Models.md) — `PackageRequest.ProjectDirectory`.
