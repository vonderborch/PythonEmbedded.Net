# Getting Started

PythonEmbedded.Net gives your .NET app its own managed Python. Targets **.NET 9** and **.NET 10**; runs on Windows, macOS, and Linux (x64/arm64).

## Install

```bash
dotnet add package PythonEmbedded.Net
```

## The happy path

```csharp
using PythonEmbedded.Net;

var env = await PythonEnvironment.GetEnvironmentAsync("3.13", "myapp");
await env.Packages.InstallAsync("requests");
var result = await env.RunAsync("script.py");
Console.Write(result.StandardOutput);
```

On first use this downloads the requested Python from [python-build-standalone](https://github.com/astral-sh/python-build-standalone) (checksum-verified, cached on disk) and creates a virtual environment named `myapp`. Afterwards the same call returns in milliseconds with no network access.

Version strings accept `"latest"`, `"3"`, `"3.13"`, or `"3.13.2"`. Partial versions resolve to the newest matching build. The environment name is always required — there is no default — so the same Python version can back multiple independent named environments.

Prefer synchronous code? Every method has a sync twin: `PythonEnvironment.GetEnvironment("3.13", "myapp")`, `env.Run(...)`, `env.Packages.Install(...)`.

## Running code

```csharp
await env.RunAsync("script.py", ["--flag", "value"]);   // a script file
await env.RunCodeAsync("print('hello')");               // inline code
await env.RunModuleAsync("http.server", ["8080"]);      // python -m

// Options: working dir, env vars, timeout, stdin
await env.RunAsync("job.py", options: new RunOptions
{
    WorkingDirectory = "/data",
    Timeout = TimeSpan.FromMinutes(5),
    Environment = new Dictionary<string, string> { ["MODE"] = "prod" },
});
```

A nonzero exit code throws `PythonProcessException` (with the full output attached). To inspect results instead, pass `new RunOptions { ThrowOnError = false }` and check `result.ExitCode`.

For long-lived processes (servers, workers) use `Start`:

```csharp
await using var server = env.Start("server.py", ["--port", "8080"]);
server.OutputLine += line => Console.WriteLine(line);
await server.StandardInput.WriteLineAsync("command");
// Disposing kills the process if it is still running.
```

## Configuration (optional)

Call `PythonEnvironment.Configure` once, before anything else:

```csharp
PythonEnvironment.Configure(o =>
{
    o.RootDirectory = @"C:\MyApp\python";  // default: app-local data folder
    o.Offline = true;                      // never touch the network
    o.GitHubToken = "...";                 // defaults to GITHUB_TOKEN env var
    o.Logger = loggerFactory.CreateLogger("Python");
    o.AddDirectorySource(@"D:\python-archives");  // your own archive directory
});
```

## Going offline

Reference a runtime package and your app needs no network at all:

```bash
dotnet add package PythonEmbedded.Net.Runtime.Python313
```

The package bundles the official archives; at build time the one matching your platform is copied next to your app, where the library finds it automatically. `PythonEnvironment.GetEnvironmentAsync("3.13", "myapp")` then works with zero configuration, even with `o.Offline = true`.

## Faster installs, conda, poetry, in-process

Each is a one-line opt-in from a satellite package — see [Examples.md](Examples.md):

```csharp
PythonEnvironment.Configure(o => o.Installer = new UvInstaller());      // PythonEmbedded.Net.PackageManagers.Uv
PythonEnvironment.Configure(o => o.Installer = new CondaInstaller());   // PythonEmbedded.Net.PackageManagers.Conda
PythonEnvironment.Configure(o => o.Installer = new PoetryInstaller());  // PythonEmbedded.Net.PackageManagers.Poetry
PythonEnvironment.Configure(o => o.Runner = new InProcessRunner());     // PythonEmbedded.Net.Runners.PythonNet
```

Each package-manager installer accepts a `Version` (uv/poetry) or `MicromambaVersion` (conda) option to pin the underlying tool to a specific release instead of always grabbing the latest — e.g. `new UvInstaller { Version = "0.5.11" }`.
