# Conda (`PythonEmbedded.Net.PackageManagers.Conda`)

Namespace: `PythonEmbedded.Net.PackageManagers.Conda`. Activate with:

```csharp
PythonEnvironment.Configure(o => o.Installer = new CondaInstaller());
```

## `CondaInstaller.cs`

`public sealed class CondaInstaller : PackageInstallerBase` — an `IPackageInstaller` for the conda ecosystem, driven by [micromamba](https://mamba.readthedocs.io/) — a single static binary downloaded runtime-locally on first use (real conda/mamba installs are never touched). Environments are conda environments (self-contained, python from conda-forge pinned to the installation's major.minor), so conda-only packages (CUDA toolkits, geospatial stacks, ...) work.

| Member | Type / signature | Notes |
| --- | --- | --- |
| `Name` | `override string` → `"conda"` | Recorded in `env.json`. |
| `Channels` | `string[] { get; init; } = ["conda-forge"]` | Channels searched, in priority order; each becomes a `--channel <c>` arg. |
| `MicromambaVersion` | `string { get; init; } = "latest"` | The micromamba release to provision, e.g. `"2.1.1-0"`. `"latest"` downloads from the `latest/download/` release alias; a pinned value downloads from `download/<version>/` and is provisioned into a version-specific tools subdirectory. |
| `CreateEnvironmentAsync` | `override Task CreateEnvironmentAsync(PythonInstallation install, string envDirectory, CancellationToken ct)` | `micromamba create --yes --prefix <envDirectory> [--channel ...] python=<major>.<minor>` (pinned to the base installation's major.minor). Throws `PythonException(EnvironmentFailed)` on failure. |
| `InstallAsync` | `override Task InstallAsync(PythonVirtualEnvironment env, PackageRequest request, CancellationToken ct)` | `micromamba install --yes --prefix <env-dir> [--channel ...] [--file <environment.yml or requirements file>] [...extra args] [...packages]`. When `request.ProjectDirectory` is set, requires an `environment.yml` inside it — throws `PythonException(PackageOperationFailed)` if missing. `RequirementsFile` (if also given) is passed as an additional `--file`. |
| `UninstallAsync` | `override Task UninstallAsync(PythonVirtualEnvironment env, string package, CancellationToken ct)` | `micromamba remove --yes --prefix <env-dir> <package>`. |
| `ListAsync` | `override Task<IReadOnlyList<InstalledPackage>> ListAsync(PythonVirtualEnvironment env, CancellationToken ct)` | `micromamba list --prefix <env-dir> --json`, deserialized into a private `CondaPackage(string Name, string Version)` record array and mapped to `InstalledPackage`. |
| `AddChannels` | `private void AddChannels(List<string> args)` | Appends `--channel <c>` for each entry in `Channels`. |
| `RunMicromambaOrThrowAsync` | `private Task<PythonResult> RunMicromambaOrThrowAsync(PythonInstallation install, List<string> args, PythonErrorKind kind, string what, CancellationToken ct)` | Resolves the micromamba binary via `Tools.EnsureAsync` (version = `null` when `MicromambaVersion == "latest"`, else the pinned value), sets `MAMBA_ROOT_PREFIX` to a `mamba-root` directory next to the binary (keeping micromamba's package cache/state runtime-local), then runs via `RunOrThrowAsync`. |
| `ProvisionAsync` | `private Task ProvisionAsync(ToolContext context, CancellationToken ct)` | Selects the release asset name by OS/architecture (`micromamba-{win-64,win-arm64,osx-64,osx-arm64,linux-64,linux-aarch64}[.exe]`; throws `PythonException(UnsupportedPlatform)` for anything else), downloads it via `context.Sources.DownloadAsync` (no checksum verification — micromamba releases don't publish one used here), copies it into `<toolsDirectory>/micromamba[-<version>]/micromamba[.exe]`, and (POSIX only) sets executable file-mode bits via `File.SetUnixFileMode`. |
| `CondaPackage` | `private sealed record CondaPackage(string Name, string Version)` | Deserialization target for `micromamba list --json` entries (`name`/`version` JSON properties via `[JsonPropertyName]`). |

## See also

- [Core/Extensibility.md](../Core/Extensibility.md) — `PackageInstallerBase`, `Tools.EnsureAsync`, `ToolContext`, `SourceContext.DownloadAsync`.
- [Core/Models.md](../Core/Models.md) — `PackageRequest.ProjectDirectory`, honored here for `environment.yml`-based installs.
