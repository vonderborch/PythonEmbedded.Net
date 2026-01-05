using System.Diagnostics;
using System.Text;
using Microsoft.Extensions.Logging;
using PythonEmbedded.Net.Exceptions;
using PythonEmbedded.Net.Services;

namespace PythonEmbedded.Net;

/// <summary>
/// Represents the result of executing a Python command or script.
/// </summary>
public record PythonExecutionResult(
    int ExitCode,
    string StandardOutput = "",
    string StandardError = "");

/// <summary>
/// Base class for Python runtime implementations, providing common functionality for executing commands,
/// scripts, and installing packages.
/// </summary>
public abstract class BasePythonRuntime
{
    /// <summary>
    /// Gets the Python executable path for this runtime.
    /// </summary>
    protected abstract string PythonExecutablePath { get; }

    /// <summary>
    /// Gets the working directory for this runtime.
    /// </summary>
    protected abstract string WorkingDirectory { get; }

    /// <summary>
    /// Gets the logger for this runtime.
    /// </summary>
    protected abstract ILogger? Logger { get; }

    /// <summary>
    /// Gets or creates the process executor for this runtime.
    /// </summary>
    private IProcessExecutor? _processExecutor;
    protected virtual IProcessExecutor ProcessExecutor
    {
        get
        {
            if (_processExecutor == null)
            {
                // Create a default process executor with the runtime's logger
                _processExecutor = new ProcessExecutor(Logger as ILogger<ProcessExecutor>);
            }
            return _processExecutor;
        }
    }

    /// <summary>
    /// Validates that the Python installation is complete and valid.
    /// </summary>
    protected abstract void ValidateInstallation();

    /// <summary>
    /// Gets the detected path to the uv executable, or null if not found.
    /// </summary>
    public string? UvPath { get; private set; }

    /// <summary>
    /// Gets whether uv is available for use.
    /// </summary>
    public bool IsUvAvailable => !string.IsNullOrEmpty(UvPath);

    private bool _uvDetected;

    /// <summary>
    /// Detects if uv is installed and available on the system.
    /// </summary>
    /// <param name="customPath">Optional custom path to uv executable.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>True if uv is available, false otherwise.</returns>
    public async Task<bool> DetectUvAsync(string? customPath = null, CancellationToken cancellationToken = default)
    {
        if (_uvDetected && UvPath != null)
            return true;

        // If custom path is provided, check it first
        if (!string.IsNullOrEmpty(customPath))
        {
            if (await IsUvExecutableValidAsync(customPath, cancellationToken).ConfigureAwait(false))
            {
                UvPath = customPath;
                _uvDetected = true;
                this.Logger?.LogInformation("uv detected at custom path: {Path}", UvPath);
                return true;
            }
        }

        // Common locations to search for uv
        var candidates = GetUvCandidatePaths();

        foreach (var candidate in candidates)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (await IsUvExecutableValidAsync(candidate, cancellationToken).ConfigureAwait(false))
            {
                UvPath = candidate;
                _uvDetected = true;
                this.Logger?.LogInformation("uv detected: {Path}", UvPath);
                return true;
            }
        }

        _uvDetected = true; // Mark as detected (but not found)
        this.Logger?.LogWarning("uv not found on system. Package operations require uv. Call EnsureUvInstalledAsync() to install it.");
        return false;
    }

    /// <summary>
    /// Gets candidate paths where uv might be installed.
    /// </summary>
    private static string[] GetUvCandidatePaths()
    {
        var candidates = new List<string> { "uv" }; // In PATH

        var homeDir = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);

        if (OperatingSystem.IsWindows())
        {
            candidates.AddRange([
                Path.Combine(homeDir, ".cargo", "bin", "uv.exe"),
                Path.Combine(homeDir, ".local", "bin", "uv.exe"),
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "uv", "uv.exe"),
            ]);
        }
        else if (OperatingSystem.IsMacOS())
        {
            candidates.AddRange([
                Path.Combine(homeDir, ".cargo", "bin", "uv"),
                Path.Combine(homeDir, ".local", "bin", "uv"),
                "/opt/homebrew/bin/uv",
                "/usr/local/bin/uv",
            ]);
        }
        else // Linux
        {
            candidates.AddRange([
                Path.Combine(homeDir, ".cargo", "bin", "uv"),
                Path.Combine(homeDir, ".local", "bin", "uv"),
                "/usr/local/bin/uv",
                "/usr/bin/uv",
            ]);
        }

        return candidates.ToArray();
    }

    /// <summary>
    /// Checks if a uv executable path is valid and working.
    /// </summary>
    private async Task<bool> IsUvExecutableValidAsync(string path, CancellationToken cancellationToken)
    {
        try
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = path,
                Arguments = "--version",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                RedirectStandardInput = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using var process = Process.Start(startInfo);
            if (process == null)
                return false;

            process.StandardInput.Close();
            await process.WaitForExitAsync(cancellationToken).ConfigureAwait(false);

            return process.ExitCode == 0;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Executes a uv command and returns the result.
    /// </summary>
    /// <param name="arguments">The arguments to pass to uv.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <param name="workingDirectory">Optional working directory.</param>
    /// <returns>The execution result.</returns>
    protected async Task<PythonExecutionResult> ExecuteUvAsync(
        IEnumerable<string> arguments,
        CancellationToken cancellationToken = default,
        string? workingDirectory = null)
    {
        if (!IsUvAvailable)
            throw new InvalidOperationException("uv is not available. Call DetectUvAsync first.");

        var startInfo = new ProcessStartInfo
        {
            FileName = UvPath!,
            WorkingDirectory = workingDirectory ?? WorkingDirectory,
            UseShellExecute = false,
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true,
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8
        };

        foreach (var arg in arguments)
        {
            startInfo.ArgumentList.Add(arg);
        }

        this.Logger?.LogDebug("Executing uv: {Args}", string.Join(" ", arguments));

        var result = await ProcessExecutor.ExecuteAsync(
            startInfo,
            null,
            null,
            null,
            cancellationToken).ConfigureAwait(false);

        return new PythonExecutionResult(result.ExitCode, result.StandardOutput, result.StandardError);
    }

    /// <summary>
    /// Ensures uv is installed, attempting to install it via pip if not found.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>True if uv is available after this call, false otherwise.</returns>
    public async Task<bool> EnsureUvInstalledAsync(CancellationToken cancellationToken = default)
    {
        // First check if already available
        if (await DetectUvAsync(cancellationToken: cancellationToken).ConfigureAwait(false))
            return true;

        this.Logger?.LogInformation("uv not found, attempting to install via pip...");

        try
        {
            // Install uv via pip
            var result = await ExecuteProcessAsync(
                ["-m", "pip", "install", "uv"],
                null, null, null,
                cancellationToken, null, null).ConfigureAwait(false);

            if (result.ExitCode != 0)
            {
                this.Logger?.LogWarning("Failed to install uv via pip: {Error}", result.StandardError);
                return false;
            }

            // Re-detect uv after installation
            _uvDetected = false;
            return await DetectUvAsync(cancellationToken: cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            this.Logger?.LogWarning(ex, "Failed to install uv");
            return false;
        }
    }

    /// <summary>
    /// Ensures that uv is available for package operations.
    /// Automatically detects and installs uv if not already available.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <exception cref="InvalidOperationException">Thrown if uv cannot be detected or installed.</exception>
    protected async Task EnsureUvAvailableAsync(CancellationToken cancellationToken = default)
    {
        if (IsUvAvailable)
            return;

        // Try to detect uv first
        if (await DetectUvAsync(cancellationToken: cancellationToken).ConfigureAwait(false))
            return;

        // Try to install uv
        this.Logger?.LogInformation("uv not found, attempting to auto-install...");
        if (await EnsureUvInstalledAsync(cancellationToken).ConfigureAwait(false))
            return;

        throw new InvalidOperationException(
            "uv is required for package operations but could not be detected or installed. " +
            "Please install uv manually (https://docs.astral.sh/uv/getting-started/installation/).");
    }

    /// <summary>
    /// Validates that uv is available for package operations.
    /// Throws an exception if uv is not available.
    /// </summary>
    /// <exception cref="InvalidOperationException">Thrown if uv is not available.</exception>
    protected void ValidateUvAvailable()
    {
        if (!IsUvAvailable)
        {
            throw new InvalidOperationException(
                "uv is not available. uv should be installed when the runtime or virtual environment is created. " +
                "If this runtime was created manually, call EnsureUvInstalledAsync() first.");
        }
    }

    /// <summary>
    /// Performs a comprehensive health check of the Python installation.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A dictionary containing health check results.</returns>
    public async Task<Dictionary<string, object>> ValidatePythonInstallationAsync(CancellationToken cancellationToken = default)
    {
        var results = new Dictionary<string, object>();

        // Check if executable exists
        results["ExecutableExists"] = File.Exists(PythonExecutablePath);
        results["ExecutablePath"] = PythonExecutablePath;

        // Check if working directory exists
        results["WorkingDirectoryExists"] = Directory.Exists(WorkingDirectory);
        results["WorkingDirectory"] = WorkingDirectory;

        // Try to get Python version
        try
        {
            var versionInfo = await GetPythonVersionInfoAsync(cancellationToken).ConfigureAwait(false);
            results["PythonVersionInfo"] = versionInfo;
            results["PythonVersionCheck"] = "Success";
        }
        catch (Exception ex)
        {
            results["PythonVersionCheck"] = "Failed";
            results["PythonVersionError"] = ex.Message;
        }

        // Try to get pip version
        try
        {
            var pipVersion = await GetPipVersionAsync(cancellationToken).ConfigureAwait(false);
            results["PipVersion"] = pipVersion;
            results["PipCheck"] = "Success";
        }
        catch (Exception ex)
        {
            results["PipCheck"] = "Failed";
            results["PipError"] = ex.Message;
        }

        // Try a simple command execution
        try
        {
            var testResult = await ExecuteCommandAsync("print('health_check')", cancellationToken: cancellationToken).ConfigureAwait(false);
            results["CommandExecution"] = testResult.ExitCode == 0 ? "Success" : "Failed";
            results["CommandExitCode"] = testResult.ExitCode;
        }
        catch (Exception ex)
        {
            results["CommandExecution"] = "Failed";
            results["CommandError"] = ex.Message;
        }

        // Overall health status
        var allChecks = new[] { "ExecutableExists", "WorkingDirectoryExists", "PythonVersionCheck", "PipCheck", "CommandExecution" };
        var allPassed = allChecks.All(key => 
            results.TryGetValue(key, out var value) && 
            (value is bool boolVal && boolVal) || 
            (value is string strVal && strVal == "Success"));

        results["OverallHealth"] = allPassed ? "Healthy" : "Unhealthy";

        return results;
    }

    /// <summary>
    /// Performs a comprehensive health check (synchronous version).
    /// </summary>
    public Dictionary<string, object> ValidatePythonInstallation()
    {
        Task<Dictionary<string, object>> task = ValidatePythonInstallationAsync();
        task.Wait();
        return task.Result;
    }

    /// <summary>
    /// Executes a Python command and returns the result.
    /// </summary>
    /// <param name="command">The Python command to execute (e.g., "-c 'print(\"Hello\")'").</param>
    /// <param name="stdinHandler">Optional handler for providing stdin input line by line. Return null to end input.</param>
    /// <param name="stdoutHandler">Optional handler for processing stdout output line by line.</param>
    /// <param name="stderrHandler">Optional handler for processing stderr output line by line.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <param name="workingDirectory">Optional working directory override.</param>
    /// <param name="environmentVariables">Optional environment variables to set for the execution.</param>
    /// <param name="priority">Optional process priority.</param>
    /// <param name="maxMemoryMB">Optional maximum memory limit in MB (note: requires process access which is abstracted).</param>
    /// <param name="timeout">Optional per-execution timeout (in addition to CancellationToken).</param>
    /// <returns>The execution result containing exit code, stdout, and stderr.</returns>
    public async Task<PythonExecutionResult> ExecuteCommandAsync(
        string command,
        Func<string?>? stdinHandler = null,
        Action<string>? stdoutHandler = null,
        Action<string>? stderrHandler = null,
        CancellationToken cancellationToken = default,
        string? workingDirectory = null,
        Dictionary<string, string>? environmentVariables = null,
        ProcessPriorityClass? priority = null,
        int? maxMemoryMB = null,
        TimeSpan? timeout = null)
    {
        if (string.IsNullOrWhiteSpace(command))
            throw new ArgumentException("Command cannot be null or empty.", nameof(command));

        ValidateInstallation();

        this.Logger?.LogInformation("Executing Python command: {Command}", command);

        var result = await ExecuteProcessAsync(
            ["-c", command],
            stdinHandler,
            stdoutHandler,
            stderrHandler,
            cancellationToken,
            workingDirectory,
            environmentVariables,
            priority,
            maxMemoryMB,
            timeout).ConfigureAwait(false);

        this.Logger?.LogDebug("Command execution completed with exit code: {ExitCode}", result.ExitCode);

        return result;
    }

    /// <summary>
    /// Executes a Python command and returns the result (synchronous version).
    /// </summary>
    /// <param name="command">The Python command to execute (e.g., "-c 'print(\"Hello\")'").</param>
    /// <param name="stdinHandler">Optional handler for providing stdin input line by line. Return null to end input.</param>
    /// <param name="stdoutHandler">Optional handler for processing stdout output line by line.</param>
    /// <param name="stderrHandler">Optional handler for processing stderr output line by line.</param>
    /// <returns>The execution result containing exit code, stdout, and stderr.</returns>
    public PythonExecutionResult ExecuteCommand(
        string command,
        Func<string?>? stdinHandler = null,
        Action<string>? stdoutHandler = null,
        Action<string>? stderrHandler = null)
    {
        Task<PythonExecutionResult> task = ExecuteCommandAsync(command, stdinHandler, stdoutHandler, stderrHandler);
        task.Wait();
        return task.Result;
    }

    /// <summary>
    /// Executes a Python script file and returns the result.
    /// </summary>
    /// <param name="scriptPath">The path to the Python script file to execute.</param>
    /// <param name="arguments">Optional arguments to pass to the script.</param>
    /// <param name="stdinHandler">Optional handler for providing stdin input line by line. Return null to end input.</param>
    /// <param name="stdoutHandler">Optional handler for processing stdout output line by line.</param>
    /// <param name="stderrHandler">Optional handler for processing stderr output line by line.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <param name="workingDirectory">Optional working directory override.</param>
    /// <param name="environmentVariables">Optional environment variables to set for the execution.</param>
    /// <param name="priority">Optional process priority.</param>
    /// <param name="maxMemoryMB">Optional maximum memory limit in MB (note: requires process access which is abstracted).</param>
    /// <param name="timeout">Optional per-execution timeout (in addition to CancellationToken).</param>
    /// <returns>The execution result containing exit code, stdout, and stderr.</returns>
    public async Task<PythonExecutionResult> ExecuteScriptAsync(
        string scriptPath,
        IEnumerable<string>? arguments = null,
        Func<string?>? stdinHandler = null,
        Action<string>? stdoutHandler = null,
        Action<string>? stderrHandler = null,
        CancellationToken cancellationToken = default,
        string? workingDirectory = null,
        Dictionary<string, string>? environmentVariables = null,
        ProcessPriorityClass? priority = null,
        int? maxMemoryMB = null,
        TimeSpan? timeout = null)
    {
        if (string.IsNullOrWhiteSpace(scriptPath))
            throw new ArgumentException("Script path cannot be null or empty.", nameof(scriptPath));

        if (!File.Exists(scriptPath))
            throw new FileNotFoundException($"Script file not found: {scriptPath}", scriptPath);

        ValidateInstallation();

        var args = new List<string> { scriptPath };
        if (arguments != null)
        {
            args.AddRange(arguments);
        }

        this.Logger?.LogInformation("Executing Python script: {ScriptPath} with arguments: {Arguments}",
            scriptPath, string.Join(" ", args.Skip(1)));

        var result = await ExecuteProcessAsync(
            args,
            stdinHandler,
            stdoutHandler,
            stderrHandler,
            cancellationToken,
            workingDirectory,
            environmentVariables,
            priority,
            maxMemoryMB,
            timeout).ConfigureAwait(false);

        this.Logger?.LogDebug("Script execution completed with exit code: {ExitCode}", result.ExitCode);

        return result;
    }

    /// <summary>
    /// Executes a Python script file and returns the result (synchronous version).
    /// </summary>
    /// <param name="scriptPath">The path to the Python script file to execute.</param>
    /// <param name="arguments">Optional arguments to pass to the script.</param>
    /// <param name="stdinHandler">Optional handler for providing stdin input line by line. Return null to end input.</param>
    /// <param name="stdoutHandler">Optional handler for processing stdout output line by line.</param>
    /// <param name="stderrHandler">Optional handler for processing stderr output line by line.</param>
    /// <returns>The execution result containing exit code, stdout, and stderr.</returns>
    public PythonExecutionResult ExecuteScript(
        string scriptPath,
        IEnumerable<string>? arguments = null,
        Func<string?>? stdinHandler = null,
        Action<string>? stdoutHandler = null,
        Action<string>? stderrHandler = null)
    {
        Task<PythonExecutionResult> task = ExecuteScriptAsync(scriptPath, arguments, stdinHandler, stdoutHandler, stderrHandler);
        task.Wait();
        return task.Result;
    }

    /// <summary>
    /// Installs a Python package using pip or uv.
    /// </summary>
    /// <param name="packageSpecification">The package specification (e.g., "numpy", "torch==2.0.0", "numpy>=1.20.0").</param>
    /// <param name="upgrade">Whether to upgrade the package if it's already installed.</param>
    /// <param name="indexUrl">Optional custom PyPI index URL to use for this installation.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The execution result from uv.</returns>
    public async Task<PythonExecutionResult> InstallPackageAsync(
        string packageSpecification,
        bool upgrade = false,
        string? indexUrl = null,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(packageSpecification))
            throw new ArgumentException("Package specification cannot be null or empty.", nameof(packageSpecification));

        ValidateInstallation();
        ValidateUvAvailable();

        this.Logger?.LogInformation("Installing Python package: {Package}", packageSpecification);

        var uvArgs = new List<string> { "pip", "install" };

        if (upgrade)
            uvArgs.Add("--upgrade");

        if (!string.IsNullOrWhiteSpace(indexUrl))
        {
            uvArgs.Add("--index-url");
            uvArgs.Add(indexUrl);
        }

        uvArgs.Add("--python");
        uvArgs.Add(PythonExecutablePath);
        uvArgs.Add(packageSpecification);

        var result = await ExecuteUvAsync(uvArgs, cancellationToken).ConfigureAwait(false);

        if (result.ExitCode != 0)
        {
            throw new PackageInstallationException(
                $"Failed to install package '{packageSpecification}'. Exit code: {result.ExitCode}")
            {
                PackageSpecification = packageSpecification,
                InstallationOutput = result.StandardOutput + result.StandardError
            };
        }

        this.Logger?.LogInformation("Successfully installed package: {Package}", packageSpecification);
        return result;
    }

    /// <summary>
    /// Installs a Python package (synchronous version).
    /// </summary>
    /// <param name="packageSpecification">The package specification (e.g., "numpy", "torch==2.0.0", "numpy>=1.20.0").</param>
    /// <param name="upgrade">Whether to upgrade the package if it's already installed.</param>
    /// <param name="indexUrl">Optional custom PyPI index URL to use for this installation.</param>
    /// <returns>The execution result from uv.</returns>
    public PythonExecutionResult InstallPackage(
        string packageSpecification,
        bool upgrade = false,
        string? indexUrl = null)
    {
        Task<PythonExecutionResult> task = InstallPackageAsync(packageSpecification, upgrade, indexUrl);
        task.Wait();
        return task.Result;
    }

    /// <summary>
    /// Installs Python packages from a requirements.txt file.
    /// </summary>
    /// <param name="requirementsFilePath">The path to the requirements.txt file.</param>
    /// <param name="upgrade">Whether to upgrade packages if they're already installed.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The execution result from uv.</returns>
    public async Task<PythonExecutionResult> InstallRequirementsAsync(
        string requirementsFilePath,
        bool upgrade = false,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(requirementsFilePath))
            throw new ArgumentException("Requirements file path cannot be null or empty.", nameof(requirementsFilePath));

        if (!File.Exists(requirementsFilePath))
            throw new FileNotFoundException($"Requirements file not found: {requirementsFilePath}", requirementsFilePath);

        ValidateInstallation();
        ValidateUvAvailable();

        this.Logger?.LogInformation("Installing packages from requirements file: {FilePath}", requirementsFilePath);

        var uvArgs = new List<string> { "pip", "install", "-r", requirementsFilePath };

        if (upgrade)
            uvArgs.Add("--upgrade");

        uvArgs.Add("--python");
        uvArgs.Add(PythonExecutablePath);

        var result = await ExecuteUvAsync(uvArgs, cancellationToken).ConfigureAwait(false);

        if (result.ExitCode != 0)
        {
            throw new RequirementsFileException(
                $"Failed to install packages from requirements file '{requirementsFilePath}'. Exit code: {result.ExitCode}")
            {
                PackageSpecification = requirementsFilePath,
                InstallationOutput = result.StandardOutput + result.StandardError
            };
        }

        this.Logger?.LogInformation("Successfully installed packages from requirements file: {FilePath}", requirementsFilePath);
        return result;
    }

    /// <summary>
    /// Installs Python packages from a requirements.txt file (synchronous version).
    /// </summary>
    /// <param name="requirementsFilePath">The path to the requirements.txt file.</param>
    /// <param name="upgrade">Whether to upgrade packages if they're already installed.</param>
    /// <returns>The execution result from uv.</returns>
    public PythonExecutionResult InstallRequirements(
        string requirementsFilePath,
        bool upgrade = false)
    {
        Task<PythonExecutionResult> task = InstallRequirementsAsync(requirementsFilePath, upgrade);
        task.Wait();
        return task.Result;
    }

    /// <summary>
    /// Installs a Python package from a pyproject.toml file.
    /// </summary>
    /// <param name="pyProjectFilePath">The path to the directory containing pyproject.toml or the pyproject.toml file itself.</param>
    /// <param name="editable">Whether to install in editable mode (-e).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The execution result from uv.</returns>
    public async Task<PythonExecutionResult> InstallPyProjectAsync(
        string pyProjectFilePath,
        bool editable = false,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(pyProjectFilePath))
            throw new ArgumentException("PyProject file path cannot be null or empty.", nameof(pyProjectFilePath));

        // Determine the directory containing pyproject.toml
        string projectDirectory;
        if (Directory.Exists(pyProjectFilePath))
        {
            projectDirectory = pyProjectFilePath;
        }
        else if (File.Exists(pyProjectFilePath))
        {
            projectDirectory = Path.GetDirectoryName(pyProjectFilePath) ?? throw new ArgumentException(
                $"Invalid path: {pyProjectFilePath}", nameof(pyProjectFilePath));
        }
        else
        {
            throw new FileNotFoundException($"PyProject file or directory not found: {pyProjectFilePath}", pyProjectFilePath);
        }

        string pyProjectTomlPath = Path.Combine(projectDirectory, "pyproject.toml");
        if (!File.Exists(pyProjectTomlPath))
        {
            throw new FileNotFoundException($"pyproject.toml not found in directory: {projectDirectory}", pyProjectTomlPath);
        }

        ValidateInstallation();
        ValidateUvAvailable();

        this.Logger?.LogInformation("Installing package from pyproject.toml: {Path} (editable: {Editable})",
            pyProjectTomlPath, editable);

        var uvArgs = new List<string> { "pip", "install" };
        if (editable)
        {
            uvArgs.Add("-e");
        }
        uvArgs.Add(projectDirectory);
        uvArgs.Add("--python");
        uvArgs.Add(PythonExecutablePath);

        var result = await ExecuteUvAsync(uvArgs, cancellationToken, workingDirectory: projectDirectory).ConfigureAwait(false);

        if (result.ExitCode != 0)
        {
            throw new PackageInstallationException(
                $"Failed to install package from pyproject.toml '{pyProjectTomlPath}'. Exit code: {result.ExitCode}")
            {
                PackageSpecification = pyProjectTomlPath,
                InstallationOutput = result.StandardOutput + result.StandardError
            };
        }

        this.Logger?.LogInformation("Successfully installed package from pyproject.toml: {Path}", pyProjectTomlPath);
        return result;
    }

    /// <summary>
    /// Installs a Python package from a pyproject.toml file (synchronous version).
    /// </summary>
    /// <param name="pyProjectFilePath">The path to the directory containing pyproject.toml or the pyproject.toml file itself.</param>
    /// <param name="editable">Whether to install in editable mode (-e).</param>
    /// <returns>The execution result from uv.</returns>
    public PythonExecutionResult InstallPyProject(
        string pyProjectFilePath,
        bool editable = false)
    {
        Task<PythonExecutionResult> task = InstallPyProjectAsync(pyProjectFilePath, editable);
        task.Wait();
        return task.Result;
    }

    /// <summary>
    /// Lists all installed packages with their versions.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A list of installed packages with their versions.</returns>
    public async Task<IReadOnlyList<Models.PackageInfo>> ListInstalledPackagesAsync(
        CancellationToken cancellationToken = default)
    {
        ValidateInstallation();
        ValidateUvAvailable();

        this.Logger?.LogDebug("Listing installed packages");

        var uvArgs = new List<string> { "pip", "list", "--format=json", "--python", PythonExecutablePath };
        var result = await ExecuteUvAsync(uvArgs, cancellationToken).ConfigureAwait(false);

        if (result.ExitCode != 0)
        {
            throw new Exceptions.PackageInstallationException(
                $"Failed to list installed packages. Exit code: {result.ExitCode}")
            {
                InstallationOutput = result.StandardOutput + result.StandardError
            };
        }

        var packages = new List<Models.PackageInfo>();
        try
        {
            var jsonPackages = System.Text.Json.JsonSerializer.Deserialize<List<Dictionary<string, string>>>(result.StandardOutput);
            if (jsonPackages != null)
            {
                foreach (var pkg in jsonPackages)
                {
                    var name = pkg.TryGetValue("name", out var nameVal) ? nameVal ?? "" : "";
                    var version = pkg.TryGetValue("version", out var versionVal) ? versionVal ?? "" : "";
                    packages.Add(new Models.PackageInfo(name, version));
                }
            }
        }
        catch (Exception ex)
        {
            this.Logger?.LogWarning(ex, "Failed to parse package list JSON output");
        }

        return packages.AsReadOnly();
    }

    /// <summary>
    /// Lists all installed packages (synchronous version).
    /// </summary>
    public IReadOnlyList<Models.PackageInfo> ListInstalledPackages()
    {
        Task<IReadOnlyList<Models.PackageInfo>> task = ListInstalledPackagesAsync();
        task.Wait();
        return task.Result;
    }

    /// <summary>
    /// Gets the version of a specific installed package.
    /// </summary>
    /// <param name="packageName">The name of the package.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The package version if installed, null otherwise.</returns>
    public async Task<string?> GetPackageVersionAsync(
        string packageName,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(packageName))
            throw new ArgumentException("Package name cannot be null or empty.", nameof(packageName));

        ValidateInstallation();
        ValidateUvAvailable();

        this.Logger?.LogDebug("Getting package version: {Package}", packageName);

        var uvArgs = new List<string> { "pip", "show", packageName, "--python", PythonExecutablePath };
        var result = await ExecuteUvAsync(uvArgs, cancellationToken).ConfigureAwait(false);

        if (result.ExitCode != 0)
            return null; // Package not found

        // Parse "Version: x.y.z" from output
        foreach (var line in result.StandardOutput.Split('\n', StringSplitOptions.RemoveEmptyEntries))
        {
            if (line.StartsWith("Version:", StringComparison.OrdinalIgnoreCase))
            {
                return line.Substring("Version:".Length).Trim();
            }
        }

        return null;
    }

    /// <summary>
    /// Gets the version of a specific installed package (synchronous version).
    /// </summary>
    public string? GetPackageVersion(string packageName)
    {
        Task<string?> task = GetPackageVersionAsync(packageName);
        task.Wait();
        return task.Result;
    }

    /// <summary>
    /// Checks if a package is installed.
    /// </summary>
    /// <param name="packageName">The name of the package.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>True if the package is installed, false otherwise.</returns>
    public async Task<bool> IsPackageInstalledAsync(
        string packageName,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(packageName))
            throw new ArgumentException("Package name cannot be null or empty.", nameof(packageName));

        ValidateInstallation();
        ValidateUvAvailable();

        this.Logger?.LogDebug("Checking if package is installed: {Package}", packageName);

        var uvArgs = new List<string> { "pip", "show", packageName, "--python", PythonExecutablePath };
        var result = await ExecuteUvAsync(uvArgs, cancellationToken).ConfigureAwait(false);

        return result.ExitCode == 0;
    }

    /// <summary>
    /// Checks if a package is installed (synchronous version).
    /// </summary>
    public bool IsPackageInstalled(string packageName)
    {
        Task<bool> task = IsPackageInstalledAsync(packageName);
        task.Wait();
        return task.Result;
    }

    /// <summary>
    /// Gets detailed information about an installed package.
    /// </summary>
    /// <param name="packageName">The name of the package.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Package information if installed, null otherwise.</returns>
    public async Task<Models.PackageInfo?> GetPackageInfoAsync(string packageName, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(packageName))
            throw new ArgumentException("Package name cannot be null or empty.", nameof(packageName));

        ValidateInstallation();
        ValidateUvAvailable();

        this.Logger?.LogDebug("Getting package info: {Package}", packageName);

        var uvArgs = new List<string> { "pip", "show", packageName, "--python", PythonExecutablePath };
        var result = await ExecuteUvAsync(uvArgs, cancellationToken).ConfigureAwait(false);

        if (result.ExitCode != 0)
            return null; // Package not found

        string? name = null;
        string? version = null;
        string? location = null;
        string? summary = null;

        foreach (var line in result.StandardOutput.Split('\n', StringSplitOptions.RemoveEmptyEntries))
        {
            var trimmed = line.Trim();
            if (trimmed.StartsWith("Name:", StringComparison.OrdinalIgnoreCase))
                name = trimmed.Substring("Name:".Length).Trim();
            else if (trimmed.StartsWith("Version:", StringComparison.OrdinalIgnoreCase))
                version = trimmed.Substring("Version:".Length).Trim();
            else if (trimmed.StartsWith("Location:", StringComparison.OrdinalIgnoreCase))
                location = trimmed.Substring("Location:".Length).Trim();
            else if (trimmed.StartsWith("Summary:", StringComparison.OrdinalIgnoreCase))
                summary = trimmed.Substring("Summary:".Length).Trim();
        }

        if (name != null && version != null)
        {
            return new Models.PackageInfo(name, version, location, summary);
        }

        return null;
    }

    /// <summary>
    /// Gets detailed information about an installed package (synchronous version).
    /// </summary>
    public Models.PackageInfo? GetPackageInfo(string packageName)
    {
        Task<Models.PackageInfo?> task = GetPackageInfoAsync(packageName);
        task.Wait();
        return task.Result;
    }

    /// <summary>
    /// Determines which packages from a list are not installed.
    /// Uses importlib.util.find_spec for fast checking without loading pip.
    /// </summary>
    /// <param name="packageNames">The names of packages to check.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>An array of package names that are not installed.</returns>
    /// <remarks>
    /// Performance: Single Python invocation for all packages, ~100-300ms total.
    /// Useful for checking dependencies before installation.
    /// </remarks>
    public async Task<string[]> GetMissingPackagesAsync(string[] packageNames, CancellationToken cancellationToken = default)
    {
        if (packageNames == null || packageNames.Length == 0)
            return [];

        ValidateInstallation();

        this.Logger?.LogDebug("Checking for missing packages: {Packages}", string.Join(", ", packageNames));

        // Escape package names and convert hyphens to underscores for module lookup
        var escapedPackages = packageNames
            .Select(p => p.Replace("'", "\\'").Replace("-", "_").ToLowerInvariant())
            .ToArray();

        var packageList = string.Join("','", escapedPackages);

        var script = $@"
import importlib.util
required = ['{packageList}']
missing = [r for r in required if importlib.util.find_spec(r) is None]
print(','.join(missing) if missing else '')
".Trim();

        var result = await ExecuteCommandAsync(script, cancellationToken: cancellationToken).ConfigureAwait(false);

        if (result.ExitCode != 0)
        {
            this.Logger?.LogWarning("Failed to check missing packages, assuming all are missing. Error: {Error}", result.StandardError);
            return packageNames; // Assume all missing on error
        }

        var output = result.StandardOutput.Trim();
        if (string.IsNullOrWhiteSpace(output))
            return []; // All packages are installed

        // Map back from underscore names to original names
        var missingModules = output.Split(',', StringSplitOptions.RemoveEmptyEntries);
        var missingPackages = new List<string>();

        foreach (var originalName in packageNames)
        {
            var moduleName = originalName.Replace("-", "_").ToLowerInvariant();
            if (missingModules.Contains(moduleName, StringComparer.OrdinalIgnoreCase))
            {
                missingPackages.Add(originalName);
            }
        }

        return missingPackages.ToArray();
    }

    /// <summary>
    /// Determines which packages from a list are not installed (synchronous version).
    /// </summary>
    public string[] GetMissingPackages(string[] packageNames)
    {
        Task<string[]> task = GetMissingPackagesAsync(packageNames);
        task.Wait();
        return task.Result;
    }

    /// <summary>
    /// Uninstalls a Python package.
    /// </summary>
    /// <param name="packageName">The name of the package to uninstall.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The execution result from uv.</returns>
    public async Task<PythonExecutionResult> UninstallPackageAsync(
        string packageName,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(packageName))
            throw new ArgumentException("Package name cannot be null or empty.", nameof(packageName));

        ValidateInstallation();
        ValidateUvAvailable();

        this.Logger?.LogInformation("Uninstalling Python package: {Package}", packageName);

        var uvArgs = new List<string> { "pip", "uninstall", packageName };
        uvArgs.Add("--python");
        uvArgs.Add(PythonExecutablePath);

        var result = await ExecuteUvAsync(uvArgs, cancellationToken).ConfigureAwait(false);

        if (result.ExitCode != 0)
        {
            throw new Exceptions.PackageInstallationException(
                $"Failed to uninstall package '{packageName}'. Exit code: {result.ExitCode}")
            {
                PackageSpecification = packageName,
                InstallationOutput = result.StandardOutput + result.StandardError
            };
        }

        this.Logger?.LogInformation("Successfully uninstalled package: {Package}", packageName);
        return result;
    }

    /// <summary>
    /// Uninstalls a Python package (synchronous version).
    /// </summary>
    public PythonExecutionResult UninstallPackage(string packageName)
    {
        Task<PythonExecutionResult> task = UninstallPackageAsync(packageName);
        task.Wait();
        return task.Result;
    }

    /// <summary>
    /// Lists packages that have available updates.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A list of outdated packages with their current and latest versions.</returns>
    public async Task<IReadOnlyList<Models.OutdatedPackageInfo>> ListOutdatedPackagesAsync(
        CancellationToken cancellationToken = default)
    {
        ValidateInstallation();
        ValidateUvAvailable();

        this.Logger?.LogDebug("Listing outdated packages");

        var uvArgs = new List<string> { "pip", "list", "--outdated", "--format=json", "--python", PythonExecutablePath };
        var result = await ExecuteUvAsync(uvArgs, cancellationToken).ConfigureAwait(false);

        if (result.ExitCode != 0)
        {
            throw new Exceptions.PackageInstallationException(
                $"Failed to list outdated packages. Exit code: {result.ExitCode}")
            {
                InstallationOutput = result.StandardOutput + result.StandardError
            };
        }

        return ParseOutdatedPackagesJson(result.StandardOutput);
    }

    /// <summary>
    /// Parses JSON output from uv list --outdated.
    /// </summary>
    private IReadOnlyList<Models.OutdatedPackageInfo> ParseOutdatedPackagesJson(string jsonOutput)
    {
        var outdatedPackages = new List<Models.OutdatedPackageInfo>();
        try
        {
            var jsonPackages = System.Text.Json.JsonSerializer.Deserialize<List<Dictionary<string, object>>>(jsonOutput);
            if (jsonPackages != null)
            {
                foreach (var pkg in jsonPackages)
                {
                    var name = pkg.TryGetValue("name", out var nameObj) ? nameObj?.ToString() ?? "" : "";
                    var installedVersion = pkg.TryGetValue("version", out var versionObj) ? versionObj?.ToString() ?? "" : "";
                    var latestVersion = pkg.TryGetValue("latest_version", out var latestObj) ? latestObj?.ToString() ?? "" : "";
                    outdatedPackages.Add(new Models.OutdatedPackageInfo(name, installedVersion, latestVersion));
                }
            }
        }
        catch (Exception ex)
        {
            // Fallback: parse legacy format
            foreach (var line in jsonOutput.Split('\n', StringSplitOptions.RemoveEmptyEntries))
            {
                var trimmed = line.Trim();
                if (string.IsNullOrWhiteSpace(trimmed) || trimmed.StartsWith("Package", StringComparison.OrdinalIgnoreCase) || trimmed.StartsWith("---", StringComparison.OrdinalIgnoreCase))
                    continue;

                var parts = trimmed.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length >= 3)
                {
                    outdatedPackages.Add(new Models.OutdatedPackageInfo(parts[0], parts[1], parts[2]));
                }
            }
            
            this.Logger?.LogWarning(ex, "Failed to parse list --outdated JSON output, used fallback parser");
        }

        return outdatedPackages.AsReadOnly();
    }

    /// <summary>
    /// Lists packages that have available updates (synchronous version).
    /// </summary>
    public IReadOnlyList<Models.OutdatedPackageInfo> ListOutdatedPackages()
    {
        Task<IReadOnlyList<Models.OutdatedPackageInfo>> task = ListOutdatedPackagesAsync();
        task.Wait();
        return task.Result;
    }

    /// <summary>
    /// Upgrades all installed packages to their latest versions.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The execution result from pip.</returns>
    public async Task<PythonExecutionResult> UpgradeAllPackagesAsync(CancellationToken cancellationToken = default)
    {
        ValidateInstallation();

        this.Logger?.LogInformation("Upgrading all installed packages");

        // Get list of outdated packages
        var outdated = await ListOutdatedPackagesAsync(cancellationToken: cancellationToken).ConfigureAwait(false);
        if (!outdated.Any())
        {
            this.Logger?.LogInformation("No packages to upgrade");
            return new PythonExecutionResult(0, "No packages to upgrade", "");
        }

        // Upgrade each package
        var allResults = new List<string>();
        foreach (var pkg in outdated)
        {
            try
            {
                    var result = await InstallPackageAsync(pkg.Name, upgrade: true, indexUrl: null, cancellationToken: cancellationToken).ConfigureAwait(false);
                allResults.Add($"{pkg.Name}: Success");
            }
            catch (Exception ex)
            {
                allResults.Add($"{pkg.Name}: Failed - {ex.Message}");
                this.Logger?.LogWarning(ex, "Failed to upgrade package: {Package}", pkg.Name);
            }
        }

        return new PythonExecutionResult(0, string.Join("\n", allResults), "");
    }

    /// <summary>
    /// Upgrades all installed packages (synchronous version).
    /// </summary>
    public PythonExecutionResult UpgradeAllPackages()
    {
        Task<PythonExecutionResult> task = UpgradeAllPackagesAsync();
        task.Wait();
        return task.Result;
    }

    /// <summary>
    /// Downgrades a package to a specific version.
    /// </summary>
    /// <param name="packageName">The name of the package.</param>
    /// <param name="targetVersion">The target version to downgrade to.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The execution result from pip.</returns>
    public async Task<PythonExecutionResult> DowngradePackageAsync(
        string packageName,
        string targetVersion,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(packageName))
            throw new ArgumentException("Package name cannot be null or empty.", nameof(packageName));
        if (string.IsNullOrWhiteSpace(targetVersion))
            throw new ArgumentException("Target version cannot be null or empty.", nameof(targetVersion));

        ValidateInstallation();

        this.Logger?.LogInformation("Downgrading package {Package} to version {Version}", packageName, targetVersion);

        var packageSpec = $"{packageName}=={targetVersion}";
        return await InstallPackageAsync(packageSpec, upgrade: false, indexUrl: null, cancellationToken: cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Downgrades a package to a specific version (synchronous version).
    /// </summary>
    public PythonExecutionResult DowngradePackage(string packageName, string targetVersion)
    {
        Task<PythonExecutionResult> task = DowngradePackageAsync(packageName, targetVersion);
        task.Wait();
        return task.Result;
    }

    /// <summary>
    /// Exports installed packages to a requirements.txt file (with version constraints).
    /// </summary>
    /// <param name="outputPath">The path where to write the requirements.txt file.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The execution result from uv.</returns>
    public async Task<PythonExecutionResult> ExportRequirementsAsync(
        string outputPath,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(outputPath))
            throw new ArgumentException("Output path cannot be null or empty.", nameof(outputPath));

        var content = await ExportRequirementsFreezeToStringAsync(cancellationToken).ConfigureAwait(false);
        await File.WriteAllTextAsync(outputPath, content, cancellationToken).ConfigureAwait(false);

        this.Logger?.LogInformation("Successfully exported requirements to: {Path}", outputPath);
        return new PythonExecutionResult(0, content, "");
    }

    /// <summary>
    /// Exports installed packages to a requirements.txt file (synchronous version).
    /// </summary>
    public PythonExecutionResult ExportRequirements(string outputPath)
    {
        Task<PythonExecutionResult> task = ExportRequirementsAsync(outputPath);
        task.Wait();
        return task.Result;
    }

    /// <summary>
    /// Exports installed packages as a requirements.txt string (with exact versions using freeze).
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The requirements.txt content as a string.</returns>
    public async Task<string> ExportRequirementsFreezeToStringAsync(
        CancellationToken cancellationToken = default)
    {
        ValidateInstallation();
        ValidateUvAvailable();

        this.Logger?.LogDebug("Exporting requirements");

        var uvArgs = new List<string> { "pip", "freeze", "--python", PythonExecutablePath };
        var result = await ExecuteUvAsync(uvArgs, cancellationToken).ConfigureAwait(false);

        if (result.ExitCode != 0)
        {
            throw new Exceptions.PackageInstallationException(
                $"Failed to export requirements. Exit code: {result.ExitCode}")
            {
                InstallationOutput = result.StandardOutput + result.StandardError
            };
        }

        return result.StandardOutput;
    }

    /// <summary>
    /// Exports installed packages as a requirements.txt string (synchronous version).
    /// </summary>
    public string ExportRequirementsFreezeToString()
    {
        Task<string> task = ExportRequirementsFreezeToStringAsync();
        task.Wait();
        return task.Result;
    }

    /// <summary>
    /// Exports installed packages to a requirements.txt file (with exact versions from freeze).
    /// </summary>
    /// <param name="outputPath">The path where to write the requirements.txt file.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The execution result from uv.</returns>
    public async Task<PythonExecutionResult> ExportRequirementsFreezeAsync(
        string outputPath,
        CancellationToken cancellationToken = default)
    {
        var content = await ExportRequirementsFreezeToStringAsync(cancellationToken).ConfigureAwait(false);
        await File.WriteAllTextAsync(outputPath, content, cancellationToken).ConfigureAwait(false);
        return new PythonExecutionResult(0, content, "");
    }

    /// <summary>
    /// Exports installed packages to a requirements.txt file with exact versions (synchronous version).
    /// </summary>
    public PythonExecutionResult ExportRequirementsFreeze(string outputPath)
    {
        Task<PythonExecutionResult> task = ExportRequirementsFreezeAsync(outputPath);
        task.Wait();
        return task.Result;
    }

    /// <summary>
    /// Installs multiple packages in batch.
    /// </summary>
    /// <param name="packages">The list of package specifications to install.</param>
    /// <param name="parallel">Whether to install packages in parallel.</param>
    /// <param name="upgrade">Whether to upgrade packages if they're already installed.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A dictionary mapping package names to their installation results.</returns>
    public async Task<Dictionary<string, PythonExecutionResult>> InstallPackagesAsync(
        IEnumerable<string> packages,
        bool parallel = false,
        bool upgrade = false,
        CancellationToken cancellationToken = default)
    {
        if (packages == null)
            throw new ArgumentNullException(nameof(packages));

        var packageList = packages.ToList();
        if (packageList.Count == 0)
            return new Dictionary<string, PythonExecutionResult>();

        ValidateInstallation();

        this.Logger?.LogInformation("Installing {Count} packages (parallel: {Parallel})", packageList.Count, parallel);

        var results = new Dictionary<string, PythonExecutionResult>();

        if (parallel)
        {
            var tasks = packageList.Select(async pkg =>
            {
                try
                {
                    var result = await InstallPackageAsync(pkg, upgrade, indexUrl: null, cancellationToken: cancellationToken).ConfigureAwait(false);
                    return (Package: pkg, Result: result);
                }
                catch (Exception ex)
                {
                    this.Logger?.LogWarning(ex, "Failed to install package: {Package}", pkg);
                    return (Package: pkg, Result: new PythonExecutionResult(1, "", ex.Message));
                }
            });

            var completed = await Task.WhenAll(tasks).ConfigureAwait(false);
            foreach (var (pkg, result) in completed)
            {
                results[pkg] = result;
            }
        }
        else
        {
            foreach (var pkg in packageList)
            {
                try
                {
                    results[pkg] = await InstallPackageAsync(pkg, upgrade, indexUrl: null, cancellationToken: cancellationToken).ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    this.Logger?.LogWarning(ex, "Failed to install package: {Package}", pkg);
                    results[pkg] = new PythonExecutionResult(1, "", ex.Message);
                }
            }
        }

        return results;
    }

    /// <summary>
    /// Installs multiple packages in batch (synchronous version).
    /// </summary>
    public Dictionary<string, PythonExecutionResult> InstallPackages(IEnumerable<string> packages, bool parallel = false, bool upgrade = false)
    {
        Task<Dictionary<string, PythonExecutionResult>> task = InstallPackagesAsync(packages, parallel, upgrade);
        task.Wait();
        return task.Result;
    }

    /// <summary>
    /// Uninstalls multiple packages in batch.
    /// </summary>
    /// <param name="packages">The list of package names to uninstall.</param>
    /// <param name="parallel">Whether to uninstall packages in parallel.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A dictionary mapping package names to their uninstallation results.</returns>
    public async Task<Dictionary<string, PythonExecutionResult>> UninstallPackagesAsync(
        IEnumerable<string> packages,
        bool parallel = false,
        CancellationToken cancellationToken = default)
    {
        if (packages == null)
            throw new ArgumentNullException(nameof(packages));

        var packageList = packages.ToList();
        if (packageList.Count == 0)
            return new Dictionary<string, PythonExecutionResult>();

        ValidateInstallation();

        this.Logger?.LogInformation("Uninstalling {Count} packages (parallel: {Parallel})", packageList.Count, parallel);

        var results = new Dictionary<string, PythonExecutionResult>();

        if (parallel)
        {
            var tasks = packageList.Select(async pkg =>
            {
                try
                {
                    var result = await UninstallPackageAsync(pkg, cancellationToken).ConfigureAwait(false);
                    return (pkg, result);
                }
                catch (Exception ex)
                {
                    this.Logger?.LogWarning(ex, "Failed to uninstall package: {Package}", pkg);
                    return (pkg, new PythonExecutionResult(1, "", ex.Message));
                }
            });

            var completed = await Task.WhenAll(tasks).ConfigureAwait(false);
            foreach (var (pkg, result) in completed)
            {
                results[pkg] = result;
            }
        }
        else
        {
            foreach (var pkg in packageList)
            {
                try
                {
                    results[pkg] = await UninstallPackageAsync(pkg, cancellationToken).ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    this.Logger?.LogWarning(ex, "Failed to uninstall package: {Package}", pkg);
                    results[pkg] = new PythonExecutionResult(1, "", ex.Message);
                }
            }
        }

        return results;
    }

    /// <summary>
    /// Uninstalls multiple packages in batch (synchronous version).
    /// </summary>
    public Dictionary<string, PythonExecutionResult> UninstallPackages(IEnumerable<string> packages, bool parallel = false)
    {
        Task<Dictionary<string, PythonExecutionResult>> task = UninstallPackagesAsync(packages, parallel);
        task.Wait();
        return task.Result;
    }

    /// <summary>
    /// Gets the pip version.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The pip version string.</returns>
    public async Task<string> GetPipVersionAsync(CancellationToken cancellationToken = default)
    {
        ValidateInstallation();

        var result = await ExecuteProcessAsync(
            ["-m", "pip", "--version"],
            null, null, null,
            cancellationToken, null, null).ConfigureAwait(false);

        if (result.ExitCode != 0)
        {
            throw new Exceptions.PackageInstallationException(
                $"Failed to get pip version. Exit code: {result.ExitCode}")
            {
                InstallationOutput = result.StandardOutput + result.StandardError
            };
        }

        // Extract version from "pip x.y.z from ..." format
        var output = result.StandardOutput.Trim();
        var parts = output.Split(' ');
        if (parts.Length >= 2)
        {
            return parts[1];
        }

        return output;
    }

    /// <summary>
    /// Gets the pip version (synchronous version).
    /// </summary>
    public string GetPipVersion()
    {
        Task<string> task = GetPipVersionAsync();
        task.Wait();
        return task.Result;
    }

    /// <summary>
    /// Gets detailed Python version information.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Python version information string.</returns>
    public async Task<string> GetPythonVersionInfoAsync(CancellationToken cancellationToken = default)
    {
        ValidateInstallation();

        var result = await ExecuteProcessAsync(
            ["-c", "import sys; print(sys.version); print(sys.executable)"],
            null, null, null,
            cancellationToken, null, null).ConfigureAwait(false);

        if (result.ExitCode != 0)
        {
            throw new Exceptions.PythonExecutionException(
                $"Failed to get Python version info. Exit code: {result.ExitCode}")
            {
                ExitCode = result.ExitCode,
                StandardError = result.StandardError
            };
        }

        return result.StandardOutput;
    }

    /// <summary>
    /// Gets detailed Python version information (synchronous version).
    /// </summary>
    public string GetPythonVersionInfo()
    {
        Task<string> task = GetPythonVersionInfoAsync();
        task.Wait();
        return task.Result;
    }

    /// <summary>
    /// Gets the current pip configuration.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Pip configuration information.</returns>
    public async Task<Models.PipConfiguration> GetPipConfigurationAsync(CancellationToken cancellationToken = default)
    {
        ValidateInstallation();

        var result = await ExecuteProcessAsync(
            ["-m", "pip", "config", "list"],
            null, null, null,
            cancellationToken, null, null).ConfigureAwait(false);

        string? indexUrl = null;
        string? trustedHost = null;
        string? proxy = null;

        if (result.ExitCode == 0)
        {
            foreach (var line in result.StandardOutput.Split('\n', StringSplitOptions.RemoveEmptyEntries))
            {
                var trimmed = line.Trim();
                if (trimmed.StartsWith("global.index-url", StringComparison.OrdinalIgnoreCase))
                {
                    var parts = trimmed.Split('=');
                    if (parts.Length >= 2)
                        indexUrl = parts[1].Trim();
                }
                else if (trimmed.StartsWith("global.trusted-host", StringComparison.OrdinalIgnoreCase))
                {
                    var parts = trimmed.Split('=');
                    if (parts.Length >= 2)
                        trustedHost = parts[1].Trim();
                }
                else if (trimmed.StartsWith("global.proxy", StringComparison.OrdinalIgnoreCase))
                {
                    var parts = trimmed.Split('=');
                    if (parts.Length >= 2)
                        proxy = parts[1].Trim();
                }
            }
        }

        return new Models.PipConfiguration(indexUrl, trustedHost, proxy);
    }

    /// <summary>
    /// Gets the current pip configuration (synchronous version).
    /// </summary>
    public Models.PipConfiguration GetPipConfiguration()
    {
        Task<Models.PipConfiguration> task = GetPipConfigurationAsync();
        task.Wait();
        return task.Result;
    }

    /// <summary>
    /// Configures pip to use a custom index URL.
    /// </summary>
    /// <param name="indexUrl">The index URL to use.</param>
    /// <param name="trusted">Whether to mark the host as trusted.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The execution result from pip.</returns>
    public async Task<PythonExecutionResult> ConfigurePipIndexAsync(
        string indexUrl,
        bool trusted = false,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(indexUrl))
            throw new ArgumentException("Index URL cannot be null or empty.", nameof(indexUrl));

        ValidateInstallation();

        this.Logger?.LogInformation("Configuring pip index URL: {IndexUrl}", indexUrl);

        var pipArgs = new List<string> { "-m", "pip", "config", "set", "global.index-url", indexUrl };
        var result = await ExecuteProcessAsync(pipArgs, null, null, null, cancellationToken, null, null).ConfigureAwait(false);

        if (result.ExitCode == 0 && trusted)
        {
            // Extract host from URL for trusted-host configuration
            try
            {
                var uri = new Uri(indexUrl);
                var host = uri.Host;
                var trustedArgs = new List<string> { "-m", "pip", "config", "set", "global.trusted-host", host };
                await ExecuteProcessAsync(trustedArgs, null, null, null, cancellationToken, null, null).ConfigureAwait(false);
            }
            catch (UriFormatException)
            {
                this.Logger?.LogWarning("Could not parse index URL for trusted-host configuration: {IndexUrl}", indexUrl);
            }
        }

        return result;
    }

    /// <summary>
    /// Configures pip to use a custom index URL (synchronous version).
    /// </summary>
    public PythonExecutionResult ConfigurePipIndex(string indexUrl, bool trusted = false)
    {
        Task<PythonExecutionResult> task = ConfigurePipIndexAsync(indexUrl, trusted);
        task.Wait();
        return task.Result;
    }

    /// <summary>
    /// Configures pip proxy settings.
    /// </summary>
    /// <param name="proxyUrl">The proxy URL to use.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The execution result from pip.</returns>
    public async Task<PythonExecutionResult> ConfigurePipProxyAsync(
        string proxyUrl,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(proxyUrl))
            throw new ArgumentException("Proxy URL cannot be null or empty.", nameof(proxyUrl));

        ValidateInstallation();

        this.Logger?.LogInformation("Configuring pip proxy: {ProxyUrl}", proxyUrl);

        var pipArgs = new List<string> { "-m", "pip", "config", "set", "global.proxy", proxyUrl };
        var result = await ExecuteProcessAsync(pipArgs, null, null, null, cancellationToken, null, null).ConfigureAwait(false);

        return result;
    }

    /// <summary>
    /// Configures pip proxy settings (synchronous version).
    /// </summary>
    public PythonExecutionResult ConfigurePipProxy(string proxyUrl)
    {
        Task<PythonExecutionResult> task = ConfigurePipProxyAsync(proxyUrl);
        task.Wait();
        return task.Result;
    }

    /// <summary>
    /// Validates that a Python version string is in a valid format.
    /// </summary>
    /// <param name="version">The version string to validate.</param>
    /// <returns>True if the version string is valid, false otherwise.</returns>
    public static bool ValidatePythonVersionString(string version)
    {
        if (string.IsNullOrWhiteSpace(version))
            return false;

        // Basic validation: should match pattern like "3.12.0", "3.12", "3", etc.
        return System.Text.RegularExpressions.Regex.IsMatch(version.Trim(), @"^\d+(\.\d+)*$");
    }

    /// <summary>
    /// Validates that a package specification is in a valid format.
    /// </summary>
    /// <param name="packageSpec">The package specification to validate.</param>
    /// <returns>True if the package specification is valid, false otherwise.</returns>
    public static bool ValidatePackageSpecification(string packageSpec)
    {
        if (string.IsNullOrWhiteSpace(packageSpec))
            return false;

        // Basic validation: package name should be valid
        // Package specs can be: "package", "package==1.0", "package>=1.0", etc.
        var trimmed = packageSpec.Trim();
        if (trimmed.Length == 0)
            return false;

        // Check for version specifiers
        var versionSpecifiers = new[] { "==", ">=", "<=", ">", "<", "~=", "!=" };
        foreach (var spec in versionSpecifiers)
        {
            if (trimmed.Contains(spec, StringComparison.OrdinalIgnoreCase))
            {
                var parts = trimmed.Split(new[] { spec }, StringSplitOptions.None);
                if (parts.Length != 2 || string.IsNullOrWhiteSpace(parts[0]) || string.IsNullOrWhiteSpace(parts[1]))
                    return false;
                return parts[0].Trim().Length > 0 && parts[1].Trim().Length > 0;
            }
        }

        // Just a package name
        return trimmed.Length > 0;
    }

    /// <summary>
    /// Searches PyPI for packages matching the query.
    /// </summary>
    /// <param name="query">The search query.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A list of search results.</returns>
    public async Task<IReadOnlyList<Models.PyPISearchResult>> SearchPackagesAsync(
        string query,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(query))
            throw new ArgumentException("Search query cannot be null or empty.", nameof(query));

        ValidateInstallation();

        this.Logger?.LogDebug("Searching PyPI for packages: {Query}", query);

        try
        {
            using var httpClient = new System.Net.Http.HttpClient();
            httpClient.Timeout = TimeSpan.FromSeconds(30);
            
            // Use PyPI's JSON API: https://pypi.org/pypi/<package_name>/json
            // For search, we can use: https://pypi.org/search/?q=<query>&c=Programming+Language+%3A%3A+Python&:action=search
            // However, the search endpoint doesn't have a clean JSON API, so we'll use pip search via subprocess
            // Actually, pip search was removed. We'll use the PyPI JSON API directly for individual package lookups
            // For search, we can use the PyPI simple index HTML and parse it, or use an external search API
            
            // Using PyPI's JSON API endpoint (simplified - would need HTML parsing for full search)
            // For now, let's use a simple approach: try to get package info for the exact query
            var searchUrl = $"https://pypi.org/pypi/{Uri.EscapeDataString(query)}/json";
            var response = await httpClient.GetAsync(searchUrl, cancellationToken).ConfigureAwait(false);
            
            if (response.IsSuccessStatusCode)
            {
                var jsonContent = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
                var jsonDoc = System.Text.Json.JsonDocument.Parse(jsonContent);
                
                if (jsonDoc.RootElement.TryGetProperty("info", out var info))
                {
                    var name = info.TryGetProperty("name", out var nameProp) ? nameProp.GetString() ?? query : query;
                    var version = info.TryGetProperty("version", out var versionProp) ? versionProp.GetString() ?? "" : "";
                    var summary = info.TryGetProperty("summary", out var summaryProp) ? summaryProp.GetString() : null;
                    
                    return new[] { new Models.PyPISearchResult(name, version, summary) }.ToList().AsReadOnly();
                }
            }
            
            // If exact match not found, return empty list
            // Note: Full search would require parsing HTML or using a different API
            this.Logger?.LogWarning("Package not found in PyPI: {Query}", query);
            return Array.Empty<Models.PyPISearchResult>().ToList().AsReadOnly();
        }
        catch (Exception ex)
        {
            this.Logger?.LogError(ex, "Failed to search PyPI for packages: {Query}", query);
            throw new Exceptions.PackageInstallationException(
                $"Failed to search PyPI for packages: {ex.Message}",
                ex);
        }
    }

    /// <summary>
    /// Searches PyPI for packages (synchronous version).
    /// </summary>
    public IReadOnlyList<Models.PyPISearchResult> SearchPackages(string query)
    {
        Task<IReadOnlyList<Models.PyPISearchResult>> task = SearchPackagesAsync(query);
        task.Wait();
        return task.Result;
    }

    /// <summary>
    /// Gets package metadata from PyPI.
    /// </summary>
    /// <param name="packageName">The name of the package.</param>
    /// <param name="version">Optional version. If not specified, returns the latest version.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Package metadata, or null if not found.</returns>
    public async Task<Models.PyPIPackageInfo?> GetPackageMetadataAsync(
        string packageName,
        string? version = null,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(packageName))
            throw new ArgumentException("Package name cannot be null or empty.", nameof(packageName));

        ValidateInstallation();

        this.Logger?.LogDebug("Getting package metadata from PyPI: {Package}, Version={Version}", packageName, version ?? "latest");

        try
        {
            using var httpClient = new System.Net.Http.HttpClient();
            httpClient.Timeout = TimeSpan.FromSeconds(30);
            
            var url = string.IsNullOrWhiteSpace(version)
                ? $"https://pypi.org/pypi/{Uri.EscapeDataString(packageName)}/json"
                : $"https://pypi.org/pypi/{Uri.EscapeDataString(packageName)}/{Uri.EscapeDataString(version)}/json";
            
            var response = await httpClient.GetAsync(url, cancellationToken).ConfigureAwait(false);
            
            if (!response.IsSuccessStatusCode)
            {
                if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
                {
                    this.Logger?.LogDebug("Package not found in PyPI: {Package}", packageName);
                    return null;
                }
                
                response.EnsureSuccessStatusCode();
            }
            
            var jsonContent = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
            var jsonDoc = System.Text.Json.JsonDocument.Parse(jsonContent);
            
            if (!jsonDoc.RootElement.TryGetProperty("info", out var info))
                return null;
            
            var name = info.TryGetProperty("name", out var nameProp) ? nameProp.GetString() ?? packageName : packageName;
            var pkgVersion = info.TryGetProperty("version", out var versionProp) ? versionProp.GetString() ?? "" : "";
            var summary = info.TryGetProperty("summary", out var summaryProp) ? summaryProp.GetString() : null;
            var description = info.TryGetProperty("description", out var descProp) ? descProp.GetString() : null;
            var author = info.TryGetProperty("author", out var authorProp) ? authorProp.GetString() : null;
            var authorEmail = info.TryGetProperty("author_email", out var emailProp) ? emailProp.GetString() : null;
            var homePage = info.TryGetProperty("home_page", out var homePageProp) ? homePageProp.GetString() : null;
            var license = info.TryGetProperty("license", out var licenseProp) ? licenseProp.GetString() : null;
            
            var requiresPython = new List<string>();
            if (info.TryGetProperty("requires_python", out var requiresPythonProp) && requiresPythonProp.ValueKind == System.Text.Json.JsonValueKind.String)
            {
                var requiresPythonStr = requiresPythonProp.GetString();
                if (!string.IsNullOrWhiteSpace(requiresPythonStr))
                {
                    requiresPython.Add(requiresPythonStr);
                }
            }
            
            return new Models.PyPIPackageInfo(
                name,
                pkgVersion,
                summary,
                description,
                author,
                authorEmail,
                homePage,
                license,
                requiresPython.AsReadOnly());
        }
        catch (Exception ex)
        {
            this.Logger?.LogError(ex, "Failed to get package metadata from PyPI: {Package}", packageName);
            throw new Exceptions.PackageInstallationException(
                $"Failed to get package metadata from PyPI: {ex.Message}",
                ex);
        }
    }

    /// <summary>
    /// Gets package metadata from PyPI (synchronous version).
    /// </summary>
    public Models.PyPIPackageInfo? GetPackageMetadata(string packageName, string? version = null)
    {
        Task<Models.PyPIPackageInfo?> task = GetPackageMetadataAsync(packageName, version);
        task.Wait();
        return task.Result;
    }

    /// <summary>
    /// Executes a Python process with the specified arguments and handles stdin/stdout/stderr.
    /// </summary>
    protected virtual async Task<PythonExecutionResult> ExecuteProcessAsync(
        IEnumerable<string> arguments,
        Func<string?>? stdinHandler,
        Action<string>? stdoutHandler,
        Action<string>? stderrHandler,
        CancellationToken cancellationToken,
        string? workingDirectory = null,
        Dictionary<string, string>? environmentVariables = null,
        ProcessPriorityClass? priority = null,
        int? maxMemoryMB = null,
        TimeSpan? timeout = null)
    {
        var processStartInfo = new ProcessStartInfo
        {
            FileName = PythonExecutablePath,
            WorkingDirectory = workingDirectory ?? WorkingDirectory,
            UseShellExecute = false,
            // Always redirect stdin to prevent blocking on TTY when parent process has terminal attached
            RedirectStandardInput = true,
            // Always redirect output/error to capture for result, even if no handlers provided
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true,
            StandardInputEncoding = stdinHandler is not null ? Encoding.UTF8 : null,
            // Always use UTF-8 encoding for output/error streams
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8
        };

        // Add environment variables if provided
        if (environmentVariables != null)
        {
            foreach (var kvp in environmentVariables)
            {
                processStartInfo.Environment[kvp.Key] = kvp.Value;
            }
        }

        // Use ArgumentList for proper argument handling (available in .NET Core 2.1+)
        foreach (var arg in arguments)
        {
            processStartInfo.ArgumentList.Add(arg);
        }

        // Create a combined cancellation token that includes the timeout if specified
        CancellationToken effectiveCancellationToken = cancellationToken;
        CancellationTokenSource? timeoutCts = null;
        if (timeout.HasValue && timeout.Value > TimeSpan.Zero)
        {
            timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeoutCts.CancelAfter(timeout.Value);
            effectiveCancellationToken = timeoutCts.Token;
        }

        try
        {
            // Use the process executor service for execution
            var processResult = await ProcessExecutor.ExecuteAsync(
                processStartInfo,
                stdinHandler,
                stdoutHandler,
                stderrHandler,
                effectiveCancellationToken).ConfigureAwait(false);

            // Note: ProcessPriority and maxMemoryMB would need to be set on the Process object
            // which is currently abstracted in ProcessExecutor. These parameters are accepted
            // for API compatibility but are not yet fully implemented.

            return new PythonExecutionResult(
                processResult.ExitCode,
                processResult.StandardOutput,
                processResult.StandardError);
        }
        finally
        {
            timeoutCts?.Dispose();
        }
    }
}
