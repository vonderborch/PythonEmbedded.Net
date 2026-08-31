# Examples

Recipes for PythonEmbedded.Net 2.x. See [Quick-Reference.md](Quick-Reference.md) for the full API and [Getting-Started.md](Getting-Started.md) for the basics.

## Table of Contents

- [Basic Usage](#basic-usage)
- [Named Environments](#named-environments)
- [Package Management](#package-management)
- [Script, Code, and Module Execution](#script-code-and-module-execution)
- [Long-Lived Processes](#long-lived-processes)
- [Configuration](#configuration)
- [Offline / Bundled Runtimes](#offline--bundled-runtimes)
- [uv](#uv)
- [Conda](#conda)
- [Poetry](#poetry)
- [In-Process Execution (Python.NET)](#in-process-execution-pythonnet)
- [Building CPython from Source](#building-cpython-from-source)
- [Error Handling](#error-handling)
- [Dependency Injection](#dependency-injection)
- [Multiple Python Versions](#multiple-python-versions)

## Basic Usage

```csharp
using PythonEmbedded.Net;

var env = await PythonEnvironment.GetEnvironmentAsync("3.13", "myapp");
await env.Packages.InstallAsync("requests");
var result = await env.RunAsync("script.py");
Console.WriteLine(result.StandardOutput);
```

Synchronous callers use the twin methods instead:

```csharp
var env = PythonEnvironment.GetEnvironment("3.13", "myapp");
env.Packages.Install("requests");
var result = env.Run("script.py");
```

### Running Inline Code

```csharp
var result = await env.RunCodeAsync("import sys; print(sys.version)");
Console.WriteLine(result.StandardOutput);
```

### Running a Module

```csharp
await env.RunModuleAsync("http.server", ["8080"]);
```

## Named Environments

Every installation can have multiple named virtual environments. The name is always required — there is no default — so the same version can back several independent environments.

```csharp
var install = await PythonEnvironment.GetInstallationAsync("3.13");

var webEnv = await install.GetEnvironmentAsync("web");
var dataEnv = await install.GetEnvironmentAsync("data");

await webEnv.Packages.InstallAsync("flask");
await dataEnv.Packages.InstallAsync("pandas");
```

Or go straight from the facade — `PythonEnvironment.GetEnvironmentAsync` resolves the installation implicitly:

```csharp
var webEnv = await PythonEnvironment.GetEnvironmentAsync("3.13", "web");
```

Calling `GetEnvironmentAsync` again with the same version and name returns the existing environment; nothing is recreated.

## Package Management

```csharp
await env.Packages.InstallAsync("requests");
await env.Packages.InstallAsync("requests==2.31.0");
await env.Packages.InstallAsync("numpy>=1.20.0");

await env.Packages.InstallAsync(new PackageRequest
{
    Packages = ["numpy", "pandas"],
    IndexUrl = "https://my-pypi-mirror.com/simple/",
});

await env.Packages.InstallAsync(new PackageRequest
{
    RequirementsFile = "requirements.txt",
});

await env.Packages.UninstallAsync("requests");

var installed = await env.Packages.ListAsync();
foreach (var pkg in installed)
{
    Console.WriteLine($"{pkg.Name}: {pkg.Version}");
}
```

## Script, Code, and Module Execution

```csharp
// A script file, with args and options
var result = await env.RunAsync("job.py", ["--flag", "value"], new RunOptions
{
    WorkingDirectory = "/data",
    Environment = new Dictionary<string, string> { ["MODE"] = "prod" },
    Timeout = TimeSpan.FromMinutes(5),
});

// Inline code
await env.RunCodeAsync("print('hello')");

// python -m
await env.RunModuleAsync("http.server", ["8080"]);
```

A nonzero exit throws `PythonProcessException` by default. Pass `ThrowOnError = false` to get a `PythonResult` back instead:

```csharp
var result = await env.RunAsync("might-fail.py", options: new RunOptions { ThrowOnError = false });
if (!result.Success)
{
    Console.WriteLine($"Exit {result.ExitCode}: {result.StandardError}");
}
```

### Providing stdin

```csharp
var result = await env.RunCodeAsync(
    "import sys; print(sys.stdin.read().upper())",
    new RunOptions { Stdin = "hello from .net" });
```

## Long-Lived Processes

`Start` returns a live `PythonProcess` for servers, workers, or anything that needs streamed I/O.

```csharp
await using var server = env.Start("server.py", ["--port", "8080"]);

server.OutputLine += line => Console.WriteLine($"[py] {line}");
server.ErrorLine += line => Console.WriteLine($"[py:err] {line}");

await server.StandardInput.WriteLineAsync("ping");

// Disposing kills the process if it's still running.
```

To wait for a natural exit instead:

```csharp
var exitCode = await server.WaitForExitAsync();
```

## Configuration

```csharp
PythonEnvironment.Configure(o =>
{
    o.RootDirectory = @"C:\MyApp\python";           // default: app-local data folder
    o.Offline = true;                                // never touch the network
    o.GitHubToken = "...";                            // defaults to GITHUB_TOKEN env var
    o.Logger = loggerFactory.CreateLogger("Python");
    o.AddDirectorySource(@"D:\python-archives");      // your own archive directory, highest priority
    o.LockTimeout = TimeSpan.FromMinutes(2);
});
```

`Configure` must run before the first `PythonEnvironment.Get*` call; calling it afterward throws.

## Offline / Bundled Runtimes

Reference a runtime package and `GetEnvironmentAsync` needs no network at all, even with `o.Offline = true`:

```bash
dotnet add package PythonEmbedded.Net.Runtime.Python313
```

```csharp
PythonEnvironment.Configure(o => o.Offline = true);
var env = await PythonEnvironment.GetEnvironmentAsync("3.13", "myapp");   // resolves entirely from the bundled archive
```

## uv

```csharp
PythonEnvironment.Configure(o => o.Installer = new UvInstaller { Seed = true });

var env = await PythonEnvironment.GetEnvironmentAsync("3.13", "myapp");
await env.Packages.InstallAsync("requests");   // now backed by uv, ~10x faster than pip
```

uv is provisioned automatically into the base interpreter on first use — no separate install step. Pin a specific uv release instead of always grabbing latest with `new UvInstaller { Version = "0.5.11" }`; pinned versions are provisioned into their own private location so they can coexist with other requested versions.

## Conda

```csharp
PythonEnvironment.Configure(o => o.Installer = new CondaInstaller { Channels = ["conda-forge"] });

var env = await PythonEnvironment.GetEnvironmentAsync("3.13", "geo");
await env.Packages.InstallAsync("gdal");   // real conda env via a self-provisioned micromamba
```

Conda's underlying micromamba binary is pinned with `MicromambaVersion` (e.g. `"2.1.1-0"`); it defaults to `"latest"`.

### From an `environment.yml`

```csharp
await env.Packages.InstallAsync(new PackageRequest { ProjectDirectory = "./my-project" });
```

## Poetry

```csharp
PythonEnvironment.Configure(o => o.Installer = new PoetryInstaller { WithDevDependencies = false });

var env = await PythonEnvironment.GetEnvironmentAsync("3.13", "myapp");
await env.Packages.InstallAsync(new PackageRequest { ProjectDirectory = "./my-poetry-project" });
```

Ad-hoc single-package installs on a poetry-managed environment fall back to plain pip. Pin a specific Poetry release with `new PoetryInstaller { Version = "1.8.3" }`.

## In-Process Execution (Python.NET)

```csharp
using PythonEmbedded.Net.Runners.PythonNet;

PythonEnvironment.Configure(o => o.Runner = new InProcessRunner());

var env = await PythonEnvironment.GetEnvironmentAsync("3.13", "myapp");
PythonNetHost.Initialize(env);   // one engine per process; call once

var result = await env.RunCodeAsync("print(2 + 2)");   // runs in-process, no subprocess overhead

PythonNetHost.RunInScope(scope =>
{
    // direct interop with the Python.NET scope
});
```

## Building CPython from Source

`SourceBuildSource` downloads an official tarball from python.org and compiles it. Insert it **first**
so it wins over the prebuilt sources, and show progress — the first build of a version takes 15–40
minutes with the default PGO+LTO profile:

```csharp
using PythonEmbedded.Net.Sources.SourceBuild;

PythonEnvironment.Configure(o => o.Sources.Insert(0, new SourceBuildSource()));

var env = await PythonEnvironment.GetEnvironmentAsync(
    "3.13", "myapp",
    progress: new Progress<InstallProgress>(p => Console.WriteLine($"{p.Phase} {p.Detail}")));
```

Once built, the interpreter is cached like any other installation; later calls return in milliseconds.
For iteration, turn the optimizations off — minutes instead of tens of minutes:

```csharp
new SourceBuildSource { Optimize = false, Lto = false }
```

### Build variants

Installs are keyed by version *and* source name, so **every differently-configured instance needs its own
`Name`** — otherwise the first variant built wins and the second is silently never compiled.
`FreeThreaded` handles this for you; anything else does not:

```csharp
PythonEnvironment.Configure(o =>
{
    o.Sources.Insert(0, new SourceBuildSource());                        // "source-build"
    o.Sources.Insert(0, new SourceBuildSource { FreeThreaded = true });  // "source-build-ft" (3.13+)
    o.Sources.Insert(0, new SourceBuildSource
    {
        Name = "py-debug",                                               // required: a distinct install
        ConfigureArguments = ["--with-pydebug"],
        Optimize = false,
    });
});
```

### Build dependencies

By default the source detects what is missing and throws with the command that installs it, rather than
installing system software behind your back. Opt in to have it handled:

```csharp
new SourceBuildSource
{
    ProvisionDependencies = true,   // brew on macOS; a conda-forge prefix under <root>/tools/ on Linux
    AllowElevation = true,          // additionally allow sudo / the winget UAC prompt
}
```

macOS always needs the Xcode Command Line Tools (`xcode-select --install`) — that one cannot be
automated. Windows needs the Visual Studio C++ build tools, and its build downloads external
dependencies during `build.bat`, so it never works offline.

## Error Handling

```csharp
try
{
    await env.RunAsync("flaky.py");
}
catch (PythonProcessException ex)
{
    Console.WriteLine($"Exit {ex.Result.ExitCode}: {ex.Result.StandardError}");
}
catch (PythonException ex)
{
    Console.WriteLine($"{ex.Kind}: {ex.Message}");
}
```

See [Error-Handling.md](Error-Handling.md) for the full exception model.

## Dependency Injection

`PythonEnvironment` is a static facade, so there is nothing to register — call `PythonEnvironment.Configure` once at startup (e.g. in `Program.cs`) and use `PythonEnvironment.GetEnvironmentAsync` from anywhere:

```csharp
public class MyService
{
    public async Task<string> RunAsync(string code)
    {
        var env = await PythonEnvironment.GetEnvironmentAsync("3.13", "myapp");
        var result = await env.RunCodeAsync(code);
        return result.StandardOutput;
    }
}
```

If you prefer an injectable seam for testing, wrap the calls you need behind your own small interface and have `MyService` depend on that instead.

## Multiple Python Versions

```csharp
var py312 = await PythonEnvironment.GetEnvironmentAsync("3.12", "myapp");
var py313 = await PythonEnvironment.GetEnvironmentAsync("3.13", "myapp");

var r312 = await py312.RunCodeAsync("print('3.12')");
var r313 = await py313.RunCodeAsync("print('3.13')");
```

Version strings accept `"latest"`, `"3"`, `"3.13"`, `"3.13.2"`, or a pre-release like `"3.15.0b3"`. Partial versions resolve to the newest matching build.

```csharp
var installs = await PythonEnvironment.ListInstallationsAsync();
foreach (var install in installs)
{
    Console.WriteLine($"{install.Version} at {install.Directory}");
}

await PythonEnvironment.RemoveAsync(installs[0]);   // deletes the install and all its environments
```

## See Also

- [Getting Started](Getting-Started.md)
- [Quick Reference](Quick-Reference.md)
- [Architecture](Architecture.md)
- [Error Handling](Error-Handling.md)
