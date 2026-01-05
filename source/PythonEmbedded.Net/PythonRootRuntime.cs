using System.Runtime.InteropServices;
using Microsoft.Extensions.Logging;
using PythonEmbedded.Net.Models;

namespace PythonEmbedded.Net;

/// <summary>
/// Direct implementation of BasePythonRootRuntime for root Python instances (not using Python.NET).
/// </summary>
public class PythonRootRuntime : BasePythonRootRuntime
{
    private readonly InstanceMetadata _instanceMetadata;
    private readonly ILogger<PythonRootRuntime>? _logger;

    /// <summary>
    /// Initializes a new instance of the PythonRootRuntime class.
    /// </summary>
    /// <param name="instanceMetadata">The metadata for the Python instance.</param>
    /// <param name="logger">Optional logger for this runtime.</param>
    public PythonRootRuntime(InstanceMetadata instanceMetadata, ILogger<PythonRootRuntime>? logger = null)
    {
        _instanceMetadata = instanceMetadata ?? throw new ArgumentNullException(nameof(instanceMetadata));
        _logger = logger;
    }

    /// <summary>
    /// Gets the Python executable path for this runtime.
    /// </summary>
    protected override string PythonExecutablePath
    {
        get
        {
            var pythonDirectory = Path.Combine(_instanceMetadata.Directory, "python");
            return RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
                ? Path.Combine(pythonDirectory, "python.exe")
                : Path.Combine(pythonDirectory, "bin", "python3");
        }
    }

    /// <summary>
    /// Gets the working directory for this runtime.
    /// </summary>
    protected override string WorkingDirectory => _instanceMetadata.Directory;

    /// <summary>
    /// Gets the logger for this runtime.
    /// </summary>
    protected override ILogger? Logger => _logger;

    /// <summary>
    /// Gets the base directory for virtual environments managed by this root runtime.
    /// </summary>
    protected override string VirtualEnvironmentsDirectory => Path.Combine(_instanceMetadata.Directory, "venvs");

    /// <summary>
    /// Gets the instance metadata for this root runtime.
    /// </summary>
    protected override InstanceMetadata InstanceMetadata => _instanceMetadata;

    /// <summary>
    /// Validates that the Python installation is complete and valid.
    /// </summary>
    protected override void ValidateInstallation()
    {
        if (string.IsNullOrWhiteSpace(_instanceMetadata.Directory))
        {
            throw new Exceptions.PythonNotInstalledException("Python instance directory is not set.");
        }

        if (!Directory.Exists(_instanceMetadata.Directory))
        {
            throw new Exceptions.PythonNotInstalledException(
                $"Python instance directory does not exist: {_instanceMetadata.Directory}");
        }

        if (!File.Exists(PythonExecutablePath))
        {
            throw new Exceptions.PythonNotInstalledException(
                $"Python executable not found: {PythonExecutablePath}");
        }
    }

    /// <summary>
    /// Creates a virtual runtime instance for the specified virtual environment path.
    /// </summary>
    /// <param name="venvPath">The path to the virtual environment directory.</param>
    /// <returns>A virtual runtime instance.</returns>
    protected override BasePythonVirtualRuntime CreateVirtualRuntimeInstance(string venvPath)
    {
        // Note: Logger is optional, so we pass null here.
        // If logging is needed for virtual environments, a logger factory could be added to the root runtime.
        return new PythonRootVirtualEnvironment(venvPath, null);
    }
}
