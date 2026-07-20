# Quick Reference

The complete public API of PythonEmbedded.Net 2.x. Every `*Async` method has a synchronous twin without the suffix.

## Entry point

```csharp
PythonEnvironment.Configure(Action<PythonOptions>);                   // once, before first use
PythonEnvironment.GetEnvironmentAsync(version, name);                 // → PythonVirtualEnvironment; name is required
PythonEnvironment.GetInstallationAsync(version);                      // → PythonInstallation
PythonEnvironment.ListInstallationsAsync();                           // → IReadOnlyList<PythonInstallation>
PythonEnvironment.RemoveAsync(installation);                          // delete install + its envs
```

Version strings: `"latest"` | `"3"` | `"3.13"` | `"3.13.2"` | `"3.15.0b3"`.

## PythonOptions

| Member | Default |
| --- | --- |
| `RootDirectory` | app-local data folder (entry assembly name) |
| `Sources` (`IList<IPythonSource>`) | bundled runtime packages, then astral download |
| `Installer` (`IPackageInstaller`) | pip + venv |
| `Runner` (`IPythonRunner`) | buffered subprocess |
| `GitHubToken` | `GITHUB_TOKEN` env var |
| `ReleaseCacheTtl` | 24 h |
| `Offline` | false |
| `Logger` / `HttpClient` | null |
| `LockTimeout` | 10 min |
| `AddDirectorySource(path)` | adds a local archive directory as highest-priority source |

## PythonVirtualEnvironment

```csharp
env.Installation; env.Name; env.Directory; env.PythonExecutable; env.IsBase;

env.RunAsync(scriptPath, args?, options?);      // → PythonResult, throws on nonzero exit
env.RunCodeAsync(code, options?);
env.RunModuleAsync(module, args?, options?);
env.Start(scriptPath, args?, options?);         // → PythonProcess (live handle)

env.Packages.InstallAsync("requests");          // or "requests==2.31"
env.Packages.InstallAsync(new PackageRequest { ... });
env.Packages.UninstallAsync("requests");
env.Packages.ListAsync();                       // → IReadOnlyList<InstalledPackage>
```

## RunOptions / PackageRequest

```csharp
new RunOptions
{
    WorkingDirectory, Environment,      // dict of extra env vars
    Timeout,                            // kills + throws PythonProcessException(Timeout)
    Stdin,
    ThrowOnError = true,                // false → always returns PythonResult
};

new PackageRequest
{
    Packages,             // ["requests", "flask==3.0"]
    RequirementsFile,     // "-r requirements.txt"
    IndexUrl,
    ExtraArgs,
    ProjectDirectory,     // pyproject.toml project (poetry/uv); environment.yml (conda)
};
```

## PythonResult / PythonProcess

```csharp
result.ExitCode; result.StandardOutput; result.StandardError; result.Duration;
result.Success; result.EnsureSuccess();

process.OutputLine += line => ...;   // subscribe before output arrives
process.ErrorLine += line => ...;
process.StandardInput; process.Id; process.HasExited;
await process.WaitForExitAsync();    // → exit code
process.Kill();
await process.DisposeAsync();        // kills if still running
```

## Exceptions

```csharp
PythonException { Kind }             // VersionNotFound, DownloadFailed, InstallFailed,
                                     // EnvironmentFailed, PackageOperationFailed, ToolMissing,
                                     // Locked, Offline, ExecutionFailed, Timeout, UnsupportedPlatform
PythonProcessException { Result }    // : PythonException — nonzero exit / timeout, carries output
```

## The three interfaces

```csharp
interface IPythonSource     { Name; TryInstallAsync(request, targetDir, SourceContext, ct); }
interface IPackageInstaller { Name; CreateEnvironmentAsync; InstallAsync; UninstallAsync; ListAsync; }
interface IPythonRunner     { RunAsync(env, invocation, ct); }

Subprocess.RunAsync(exe, args, ...);                    // process plumbing for extension authors
Tools.EnsureAsync(install, "uv", provisionCallback);    // runtime-local tool provisioning
```

## Satellites

```csharp
o.Installer = new UvInstaller { Seed = true, Version = null };                        // .PackageManagers.Uv
o.Installer = new CondaInstaller { Channels = ["conda-forge"], MicromambaVersion = "latest" }; // .PackageManagers.Conda
o.Installer = new PoetryInstaller { WithDevDependencies = false, Version = null };     // .PackageManagers.Poetry
o.Runner = new InProcessRunner();                                                      // .Runners.PythonNet
PythonNetHost.Initialize(env); PythonNetHost.RunInScope(scope => ...);
```

`Version` / `MicromambaVersion` pin the provisioned tool to a specific release (e.g. `"0.5.11"`, `"1.8.3"`, `"2.1.1-0"`); `null` / `"latest"` (default) always grabs the newest. Pinned versions are provisioned into their own private location so different requested versions can coexist.

Tool resolution override: set `PYEMBED_TOOL_UV` / `PYEMBED_TOOL_POETRY` / `PYEMBED_TOOL_MICROMAMBA` to a binary path.
