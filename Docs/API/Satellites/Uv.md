# Uv (`PythonEmbedded.Net.PackageManagers.Uv`)

Namespace: `PythonEmbedded.Net.PackageManagers.Uv`. Activate with:

```csharp
PythonEnvironment.Configure(o => o.Installer = new UvInstaller());
```

## `UvInstaller.cs`

`public sealed class UvInstaller : PackageInstallerBase` — an `IPackageInstaller` backed by [uv](https://github.com/astral-sh/uv): `uv venv` for environment creation and `uv pip` for package operations, typically an order of magnitude faster than pip. uv itself is provisioned runtime-locally (pip-installed into the base interpreter, or into a private pinned-version venv) on first use via `Tools.EnsureAsync`; system installs of uv are never used.

| Member | Type / signature | Notes |
| --- | --- | --- |
| `Name` | `override string` → `"uv"` | Recorded in `env.json`; environments created with `UvInstaller` can only be reopened with an installer also named `"uv"`. |
| `Seed` | `bool { get; init; } = true` | Passes `--seed` to `uv venv`, seeding new environments with pip so tools expecting `python -m pip` keep working. |
| `Version` | `string? { get; init; } = null` | The uv version to provision, e.g. `"0.5.11"`. `null` (default) means "latest", installed into the base interpreter; a pinned value is installed into a private venv under the tools directory instead, keyed by version. |
| `CreateEnvironmentAsync` | `override Task CreateEnvironmentAsync(PythonInstallation install, string envDirectory, CancellationToken ct)` | Resolves `uv` via `EnsureUvAsync`, then runs `uv venv --python <base-interpreter> [--seed] <envDirectory>`, throwing `PythonException(EnvironmentFailed)` on failure. |
| `InstallAsync` | `override Task InstallAsync(PythonVirtualEnvironment env, PackageRequest request, CancellationToken ct)` | `uv pip install --python <env-interpreter> [--index-url ...] [-r <requirements>] [...extra args] [...packages]`, throwing `PythonException(PackageOperationFailed)` on failure. |
| `UninstallAsync` | `override Task UninstallAsync(PythonVirtualEnvironment env, string package, CancellationToken ct)` | `uv pip uninstall --python <env-interpreter> <package>`. |
| `ListAsync` | `override Task<IReadOnlyList<InstalledPackage>> ListAsync(PythonVirtualEnvironment env, CancellationToken ct)` | `uv pip list --python <env-interpreter> --format json`, parsed via the shared `JsonOptions` from `PackageInstallerBase`. |
| `EnsureUvAsync` | `private Task<string> EnsureUvAsync(PythonInstallation install, CancellationToken ct)` | `Tools.EnsureAsync(install, "uv", ProvisionAsync, ct, version: Version)`. |
| `ProvisionAsync` | `private Task ProvisionAsync(ToolContext context, CancellationToken ct)` | `Version is null` → `ProvisionToolViaPipAsync(context, "uv", ct)` (pip-install into the base interpreter); otherwise `ProvisionPinnedToolAsync(context, "uv", Version, ct)` (private pinned venv). Both are shared helpers from `PackageInstallerBase` (see [Extensibility.md](../Core/Extensibility.md)). |

Note: uv doesn't copy `uv` itself into venvs it creates — `UvInstaller` always resolves `uv` from the base interpreter's tool location, not the venv, which is transparent to callers of this library.

## See also

- [Core/Extensibility.md](../Core/Extensibility.md) — `PackageInstallerBase`, `Tools.EnsureAsync`, `ToolContext`.
- [Core/BuiltInInstaller.md](../Core/BuiltInInstaller.md) — `PipInstaller`, the default this replaces.
- [../../Troubleshooting.md](../../Troubleshooting.md#uv-created-venvs-and-tool-resolution) — the uv-tool-resolution note from a user's perspective.
