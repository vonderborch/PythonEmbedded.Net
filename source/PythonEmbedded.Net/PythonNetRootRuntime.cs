using System.Runtime.InteropServices;
using Microsoft.Extensions.Logging;
using PythonEmbedded.Net.Exceptions;
using PythonEmbedded.Net.Models;
using Python.Runtime;

namespace PythonEmbedded.Net;

/// <summary>
/// Python.NET implementation of BasePythonRootRuntime for root Python instances.
/// </summary>
public class PythonNetRootRuntime : BasePythonRootRuntime, IDisposable
{
    private readonly InstanceMetadata _instanceMetadata;
    private readonly ILogger<PythonNetRootRuntime>? _logger;
    private static bool _pythonNetInitialized = false;
    private static readonly object _initLock = new object();
    private static int _activeInstanceCount = 0;
    private bool _disposed = false;

    /// <summary>
    /// Initializes a new instance of the PythonNetRootRuntime class.
    /// </summary>
    /// <param name="instanceMetadata">The metadata for the Python instance.</param>
    /// <param name="logger">Optional logger for this runtime.</param>
    public PythonNetRootRuntime(InstanceMetadata instanceMetadata, ILogger<PythonNetRootRuntime>? logger = null)
    {
        _instanceMetadata = instanceMetadata ?? throw new ArgumentNullException(nameof(instanceMetadata));
        _logger = logger;

        InitializePythonNet();
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
            throw new PythonNotInstalledException("Python instance directory is not set.");
        }

        if (!Directory.Exists(_instanceMetadata.Directory))
        {
            throw new PythonNotInstalledException(
                $"Python instance directory does not exist: {_instanceMetadata.Directory}");
        }

        if (!File.Exists(PythonExecutablePath))
        {
            throw new PythonNotInstalledException(
                $"Python executable not found: {PythonExecutablePath}");
        }

        // Verify Python.NET is initialized
        if (!_pythonNetInitialized)
        {
            InitializePythonNet();
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
        return new PythonNetVirtualEnvironment(venvPath, null);
    }

    /// <summary>
    /// Initializes Python.NET runtime.
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
                    // Set Python DLL path - Python.NET needs to know where the Python DLL is located
                    var pythonDirectory = Path.Combine(_instanceMetadata.Directory, "python");
                    string pythonDllPath = FindPythonDllPath();
                    if (!string.IsNullOrEmpty(pythonDllPath))
                    {
                        PythonEngine.PythonHome = pythonDirectory;
                        Runtime.PythonDLL = pythonDllPath;
                    }

                    PythonEngine.Initialize();
                    this.Logger?.LogInformation("Python.NET runtime initialized for Python instance: {Directory}", pythonDirectory);
                }

                _pythonNetInitialized = true;
            }
            catch (Exception ex)
            {
                throw new PythonNetInitializationException(
                    $"Failed to initialize Python.NET runtime for Python instance at '{_instanceMetadata.Directory}': {ex.Message}",
                    ex)
                {
                    PythonInstallPath = _instanceMetadata.Directory
                };
            }
        }
    }

    /// <summary>
    /// Finds the Python DLL path based on the platform.
    /// </summary>
    private string FindPythonDllPath()
    {
        var pythonDirectory = Path.Combine(_instanceMetadata.Directory, "python");
        
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            // On Windows, look for python3x.dll in the python directory
            string[] possibleDllNames = ["python3.dll", "python310.dll", "python311.dll", "python312.dll", "python313.dll"];
            foreach (var dllName in possibleDllNames)
            {
                string dllPath = Path.Combine(pythonDirectory, dllName);
                if (File.Exists(dllPath))
                {
                    return dllPath;
                }
            }
        }
        else
        {
            // On Unix-like systems, Python.NET typically finds the library automatically
            // but we can try to locate it in common locations
            string libPath = Path.Combine(pythonDirectory, "lib");
            if (Directory.Exists(libPath))
            {
                // Look for libpython*.so files
                var pythonLibs = Directory.GetFiles(libPath, "libpython*.so*", SearchOption.AllDirectories);
                if (pythonLibs.Length > 0)
                {
                    // Return the directory containing the library
                    return Path.GetDirectoryName(pythonLibs[0]) ?? libPath;
                }
            }
        }

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
