# PythonEmbedded.Net

Embedded Python for .NET that feels like magic.

![Logo](https://raw.githubusercontent.com/vonderborch/PythonEmbedded.Net/refs/heads/main/logo.png)

[![NuGet version (PythonEmbedded.Net)](https://img.shields.io/nuget/v/PythonEmbedded.Net.svg?style=flat-square)](https://www.nuget.org/packages/PythonEmbedded.Net/)

Give your .NET app its own Python — downloaded, isolated, and managed automatically. No system Python, no PATH surgery, no "please install Python 3.x first" in your README.

```csharp
using PythonEmbedded.Net;

var env = await PythonEnvironment.GetEnvironmentAsync("3.13", "myapp");
await env.Packages.InstallAsync("requests");
var result = await env.RunAsync("script.py");
Console.Write(result.StandardOutput);
```

That first line downloads a standalone CPython from [python-build-standalone](https://github.com/astral-sh/python-build-standalone) (checksum-verified, cached), creates a virtual environment named `myapp`, and hands you a ready-to-use handle. Environment names are always explicit, so the same Python version can back multiple independent environments. Subsequent calls return in milliseconds. Every method also has a synchronous twin (`PythonEnvironment.GetEnvironment`, `env.Run`, ...).

## Packages

| Package | What it adds |
| --- | --- |
| `PythonEmbedded.Net` | Everything above: interpreter acquisition, venvs, pip, subprocess execution, live process handles. Fully functional alone. |
| `PythonEmbedded.Net.Runtime.Python311`–`Python315` | Bundled Python archives for fully **offline** use — reference one and `GetEnvironment("3.13", name)` needs zero network and zero configuration. Auto-refreshed when upstream releases. |
| `PythonEmbedded.Net.PackageManagers.Uv` | [uv](https://github.com/astral-sh/uv)-backed env creation and installs (~10x faster than pip). |
| `PythonEmbedded.Net.PackageManagers.Conda` | Conda-ecosystem environments via a self-provisioned [micromamba](https://mamba.readthedocs.io/) — for conda-only packages (CUDA, geospatial, ...). |
| `PythonEmbedded.Net.PackageManagers.Poetry` | Installs a `pyproject.toml` project's dependencies with Poetry. |
| `PythonEmbedded.Net.Runners.PythonNet` | In-process execution via [Python.NET](https://github.com/pythonnet/pythonnet) — no subprocess overhead, direct .NET↔Python interop. |

Satellites plug in with one line — no registration, no reflection:

```csharp
PythonEnvironment.Configure(o =>
{
    o.Installer = new UvInstaller();       // packages via uv
    o.Runner = new InProcessRunner();      // execution via Python.NET
});
```

## More than one-shot scripts

```csharp
// Long-lived processes (servers, workers): streamed output, stdin, kill-on-dispose.
await using var server = env.Start("server.py", ["--port", "8080"]);
server.OutputLine += line => Console.WriteLine($"[py] {line}");

// Failures throw by default, carrying exit code + stdout/stderr:
try { await env.RunAsync("flaky.py"); }
catch (PythonProcessException ex) { Console.WriteLine(ex.Result.StandardError); }

// Or opt out: new RunOptions { ThrowOnError = false }
```

## Extensible by design

Three small interfaces cover every axis, and everything else is sealed:

- **`IPythonSource`** — where interpreters come from (bundled archives, astral downloads, your own directories, a future compile-from-source).
- **`IPackageInstaller`** — how environments are created and packages managed (pip, uv, conda, poetry, yours).
- **`IPythonRunner`** — how code executes (subprocess, in-process, yours).

Implement one, hand it to `PythonEnvironment.Configure`, done.

## Documentation

Start with [Docs/Getting-Started.md](Docs/Getting-Started.md); the full index is in [Docs/README.md](Docs/README.md).

## Requirements

- .NET 9.0 or later
- Windows, macOS, or Linux (x64 / arm64)

## License

MIT — see [LICENSE](LICENSE).
