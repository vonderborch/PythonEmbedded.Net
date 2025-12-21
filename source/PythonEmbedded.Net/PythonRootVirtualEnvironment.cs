using Microsoft.Extensions.Logging;

namespace PythonEmbedded.Net;

/// <summary>
/// Direct implementation of BasePythonVirtualRuntime for virtual environments (not using Python.NET).
/// </summary>
public class PythonRootVirtualEnvironment : BasePythonVirtualRuntime
{
    private readonly string _virtualEnvironmentPath;
    private readonly ILogger<PythonRootVirtualEnvironment>? _logger;

    /// <summary>
    /// Initializes a new instance of the PythonRootVirtualEnvironment class.
    /// </summary>
    /// <param name="virtualEnvironmentPath">The path to the virtual environment directory.</param>
    /// <param name="logger">Optional logger for this runtime.</param>
    public PythonRootVirtualEnvironment(string virtualEnvironmentPath, ILogger<PythonRootVirtualEnvironment>? logger = null)
    {
        if (string.IsNullOrWhiteSpace(virtualEnvironmentPath))
            throw new ArgumentException("Virtual environment path cannot be null or empty.", nameof(virtualEnvironmentPath));

        _virtualEnvironmentPath = virtualEnvironmentPath;
        _logger = logger;
    }

    /// <summary>
    /// Gets the path to the virtual environment directory.
    /// </summary>
    protected override string VirtualEnvironmentPath => _virtualEnvironmentPath;

    /// <summary>
    /// Gets the logger for this runtime.
    /// </summary>
    protected override ILogger? Logger => _logger;
}
