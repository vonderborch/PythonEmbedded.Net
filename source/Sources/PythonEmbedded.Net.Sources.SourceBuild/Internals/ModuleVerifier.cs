using Microsoft.Extensions.Logging;
using PythonEmbedded.Net.Exceptions;
using PythonEmbedded.Net.Extensibility;
using PythonEmbedded.Net.Models;

namespace PythonEmbedded.Net.Sources.SourceBuild.Internals;

/// <summary>
/// Imports the stdlib modules a fresh build is most likely to have silently dropped. CPython's configure
/// does not fail when a dependency is missing — it just builds without that module — so an interpreter
/// that compiled cleanly can still be useless (no <c>ssl</c> means no <c>pip install</c>).
/// </summary>
internal static class ModuleVerifier
{
    /// <summary>Without these the interpreter is not fit to hand back, so a failure throws.</summary>
    public static readonly string[] CriticalModules = ["ssl", "zlib", "ctypes", "sqlite3", "ensurepip"];

    /// <summary>Useful but not disqualifying; a failure is logged and the install is kept.</summary>
    public static string[] OptionalModules => OperatingSystem.IsWindows()
        ? ["lzma", "bz2", "tkinter"]
        : ["readline", "lzma", "bz2", "tkinter", "dbm.gnu"];

    /// <summary>What to install to get a module back, named in the failure message.</summary>
    private static readonly Dictionary<string, string> Dependencies = new(StringComparer.Ordinal)
    {
        ["ssl"] = "OpenSSL development headers",
        ["zlib"] = "zlib development headers",
        ["ctypes"] = "libffi development headers",
        ["sqlite3"] = "SQLite development headers",
        ["ensurepip"] = "a complete stdlib install",
        ["readline"] = "readline development headers",
        ["lzma"] = "liblzma/xz development headers",
        ["bz2"] = "bzip2 development headers",
        ["tkinter"] = "Tk development headers",
        ["dbm.gnu"] = "gdbm development headers",
    };

    /// <summary>
    /// Runs the staged interpreter once per module group. Throws
    /// <see cref="PythonErrorKind.InstallFailed"/> naming every critical module that would not import.
    /// </summary>
    public static async Task VerifyAsync(
        string pythonExecutable, BuildLog log, IProgress<InstallProgress>? progress, CancellationToken ct)
    {
        progress?.Report(new InstallProgress(InstallPhase.Verifying, Detail: Path.GetFileName(pythonExecutable)));

        List<string> missingCritical = await FindMissingAsync(pythonExecutable, CriticalModules, log, ct).ConfigureAwait(false);
        if (missingCritical.Count > 0)
        {
            throw new PythonException(
                PythonErrorKind.InstallFailed,
                $"""
                 The interpreter compiled, but these required modules are missing: {string.Join(", ", missingCritical)}.
                 CPython builds without a module when its dependency is absent at configure time; install the
                 development packages and rebuild:

                     {string.Join(Environment.NewLine + "    ", missingCritical.Select(Describe))}

                     {DependencyProvisioner.ManualInstallCommand()}

                 Full build log: {log.FilePath}
                 """);
        }

        List<string> missingOptional = await FindMissingAsync(pythonExecutable, OptionalModules, log, ct).ConfigureAwait(false);
        if (missingOptional.Count > 0)
        {
            log.Logger.LogWarning(
                "Built interpreter is missing optional modules: {Modules}. Install the matching development packages and rebuild if you need them.",
                string.Join(", ", missingOptional.Select(Describe)));
        }
    }

    /// <summary>
    /// Imports the group in one process first; only if that fails does each module get its own run, so the
    /// common (healthy) case costs a single subprocess.
    /// </summary>
    private static async Task<List<string>> FindMissingAsync(
        string pythonExecutable, IReadOnlyList<string> modules, BuildLog log, CancellationToken ct)
    {
        PythonResult combined = await log.RunAllowingFailureAsync(
            "verify " + string.Join(", ", modules),
            pythonExecutable,
            ["-c", "import " + string.Join(", ", modules)],
            ct: ct).ConfigureAwait(false);
        if (combined.ExitCode == 0)
        {
            return [];
        }

        List<string> missing = [];
        foreach (string module in modules)
        {
            PythonResult result = await log.RunAllowingFailureAsync(
                $"verify {module}", pythonExecutable, ["-c", $"import {module}"], ct: ct).ConfigureAwait(false);
            if (result.ExitCode != 0)
            {
                missing.Add(module);
            }
        }

        return missing;
    }

    private static string Describe(string module) =>
        Dependencies.TryGetValue(module, out string? dependency) ? $"{module} — needs {dependency}" : module;
}
