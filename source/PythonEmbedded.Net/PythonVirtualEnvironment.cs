namespace PythonEmbedded.Net;

/// <summary>
/// A usable Python environment: install packages via <see cref="Packages"/>, run code via
/// <see cref="RunAsync"/>/<see cref="RunCodeAsync"/>/<see cref="RunModuleAsync"/>, or start a
/// long-lived process via <see cref="Start"/>.
/// </summary>
public sealed class PythonVirtualEnvironment
{
    private readonly PythonHost _host;

    internal PythonVirtualEnvironment(
        PythonInstallation installation, string name, string directory,
        string pythonExecutable, bool isBase, PythonHost host)
    {
        Installation = installation;
        Name = name;
        Directory = directory;
        PythonExecutable = pythonExecutable;
        IsBase = isBase;
        _host = host;
        Packages = new PackageManager(this, host);
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

        PythonResult result = await _host.Options.Runner.RunAsync(this, invocation, ct).ConfigureAwait(false);
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

    /// <inheritdoc />
    public override string ToString() => $"{Installation.Version}/{Name} at {Directory}";
}
