using Microsoft.Extensions.Logging;
using Python.Runtime;

namespace PythonEmbedded.Net;

/// <summary>
/// Python.NET implementation of BasePythonVirtualRuntime for virtual environments.
/// </summary>
public class PythonNetVirtualEnvironment : BasePythonVirtualRuntime, IDisposable
{
    private readonly string _virtualEnvironmentPath;
    private readonly ILogger<PythonNetVirtualEnvironment>? _logger;
    private static bool _pythonNetInitialized = false;
    private static readonly object _initLock = new object();
    private static int _activeInstanceCount = 0;
    private bool _disposed = false;

    /// <summary>
    /// Initializes a new instance of the PythonNetVirtualEnvironment class.
    /// </summary>
    /// <param name="virtualEnvironmentPath">The path to the virtual environment directory.</param>
    /// <param name="logger">Optional logger for this runtime.</param>
    public PythonNetVirtualEnvironment(string virtualEnvironmentPath, ILogger<PythonNetVirtualEnvironment>? logger = null)
    {
        if (string.IsNullOrWhiteSpace(virtualEnvironmentPath))
            throw new ArgumentException("Virtual environment path cannot be null or empty.", nameof(virtualEnvironmentPath));

        _virtualEnvironmentPath = virtualEnvironmentPath;
        _logger = logger;

        InitializePythonNet();
        lock (_initLock)
        {
            _activeInstanceCount++;
        }
    }

    /// <summary>
    /// Gets the path to the virtual environment directory.
    /// </summary>
    protected override string VirtualEnvironmentPath => _virtualEnvironmentPath;

    /// <summary>
    /// Gets the logger for this runtime.
    /// </summary>
    protected override ILogger? Logger => _logger;

    /// <summary>
    /// Validates that the virtual environment installation is complete and valid.
    /// </summary>
    protected override void ValidateInstallation()
    {
        base.ValidateInstallation();

        // Verify Python.NET is initialized
        if (!_pythonNetInitialized)
        {
            InitializePythonNet();
        }
    }

    /// <summary>
    /// Initializes Python.NET runtime for the virtual environment.
    /// </summary>
    private void InitializePythonNet()
    {
        lock (_initLock)
        {
            if (_pythonNetInitialized)
                return;

            try
            {
                if (!PythonEngine.IsInitialized)
                {
                    // For virtual environments, Python.NET needs to use the virtual environment's Python
                    // Find the base Python installation from the virtual environment
                    string basePythonPath = FindBasePythonPath();

                    if (!string.IsNullOrEmpty(basePythonPath))
                    {
                        PythonEngine.PythonHome = basePythonPath;
                    }

                    PythonEngine.Initialize();
                    this.Logger?.LogInformation("Python.NET runtime initialized for virtual environment: {Path}", _virtualEnvironmentPath);
                }

                _pythonNetInitialized = true;
            }
            catch (Exception ex)
            {
                throw new Exceptions.PythonNetInitializationException(
                    $"Failed to initialize Python.NET runtime for virtual environment at '{_virtualEnvironmentPath}': {ex.Message}",
                    ex)
                {
                    PythonInstallPath = _virtualEnvironmentPath
                };
            }
        }
    }

    /// <summary>
    /// Finds the base Python installation path from the virtual environment.
    /// Virtual environments typically have a pyvenv.cfg file that points to the base installation.
    /// </summary>
    private string FindBasePythonPath()
    {
        string pyvenvCfg = Path.Combine(_virtualEnvironmentPath, "pyvenv.cfg");
        if (File.Exists(pyvenvCfg))
        {
            // Read pyvenv.cfg to find the base Python path
            var lines = File.ReadAllLines(pyvenvCfg);
            foreach (var line in lines)
            {
                if (line.StartsWith("home", StringComparison.OrdinalIgnoreCase))
                {
                    var parts = line.Split('=');
                    if (parts.Length >= 2)
                    {
                        return parts[1].Trim();
                    }
                }
            }
        }

        // Fallback: try to infer from the virtual environment structure
        // The parent directory structure might contain the base Python installation
        return string.Empty;
    }

    /// <summary>
    /// Releases the resources used by this instance.
    /// </summary>
    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    /// <summary>
    /// Releases the unmanaged resources used by this instance and optionally releases the managed resources.
    /// </summary>
    /// <param name="disposing">True to release both managed and unmanaged resources; false to release only unmanaged resources.</param>
    protected virtual void Dispose(bool disposing)
    {
        if (!_disposed && disposing)
        {
            lock (_initLock)
            {
                _activeInstanceCount--;
                
                // Note: PythonEngine.Shutdown() should only be called when completely done with Python.NET
                // Since Python.NET uses a singleton PythonEngine, we don't shut it down here to avoid
                // breaking other instances. The application should call PythonEngine.Shutdown() when
                // all Python runtimes are disposed and Python.NET is no longer needed.
                if (_activeInstanceCount == 0 && _pythonNetInitialized && Python.Runtime.PythonEngine.IsInitialized)
                {
                    // Optionally shutdown - but this would affect all instances
                    // PythonEngine.Shutdown();
                }
            }
            
            _disposed = true;
        }
    }
}
