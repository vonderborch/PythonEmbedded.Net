# API Reference

In-depth, per-file reference covering every type and member in the codebase. For narrative/tutorial docs, see [the main index](../README.md); for a dense machine-oriented summary, see [AI-Reference.md](../AI-Reference.md).

## Core (`PythonEmbedded.Net`)

| Page | Files covered |
| --- | --- |
| [Facade](Core/Facade.md) | `PythonEnvironment.cs`, `PythonOptions.cs` |
| [Handles](Core/Handles.md) | `PythonInstallation.cs`, `PythonVirtualEnvironment.cs`, `PackageManager.cs`, `PythonProcess.cs` |
| [Models](Core/Models.md) | Everything under `Models/` |
| [Exceptions](Core/Exceptions.md) | Everything under `Exceptions/` |
| [Extensibility](Core/Extensibility.md) | Everything under `Extensibility/` — the three interfaces, their base classes, `SourceContext`, `Subprocess`, `Tools`, `ToolContext` |
| [Internals](Core/Internals.md) | Everything under `Internals/` — `PythonHost`, `DiskLock`, `SysconfigPatcher`, `ArchiveExtractor`, `ArchiveName` |
| [Built-in sources](Core/BuiltInSources.md) | `Sources/DirectorySource.cs`, `Sources/AstralSource.cs` |
| [Built-in installer](Core/BuiltInInstaller.md) | `PackageManagers/PipInstaller.cs` |
| [Built-in runner](Core/BuiltInRunner.md) | `Runners/ProcessRunner.cs` |

## Satellite packages

| Page | Package | Files covered |
| --- | --- | --- |
| [Uv](Satellites/Uv.md) | `PythonEmbedded.Net.PackageManagers.Uv` | `UvInstaller.cs` |
| [Conda](Satellites/Conda.md) | `PythonEmbedded.Net.PackageManagers.Conda` | `CondaInstaller.cs` |
| [Poetry](Satellites/Poetry.md) | `PythonEmbedded.Net.PackageManagers.Poetry` | `PoetryInstaller.cs` |
| [PythonNet](Satellites/PythonNet.md) | `PythonEmbedded.Net.Runners.PythonNet` | `InProcessRunner.cs`, `PythonNetHost.cs` |

Runtime packages (`PythonEmbedded.Net.Runtime.Python311`–`Python315`) contain no code — they're NuGet content packages that drop a bundled archive under `python-embedded-runtimes/` at build time, picked up by the built-in `DirectorySource` (see [Built-in sources](Core/BuiltInSources.md)). There's nothing to document beyond that mechanism.

## Conventions used across these pages

- **One type per file** is enforced throughout the codebase; each `.cs` file name matches its single top-level type.
- **Sync + async twins**: every public async method (`XxxAsync(..., CancellationToken)`) has a sync wrapper (`Xxx(...)`) that's a thin `.GetAwaiter().GetResult()` call. To keep these pages readable, twins are documented once under the async signature rather than twice.
- **Namespace-per-folder**: a type's namespace is `PythonEmbedded.Net.<FolderPath>` (e.g. `Models/PythonVersion.cs` → `PythonEmbedded.Net.Models`), except types directly under `source/PythonEmbedded.Net/` which are `PythonEmbedded.Net`.
