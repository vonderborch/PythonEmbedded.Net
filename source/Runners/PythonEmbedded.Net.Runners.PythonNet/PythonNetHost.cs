using Python.Runtime;

namespace PythonEmbedded.Net.Runners.PythonNet;

/// <summary>
/// The process-global Python.NET engine, bound to ONE environment for the lifetime of the process
/// (a CPython limitation, not ours). Initialized lazily by <see cref="InProcessRunner"/> or
/// explicitly via <see cref="Initialize"/>; attempts to rebind to a different environment throw.
/// For direct interop, use <see cref="RunInScope"/> or <see cref="AcquireGil"/>.
/// </summary>
public static class PythonNetHost
{
    private static readonly object SyncRoot = new();
    private static PythonVirtualEnvironment? _bound;

    /// <summary>The environment the engine is bound to, or null when not yet initialized.</summary>
    public static PythonVirtualEnvironment? BoundEnvironment => _bound;

    /// <summary>
    /// Initializes the engine against <paramref name="env"/>: loads libpython from the base
    /// installation and activates the environment in-process (sys.prefix + site-packages).
    /// Safe to call repeatedly with the same environment.
    /// </summary>
    /// <exception cref="PythonException">Already bound to a different environment, or libpython was not found.</exception>
    public static void Initialize(PythonVirtualEnvironment env)
    {
        ArgumentNullException.ThrowIfNull(env);
        lock (SyncRoot)
        {
            if (_bound is not null)
            {
                if (!string.Equals(_bound.Directory, env.Directory, StringComparison.Ordinal))
                {
                    throw new PythonException(
                        PythonErrorKind.ExecutionFailed,
                        $"Python.NET is already bound to '{_bound.Directory}' and CPython cannot be re-initialized in-process. " +
                        "Use one environment per process with the in-process runner, or fall back to the subprocess runner.");
                }

                return;
            }

            Runtime.PythonDLL = FindLibPython(env.Installation);
            PythonEngine.Initialize();
            PythonEngine.BeginAllowThreads();

            using (Py.GIL())
            {
                ActivateEnvironment(env);
            }

            _bound = env;
        }
    }

    /// <summary>Runs <paramref name="action"/> under the GIL in a fresh module scope.</summary>
    public static void RunInScope(Action<PyModule> action)
    {
        ArgumentNullException.ThrowIfNull(action);
        EnsureInitialized();
        using (Py.GIL())
        using (PyModule scope = Py.CreateScope())
        {
            action(scope);
        }
    }

    /// <summary>Runs <paramref name="func"/> under the GIL in a fresh module scope and returns its result.</summary>
    public static T RunInScope<T>(Func<PyModule, T> func)
    {
        ArgumentNullException.ThrowIfNull(func);
        EnsureInitialized();
        using (Py.GIL())
        using (PyModule scope = Py.CreateScope())
        {
            return func(scope);
        }
    }

    /// <summary>Acquires the GIL for manual interop; dispose to release.</summary>
    public static IDisposable AcquireGil()
    {
        EnsureInitialized();
        return Py.GIL();
    }

    private static void EnsureInitialized()
    {
        if (_bound is null)
        {
            throw new PythonException(
                PythonErrorKind.ExecutionFailed,
                "Python.NET is not initialized. Call PythonNetHost.Initialize(env) (or run something through InProcessRunner) first.");
        }
    }

    /// <summary>Points sys at the virtual environment, mirroring what activating a venv does.</summary>
    private static void ActivateEnvironment(PythonVirtualEnvironment env)
    {
        if (env.IsBase)
        {
            return;
        }

        string sitePackages = OperatingSystem.IsWindows()
            ? Path.Combine(env.Directory, "Lib", "site-packages")
            : Path.Combine(env.Directory, "lib",
                $"python{env.Installation.Version.Major}.{env.Installation.Version.Minor}", "site-packages");

        using PyModule scope = Py.CreateScope();
        scope.Set("env_prefix", env.Directory);
        scope.Set("site_packages", sitePackages);
        scope.Exec("""
            import sys, site
            sys.prefix = env_prefix
            sys.exec_prefix = env_prefix
            if site_packages not in sys.path:
                site.addsitedir(site_packages)
            """);
    }

    private static string FindLibPython(PythonInstallation install)
    {
        int major = install.Version.Major;
        int minor = install.Version.Minor;
        string root = install.Directory;
        string interpreterDirectory = Path.GetDirectoryName(install.PythonExecutable)!;

        string[] candidates = OperatingSystem.IsWindows()
            ? [Path.Combine(interpreterDirectory, $"python{major}{minor}.dll")]
            : OperatingSystem.IsMacOS()
                ?
                [
                    Path.Combine(root, "python", "lib", $"libpython{major}.{minor}.dylib"),
                    Path.Combine(root, "lib", $"libpython{major}.{minor}.dylib"),
                ]
                :
                [
                    Path.Combine(root, "python", "lib", $"libpython{major}.{minor}.so.1.0"),
                    Path.Combine(root, "python", "lib", $"libpython{major}.{minor}.so"),
                    Path.Combine(root, "lib", $"libpython{major}.{minor}.so.1.0"),
                    Path.Combine(root, "lib", $"libpython{major}.{minor}.so"),
                ];

        return candidates.FirstOrDefault(File.Exists)
            ?? throw new PythonException(
                PythonErrorKind.ExecutionFailed,
                $"libpython for {install.Version} was not found under '{root}'. " +
                "The in-process runner requires a full installation with a shared libpython (python-build-standalone install_only builds have one).");
    }
}
