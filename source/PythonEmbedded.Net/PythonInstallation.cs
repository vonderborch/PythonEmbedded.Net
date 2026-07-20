namespace PythonEmbedded.Net;

/// <summary>A Python installation on disk (a base interpreter environments are created from).</summary>
public sealed class PythonInstallation
{
    private readonly PythonHost _host;

    internal PythonInstallation(
        PythonVersion version, string directory, string pythonExecutable,
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
    public PythonVersion Version { get; }

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
    /// environments, each identified by its own name.
    /// </summary>
    public Task<PythonVirtualEnvironment> GetEnvironmentAsync(string name, CancellationToken ct = default)
        => _host.GetEnvironmentAsync(this, name, ct);

    /// <inheritdoc cref="GetEnvironmentAsync"/>
    public PythonVirtualEnvironment GetEnvironment(string name)
        => GetEnvironmentAsync(name).GetAwaiter().GetResult();

    /// <inheritdoc />
    public override string ToString() => $"Python {Version} ({SourceName}) at {Directory}";
}
