using System.Runtime.InteropServices;
using PythonEmbedded.Net.OLD.Exceptions;

namespace PythonEmbedded.Net.OLD;

/// <summary>
/// Base class for Python virtual environment runtime implementations.
/// </summary>
public abstract class BasePythonVirtualRuntime : BasePythonRuntime
{
    /// <summary>
    /// Gets the path to the virtual environment directory.
    /// </summary>
    protected abstract string VirtualEnvironmentPath { get; }

    /// <summary>
    /// Gets the Python executable path for this virtual environment.
    /// </summary>
    protected override string PythonExecutablePath
    {
        get
        {
            return RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
                ? Path.Combine(VirtualEnvironmentPath, "Scripts", "python.exe")
                : Path.Combine(VirtualEnvironmentPath, "bin", "python3");
        }
    }

    /// <summary>
    /// Gets the working directory for this virtual environment (defaults to the virtual environment path).
    /// </summary>
    protected override string WorkingDirectory => VirtualEnvironmentPath;

    /// <summary>
    /// Virtual environments created by <c>uv venv</c> do not include pip or a local uv copy.
    /// Fall back to the base interpreter's uv from <c>pyvenv.cfg</c>; uv commands target this venv via <c>--python</c>.
    /// </summary>
    protected override IEnumerable<string> GetAdditionalUvCandidatePaths() =>
        GetUvCandidatePathsFromPyvenvCfg(VirtualEnvironmentPath);

    /// <summary>
    /// Validates that the virtual environment installation is complete and valid.
    /// </summary>
    protected override void ValidateInstallation()
    {
        if (string.IsNullOrWhiteSpace(VirtualEnvironmentPath))
        {
            throw new VirtualEnvironmentNotFoundException("Virtual environment path is not set.");
        }

        if (!Directory.Exists(VirtualEnvironmentPath))
        {
            throw new VirtualEnvironmentNotFoundException(
                $"Virtual environment directory does not exist: {VirtualEnvironmentPath}")
            {
                VirtualEnvironmentName = Path.GetFileName(VirtualEnvironmentPath)
            };
        }

        if (!File.Exists(PythonExecutablePath))
        {
            throw new VirtualEnvironmentNotFoundException(
                $"Python executable not found in virtual environment: {PythonExecutablePath}")
            {
                VirtualEnvironmentName = Path.GetFileName(VirtualEnvironmentPath)
            };
        }
    }
}
