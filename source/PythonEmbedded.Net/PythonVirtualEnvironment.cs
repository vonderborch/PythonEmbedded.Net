using System.Text.Json;
using PythonEmbedded.Net.Exceptions;
using PythonEmbedded.Net.Extensibility;
using PythonEmbedded.Net.Models;

namespace PythonEmbedded.Net;

/// <summary>
/// A usable Python environment: install packages via <see cref="Packages"/>, run code via
/// <see cref="RunAsync"/>/<see cref="RunCodeAsync"/>/<see cref="RunModuleAsync"/>, or start a
/// long-lived process via <see cref="Start"/>.
/// </summary>
public sealed class PythonVirtualEnvironment
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web) { WriteIndented = true };

    private readonly IPythonRunner _runner;
    private readonly IPackageInstaller _installer;

    internal PythonVirtualEnvironment(
        PythonInstallation installation, string name, string directory,
        string pythonExecutable, bool isBase, IPackageInstaller installer, IPythonRunner runner)
    {
        Installation = installation;
        Name = name;
        Directory = directory;
        PythonExecutable = pythonExecutable;
        IsBase = isBase;
        _runner = runner;
        _installer = installer;
        Packages = new PackageManager(this, installer);
    }

    /// <summary>The base installation this environment was created from.</summary>
    public PythonInstallation Installation { get; }

    /// <summary>The environment's name (unique per installation).</summary>
    public string Name { get; }

    /// <summary>Root directory of the environment.</summary>
    public string Directory { get; }

    /// <summary>Full path to the environment's python executable.</summary>
    public string PythonExecutable { get; }

    /// <summary>True when this is the base interpreter used directly rather than a virtual environment.</summary>
    public bool IsBase { get; }

    /// <summary>Package operations (install, uninstall, list) using the configured installer.</summary>
    public PackageManager Packages { get; }

    /// <summary>
    /// Runs a script file and returns the buffered result. Throws <see cref="PythonProcessException"/>
    /// on nonzero exit unless <see cref="RunOptions.ThrowOnError"/> is false.
    /// </summary>
    public Task<PythonResult> RunAsync(string scriptPath, string[]? args = null, RunOptions? options = null, CancellationToken ct = default)
        => ExecuteAsync(InvocationKind.Script, scriptPath, args, options, ct);

    /// <summary>Runs inline code (<c>python -c</c>). Error behavior matches <see cref="RunAsync"/>.</summary>
    public Task<PythonResult> RunCodeAsync(string code, RunOptions? options = null, CancellationToken ct = default)
        => ExecuteAsync(InvocationKind.Code, code, null, options, ct);

    /// <summary>Runs a module (<c>python -m</c>). Error behavior matches <see cref="RunAsync"/>.</summary>
    public Task<PythonResult> RunModuleAsync(string module, string[]? args = null, RunOptions? options = null, CancellationToken ct = default)
        => ExecuteAsync(InvocationKind.Module, module, args, options, ct);

    /// <inheritdoc cref="RunAsync"/>
    public PythonResult Run(string scriptPath, string[]? args = null, RunOptions? options = null)
        => RunAsync(scriptPath, args, options).GetAwaiter().GetResult();

    /// <inheritdoc cref="RunCodeAsync"/>
    public PythonResult RunCode(string code, RunOptions? options = null)
        => RunCodeAsync(code, options).GetAwaiter().GetResult();

    /// <inheritdoc cref="RunModuleAsync"/>
    public PythonResult RunModule(string module, string[]? args = null, RunOptions? options = null)
        => RunModuleAsync(module, args, options).GetAwaiter().GetResult();

    /// <summary>
    /// Starts a long-lived Python process (servers, supervised loops) and returns a live handle
    /// with streamed output. Always a subprocess, regardless of the configured runner.
    /// </summary>
    public PythonProcess Start(string scriptPath, string[]? args = null, RunOptions? options = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(scriptPath);
        return PythonProcess.Start(this, new PythonInvocation(
            InvocationKind.Script, scriptPath, args ?? [], options ?? new RunOptions()));
    }

    private async Task<PythonResult> ExecuteAsync(
        InvocationKind kind, string target, string[]? args, RunOptions? options, CancellationToken ct)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(target);
        RunOptions effective = options ?? new RunOptions();
        PythonInvocation invocation = new(kind, target, args ?? [], effective);

        PythonResult result = await _runner.RunAsync(this, invocation, ct).ConfigureAwait(false);
        if (!result.Success && effective.ThrowOnError)
        {
            string what = kind switch
            {
                InvocationKind.Script => $"script '{target}'",
                InvocationKind.Module => $"module '{target}'",
                _ => "code",
            };
            throw new PythonProcessException(
                $"Python {what} exited with code {result.ExitCode}.{(string.IsNullOrWhiteSpace(result.StandardError) ? "" : $" stderr: {result.StandardError.Trim()}")}",
                result);
        }

        return result;
    }

    /// <summary>
    /// Creates a new environment named <paramref name="newName"/> under the same installation, using the
    /// same installer/runner, and replays this environment's installed package list into it.
    /// </summary>
    public async Task<PythonVirtualEnvironment> CloneAsync(string newName, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(newName);
        IReadOnlyList<InstalledPackage> packages = await Packages.ListAsync(ct).ConfigureAwait(false);
        PythonVirtualEnvironment clone = await Installation.GetEnvironmentAsync(newName, ct, _installer, _runner).ConfigureAwait(false);
        if (packages.Count > 0)
        {
            await clone.Packages.InstallAsync(
                new PackageRequest { Packages = packages.Select(p => $"{p.Name}=={p.Version}").ToArray() }, ct)
                .ConfigureAwait(false);
        }

        return clone;
    }

    /// <inheritdoc cref="CloneAsync"/>
    public PythonVirtualEnvironment Clone(string newName)
        => CloneAsync(newName).GetAwaiter().GetResult();

    /// <summary>
    /// Writes this environment's package list as an <see cref="EnvironmentManifest"/> to <paramref name="manifestPath"/>.
    /// A manifest is a package list, not a binary directory snapshot — venv directories bake in absolute
    /// paths (<c>pyvenv.cfg</c>, shebangs) that don't survive relocation. Recreate elsewhere with
    /// <see cref="PythonInstallation.ImportEnvironmentAsync"/>.
    /// </summary>
    public async Task ExportManifestAsync(string manifestPath, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(manifestPath);
        IReadOnlyList<InstalledPackage> packages = await Packages.ListAsync(ct).ConfigureAwait(false);
        EnvironmentManifest manifest = new(Packages.InstallerName, Installation.Version.ToString(), packages, DateTimeOffset.UtcNow);
        await File.WriteAllTextAsync(manifestPath, JsonSerializer.Serialize(manifest, JsonOptions), ct).ConfigureAwait(false);
    }

    /// <inheritdoc cref="ExportManifestAsync"/>
    public void ExportManifest(string manifestPath)
        => ExportManifestAsync(manifestPath).GetAwaiter().GetResult();

    /// <summary>
    /// Runs health checks on this environment: the python executable exists, the <c>env.json</c> marker
    /// is valid, and a package-manager smoke check (<see cref="PackageManager.ListAsync"/>) doesn't throw.
    /// </summary>
    public async Task<DiagnosticsResult> DiagnoseAsync(CancellationToken ct = default)
    {
        List<DiagnosticFinding> findings = [];

        if (!File.Exists(PythonExecutable))
        {
            findings.Add(new DiagnosticFinding(DiagnosticSeverity.Error, "executable-missing",
                $"Python executable not found at '{PythonExecutable}'."));
        }

        if (!Installation.Host.HasValidEnvMarker(Directory))
        {
            findings.Add(new DiagnosticFinding(DiagnosticSeverity.Error, "marker-invalid",
                $"'env.json' under '{Directory}' is missing or unreadable."));
        }

        try
        {
            await Packages.ListAsync(ct).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is PythonException or PythonProcessException)
        {
            findings.Add(new DiagnosticFinding(DiagnosticSeverity.Error, "package-manager-failed",
                $"Package manager smoke check failed: {ex.Message}"));
        }

        bool isHealthy = findings.All(f => f.Severity != DiagnosticSeverity.Error);
        return new DiagnosticsResult(isHealthy, findings);
    }

    /// <inheritdoc cref="DiagnoseAsync"/>
    public DiagnosticsResult Diagnose()
        => DiagnoseAsync().GetAwaiter().GetResult();

    /// <inheritdoc />
    public override string ToString() => $"{Installation.Version}/{Name} at {Directory}";
}
