# Contributing to PythonEmbedded.Net

Thank you for your interest in contributing! This document covers the repo layout, coding standards, and how to build/test the 2.x codebase.

## Table of Contents

- [Code of Conduct](#code-of-conduct)
- [Getting Started](#getting-started)
- [Development Setup](#development-setup)
- [Coding Standards](#coding-standards)
- [Testing](#testing)
- [Submitting Changes](#submitting-changes)
- [Documentation](#documentation)
- [Architecture Guidelines](#architecture-guidelines)

## Code of Conduct

By participating in this project, you agree to maintain a respectful and inclusive environment for all contributors.

## Getting Started

1. **Fork the repository** on GitHub
2. **Clone your fork** locally
3. **Create a branch** for your changes
4. **Make your changes**
5. **Test your changes**
6. **Submit a pull request**

## Development Setup

### Prerequisites

- .NET 9.0 and .NET 10.0 SDKs
- Visual Studio, Rider, or VS Code with the C# extension
- Git

### Building the Project

```bash
git clone https://github.com/vonderborch/PythonEmbedded.Net.git
cd PythonEmbedded.Net

dotnet restore
dotnet build
dotnet test
```

### Project Structure

```
PythonEmbedded.Net/
├── source/
│   ├── PythonEmbedded.Net/                       # core library — functional alone
│   │   └── Internals/                            # PythonHost, DiskLock, sources, default installer/runner
│   ├── PackageManagers/                          # IPackageInstaller satellites
│   │   ├── PythonEmbedded.Net.PackageManagers.Uv/
│   │   ├── PythonEmbedded.Net.PackageManagers.Conda/
│   │   └── PythonEmbedded.Net.PackageManagers.Poetry/
│   ├── Runners/                                  # IPythonRunner satellites
│   │   └── PythonEmbedded.Net.Runners.PythonNet/
│   └── Runtimes/                                 # offline runtime packages
│       ├── manifest.json                         # committed: astral tag + asset URLs + sha256
│       ├── tools/update-manifest.py               # refreshes manifest.json from astral releases
│       ├── PythonEmbedded.Net.Runtime.Template/   # shared .props/.targets
│       └── PythonEmbedded.Net.Runtime.Python3xx/  # one project per minor version
├── test/
│   ├── automated/
│   │   ├── PythonEmbedded.Net.Test/               # unit tests, offline, NUnit
│   │   └── PythonEmbedded.Net.IntegrationTest/    # real archives; [Category("RequiresNetwork")] tagging
│   ├── manual/PythonEmbedded.Net.DevTest/         # console playground
│   ├── tools/fetch-fixtures.sh                    # downloads real archives for local integration tests
│   └── fixtures/                                  # gitignored; filled by fetch-fixtures.sh
└── Docs/                                          # documentation
```

The category is visible in the folder and the package name itself — `PackageManagers.*` implement `IPackageInstaller`, `Runners.*` implement `IPythonRunner`. There is no other place package managers or runners live.

## Coding Standards

This project stays deliberately small: one core assembly, one namespace, a handful of public types, no reflection, no registries, no two-phase construction. New satellites are the only expected growth path — new capability is a new class implementing `IPythonSource`, `IPackageInstaller`, or `IPythonRunner`, not a new layer in core.

### C# Style

- File-scoped namespaces: `namespace PythonEmbedded.Net;`
- Records for value objects (`PythonVersion`, `RunOptions`, `PythonResult`, `PackageRequest`, ...)
- Sealed classes for handles and engines unless there's a concrete reason to allow inheritance
- Nullable reference types enabled
- `ConfigureAwait(false)` throughout library code
- **Every public async method has a synchronous twin**: the async method is the implementation; the sync twin is a thin `GetAwaiter().GetResult()` wrapper. Add both when adding a new public method.

### Naming Conventions

- Classes/records/interfaces: PascalCase, interfaces prefixed with `I` (`IPythonSource`)
- Methods: PascalCase, async methods suffixed `Async` (`RunAsync`), sync twin without the suffix (`Run`)
- Parameters: camelCase
- Private fields: camelCase with `_` prefix

### Code Organization

- One type per file, file name matches the type
- `internal` for everything that isn't part of the public interface surface — see the "Public API surface" list in the design plan for what's meant to stay public
- Keep methods focused; prefer a few extra small internal helpers over one large method, but don't add abstraction layers for a single call site

### Documentation

- XML doc comments on public APIs
- Comments only where the *why* isn't obvious from the code — no comments restating what a line does

## Testing

### Test Structure

- **Unit tests** (`PythonEmbedded.Net.Test`): fully offline, use fakes (`FakeSource`, `FakeInstaller`, `FakeRunner`) and a temp root per test — no real Python, no network.
- **Integration tests** (`PythonEmbedded.Net.IntegrationTest`): run against real interpreters. Local fixture archives cover install→venv→run without network; tests tagged `[Category("RequiresNetwork")]` hit the real astral API and are skipped unless explicitly included.

### Running Tests

```bash
# All unit tests (offline, fast)
dotnet test test/automated/PythonEmbedded.Net.Test

# Integration tests — fetch fixtures first
./test/tools/fetch-fixtures.sh
dotnet test test/automated/PythonEmbedded.Net.IntegrationTest --filter "Category!=RequiresNetwork"

# Everything, including real network calls
dotnet test test/automated/PythonEmbedded.Net.IntegrationTest
```

### Writing Tests

```csharp
[TestFixture]
public class PythonHostTests
{
    [Test]
    public async Task GetEnvironmentAsync_Reuses_Existing_Install()
    {
        using var root = new TempRoot();
        var host = new PythonHost(new PythonOptions
        {
            RootDirectory = root.Path,
            Sources = [new FakeSource()],
        });

        var env1 = await host.GetEnvironmentAsync("3.13", "default");
        var env2 = await host.GetEnvironmentAsync("3.13", "default");

        Assert.That(env2.Directory, Is.EqualTo(env1.Directory));
    }
}
```

`InternalsVisibleTo` exposes `PythonHost` and other internals to both test projects, so tests construct the engine directly rather than going through the static `PythonEnvironment` facade (which is covered separately by facade-level smoke tests).

## Submitting Changes

### Before Submitting

1. `dotnet build` — zero warnings
2. `dotnet test` on the unit suite (and integration suite where relevant)
3. Update docs in `Docs/` if the public API changed
4. Keep the change focused — one logical change per PR

### Pull Request Process

1. Descriptive title
2. Description covering what changed, why, and how to test it
3. Reference related issues

### Commit Messages

Conventional commits:

```
type(scope): subject

body (optional)

footer (optional)
```

Types: `feat`, `fix`, `docs`, `refactor`, `test`, `chore`.

Example:

```
feat(packagemanagers): add PixiInstaller satellite

Implements IPackageInstaller against pixi; self-provisions the pixi
binary the same way UvInstaller provisions uv.
```

## Documentation

Update the relevant file in `Docs/` alongside any public API change:

- **Quick-Reference.md** — API surface changes
- **Examples.md** — new usage patterns
- **Architecture.md** — design/interface changes
- **Error-Handling.md** — new `PythonErrorKind` values
- **Troubleshooting.md** — new failure modes worth documenting

Keep language concise, favor runnable code snippets, and cross-reference related docs at the bottom of the file.

## Architecture Guidelines

### Design Principles

- Three interfaces (`IPythonSource`, `IPackageInstaller`, `IPythonRunner`) cover every extensibility axis; there is no fourth without a strong reason
- No reflection-based discovery — satellites are wired explicitly via `PythonEnvironment.Configure`
- The filesystem is the index — no global metadata file; a marker file (`install.json`/`env.json`) written last is what makes a directory "real"
- Two exception types only — new failure modes get a new `PythonErrorKind`, not a new exception class

### Adding a New Satellite

1. New project under `source/PackageManagers/` or `source/Runners/`, named `PythonEmbedded.Net.PackageManagers.<Name>` or `PythonEmbedded.Net.Runners.<Name>`
2. Implement the relevant interface; use `Tools.EnsureAsync` for any external binary the satellite needs
3. Add it to `PythonEmbedded.Net.slnx` under the matching solution folder
4. Cover it with integration tests under `PythonEmbedded.Net.IntegrationTest`
5. Document it in `Examples.md` and the satellites table in `README.md` / `Quick-Reference.md`

### Adding a New Runtime Package

Runtime packages are generated, not hand-written — see `source/Runtimes/tools/update-manifest.py` and `.github/workflows/refresh-runtimes.yml`. Add a new minor version by adding a `PythonEmbedded.Net.Runtime.Python3xx/` project following the existing ones and letting the manifest refresh pick it up.

## Review Process

- All PRs require review
- Tests must pass before merge
- Keep discussion constructive and focused on the change

## Questions?

Check existing documentation and issues first; open a new issue or discussion if it's not covered.

Thank you for contributing to PythonEmbedded.Net!
