using System.Runtime.InteropServices;
using PythonEmbedded.Net.Models;

namespace PythonEmbedded.Net.Test.TestUtilities;

/// <summary>
/// Helper class for creating mock Python instances for testing.
/// </summary>
public static class MockPythonInstanceHelper
{
    /// <summary>
    /// Creates a mock Python instance directory structure.
    /// Note: This creates the directory structure but doesn't include actual Python binaries.
    /// For real integration tests, you'll need actual Python installations.
    /// </summary>
    /// <param name="baseDirectory">The base directory where the instance should be created.</param>
    /// <param name="pythonVersion">The Python version (e.g., "3.12.0").</param>
    /// <param name="buildDate">The build date.</param>
    /// <returns>The instance metadata.</returns>
    public static InstanceMetadata CreateMockPythonInstance(
        string baseDirectory,
        string pythonVersion = "3.12.0",
        DateTime? buildDate = null)
    {
        var actualBuildDate = buildDate ?? new DateTime(2024, 1, 15);
        string instanceDirectory = Path.Combine(baseDirectory, $"python-{pythonVersion}-{actualBuildDate:yyyyMMdd}");
        Directory.CreateDirectory(instanceDirectory);

        // Create Python executable path structure
        string pythonExe;
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            pythonExe = Path.Combine(instanceDirectory, "python.exe");
        }
        else
        {
            string binDir = Path.Combine(instanceDirectory, "bin");
            Directory.CreateDirectory(binDir);
            pythonExe = Path.Combine(binDir, "python3");
        }

        // Create a placeholder file (actual tests may need real Python)
        File.WriteAllText(pythonExe, "#!/bin/bash\necho 'Python mock'\n");

        // Create instance metadata
        var metadata = new InstanceMetadata
        {
            PythonVersion = pythonVersion,
            BuildDate = actualBuildDate,
            WasLatestBuild = false,
            InstallationDate = DateTime.Now,
            Directory = instanceDirectory
        };

        metadata.Save(instanceDirectory);

        return metadata;
    }

    /// <summary>
    /// Creates a mock virtual environment directory structure.
    /// </summary>
    /// <param name="baseDirectory">The base directory where the venv should be created.</param>
    /// <param name="venvName">The name of the virtual environment.</param>
    /// <returns>The path to the virtual environment.</returns>
    public static string CreateMockVirtualEnvironment(string baseDirectory, string venvName)
    {
        string venvPath = Path.Combine(baseDirectory, venvName);
        Directory.CreateDirectory(venvPath);

        // Create Python executable in virtual environment
        string pythonExe;
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            string scriptsDir = Path.Combine(venvPath, "Scripts");
            Directory.CreateDirectory(scriptsDir);
            pythonExe = Path.Combine(scriptsDir, "python.exe");
        }
        else
        {
            string binDir = Path.Combine(venvPath, "bin");
            Directory.CreateDirectory(binDir);
            pythonExe = Path.Combine(binDir, "python3");
        }

        // Create a placeholder file
        File.WriteAllText(pythonExe, "#!/bin/bash\necho 'Python mock'\n");

        // Create pyvenv.cfg
        string pyvenvCfg = Path.Combine(venvPath, "pyvenv.cfg");
        File.WriteAllText(pyvenvCfg, "home = /usr/local/bin\ninclude-system-site-packages = false\nversion = 3.12.0\n");

        return venvPath;
    }
}
