using PythonEmbedded.Net.Extensibility;
using PythonEmbedded.Net.Internals;
using PythonEmbedded.Net.Models;

namespace PythonEmbedded.Net;

/// <summary>A Python installation on disk (a base interpreter environments are created from).</summary>
public sealed class PythonInstallation
{
    private static readonly System.Text.Json.JsonSerializerOptions JsonOptions = new(System.Text.Json.JsonSerializerDefaults.Web);

    private readonly PythonHost _host;

    internal PythonInstallation(
        Models.PythonVersion version, string directory, string pythonExecutable,
        string sourceName, string installId, PythonHost host)
    {
        Version = version;
        Directory = directory;
        PythonExecutable = pythonExecutable;
        SourceName = sourceName;
        InstallId = installId;
        _host = host;
    }

    /// <summary>The concrete Python version.</summary>
    public Models.PythonVersion Version { get; }

    /// <summary>Root directory of the installation.</summary>
    public string Directory { get; }

    /// <summary>Full path to the base interpreter executable.</summary>
    public string PythonExecutable { get; }

    /// <summary>Which <see cref="IPythonSource"/> provided this installation.</summary>
    public string SourceName { get; }

    internal string InstallId { get; }

    internal PythonHost Host => _host;

    /// <summary>
    /// Gets (creating on first use) a named virtual environment based on this installation.
    /// <paramref name="name"/> is required — the same installation can back multiple independent
    /// environments, each identified by its own name. <paramref name="installer"/> and
    /// <paramref name="runner"/> override <see cref="PythonOptions.Installer"/>/<see cref="PythonOptions.Runner"/>
    /// for this environment; see <see cref="PythonEnvironment.Configure"/> for the recorded-vs-not-recorded
    /// distinction between them.
    /// </summary>
    public Task<PythonVirtualEnvironment> GetEnvironmentAsync(
        string name, CancellationToken ct = default, IPackageInstaller? installer = null, IPythonRunner? runner = null,
        IProgress<InstallProgress>? progress = null)
        => _host.GetEnvironmentAsync(this, name, ct, installer, runner, progress);

    /// <inheritdoc cref="GetEnvironmentAsync"/>
    public PythonVirtualEnvironment GetEnvironment(
        string name, IPackageInstaller? installer = null, IPythonRunner? runner = null, IProgress<InstallProgress>? progress = null)
        => GetEnvironmentAsync(name, default, installer, runner, progress).GetAwaiter().GetResult();

    /// <summary>
    /// Recreates an environment from a manifest previously produced by <see cref="PythonVirtualEnvironment.ExportManifestAsync"/>:
    /// creates a new environment named <paramref name="name"/> and installs the manifest's package list into it.
    /// </summary>
    public async Task<PythonVirtualEnvironment> ImportEnvironmentAsync(
        string name, string manifestPath, CancellationToken ct = default,
        IPackageInstaller? installer = null, IPythonRunner? runner = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(manifestPath);
        EnvironmentManifest manifest = System.Text.Json.JsonSerializer.Deserialize<EnvironmentManifest>(
            await File.ReadAllTextAsync(manifestPath, ct).ConfigureAwait(false), JsonOptions)
            ?? throw new Exceptions.PythonException(
                Exceptions.PythonErrorKind.EnvironmentFailed, $"Manifest at '{manifestPath}' could not be parsed.");

        PythonVirtualEnvironment env = await GetEnvironmentAsync(name, ct, installer, runner).ConfigureAwait(false);
        foreach (InstalledPackage package in manifest.Packages)
        {
            await env.Packages.InstallAsync(
                new PackageRequest { Packages = [$"{package.Name}=={package.Version}"] }, ct).ConfigureAwait(false);
        }

        return env;
    }

    /// <inheritdoc cref="ImportEnvironmentAsync"/>
    public PythonVirtualEnvironment ImportEnvironment(
        string name, string manifestPath, IPackageInstaller? installer = null, IPythonRunner? runner = null)
        => ImportEnvironmentAsync(name, manifestPath, default, installer, runner).GetAwaiter().GetResult();

    /// <summary>
    /// Runs health checks on this installation: the interpreter executable exists and reports its version,
    /// the <c>install.json</c> marker is valid, sysconfig has been patched (POSIX only), and no stale lock
    /// files remain under the locks directory.
    /// </summary>
    public async Task<DiagnosticsResult> DiagnoseAsync(CancellationToken ct = default)
    {
        List<DiagnosticFinding> findings = [];

        if (!File.Exists(PythonExecutable))
        {
            findings.Add(new DiagnosticFinding(DiagnosticSeverity.Error, "executable-missing",
                $"Python executable not found at '{PythonExecutable}'."));
        }
        else
        {
            try
            {
                Models.PythonResult result = await Subprocess.RunAsync(PythonExecutable, ["--version"], ct: ct).ConfigureAwait(false);
                if (!result.Success)
                {
                    findings.Add(new DiagnosticFinding(DiagnosticSeverity.Error, "executable-failed",
                        $"'{PythonExecutable} --version' exited {result.ExitCode}: {result.StandardError.Trim()}"));
                }
            }
            catch (Exceptions.PythonException ex)
            {
                findings.Add(new DiagnosticFinding(DiagnosticSeverity.Error, "executable-failed", ex.Message));
            }
        }

        if (!_host.HasValidInstallMarker(Directory))
        {
            findings.Add(new DiagnosticFinding(DiagnosticSeverity.Error, "marker-invalid",
                $"'install.json' under '{Directory}' is missing or unreadable."));
        }

        if (!SysconfigPatcher.IsPatched(Directory))
        {
            findings.Add(new DiagnosticFinding(DiagnosticSeverity.Warning, "sysconfig-unpatched",
                $"sysconfig data under '{Directory}' still contains the build-machine install path."));
        }

        foreach (string staleLock in _host.InspectStaleLocks())
        {
            findings.Add(new DiagnosticFinding(DiagnosticSeverity.Warning, "stale-lock", $"Stale lock file: '{staleLock}'."));
        }

        bool isHealthy = findings.All(f => f.Severity != DiagnosticSeverity.Error);
        return new DiagnosticsResult(isHealthy, findings);
    }

    /// <inheritdoc cref="DiagnoseAsync"/>
    public DiagnosticsResult Diagnose()
        => DiagnoseAsync().GetAwaiter().GetResult();

    /// <inheritdoc />
    public override string ToString() => $"Python {Version} ({SourceName}) at {Directory}";
}
