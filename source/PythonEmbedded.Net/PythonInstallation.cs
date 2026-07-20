using PythonEmbedded.Net.Extensibility;
using PythonEmbedded.Net.Internals;

namespace PythonEmbedded.Net;

/// <summary>A Python installation on disk (a base interpreter environments are created from).</summary>
public sealed class PythonInstallation
{
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
        string name, CancellationToken ct = default, IPackageInstaller? installer = null, IPythonRunner? runner = null)
        => _host.GetEnvironmentAsync(this, name, ct, installer, runner);

    /// <inheritdoc cref="GetEnvironmentAsync"/>
    public PythonVirtualEnvironment GetEnvironment(string name, IPackageInstaller? installer = null, IPythonRunner? runner = null)
        => GetEnvironmentAsync(name, default, installer, runner).GetAwaiter().GetResult();

    /// <inheritdoc />
    public override string ToString() => $"Python {Version} ({SourceName}) at {Directory}";
}
