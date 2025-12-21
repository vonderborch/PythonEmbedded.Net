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
    /// Executes a Python command and returns the result.
    /// </summary>
    /// <param name="command">The Python command to execute (e.g., "-c 'print(\"Hello\")'").</param>
    /// <param name="stdinHandler">Optional handler for providing stdin input line by line. Return null to end input.</param>
    /// <param name="stdoutHandler">Optional handler for processing stdout output line by line.</param>
    /// <param name="stderrHandler">Optional handler for processing stderr output line by line.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <param name="workingDirectory">Optional working directory override.</param>
    /// <param name="environmentVariables">Optional environment variables to set for the execution.</param>
    /// <returns>The execution result containing exit code, stdout, and stderr.</returns>
    public async Task<PythonExecutionResult> ExecuteCommandAsync(
        string command,
        Func<string?>? stdinHandler = null,
        Action<string>? stdoutHandler = null,
        Action<string>? stderrHandler = null,
        CancellationToken cancellationToken = default,
        string? workingDirectory = null,
        Dictionary<string, string>? environmentVariables = null)
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
            environmentVariables).ConfigureAwait(false);

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
    /// <returns>The execution result containing exit code, stdout, and stderr.</returns>
    public async Task<PythonExecutionResult> ExecuteScriptAsync(
        string scriptPath,
        IEnumerable<string>? arguments = null,
        Func<string?>? stdinHandler = null,
        Action<string>? stdoutHandler = null,
        Action<string>? stderrHandler = null,
        CancellationToken cancellationToken = default,
        string? workingDirectory = null,
        Dictionary<string, string>? environmentVariables = null)
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
            environmentVariables).ConfigureAwait(false);

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
    /// Installs a Python package using pip.
    /// </summary>
    /// <param name="packageSpecification">The package specification (e.g., "numpy", "torch==2.0.0", "numpy>=1.20.0").</param>
    /// <param name="upgrade">Whether to upgrade the package if it's already installed.</param>
    /// <param name="indexUrl">Optional custom PyPI index URL to use for this installation.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The execution result from pip.</returns>
    public async Task<PythonExecutionResult> InstallPackageAsync(
        string packageSpecification,
        bool upgrade = false,
        string? indexUrl = null,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(packageSpecification))
            throw new ArgumentException("Package specification cannot be null or empty.", nameof(packageSpecification));

        ValidateInstallation();

        this.Logger?.LogInformation("Installing Python package: {Package}", packageSpecification);

        var pipArgs = new List<string> { "-m", "pip", "install" };
        if (upgrade)
        {
            pipArgs.Add("--upgrade");
        }
        if (!string.IsNullOrWhiteSpace(indexUrl))
        {
            pipArgs.Add("--index-url");
            pipArgs.Add(indexUrl);
        }
        pipArgs.Add(packageSpecification);

        try
        {
            var result = await ExecuteProcessAsync(pipArgs, null, null, null, cancellationToken, null, null).ConfigureAwait(false);

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
        catch (PackageInstallationException)
        {
            throw;
        }
        catch (Exception ex)
        {
            throw new PackageInstallationException(
                $"Failed to install package '{packageSpecification}': {ex.Message}",
                ex)
            {
                PackageSpecification = packageSpecification
            };
        }
    }

    /// <summary>
    /// Installs a Python package using pip (synchronous version).
    /// </summary>
    /// <param name="packageSpecification">The package specification (e.g., "numpy", "torch==2.0.0", "numpy>=1.20.0").</param>
    /// <param name="upgrade">Whether to upgrade the package if it's already installed.</param>
    /// <param name="indexUrl">Optional custom PyPI index URL to use for this installation.</param>
    /// <returns>The execution result from pip.</returns>
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
    /// <returns>The execution result from pip.</returns>
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

        this.Logger?.LogInformation("Installing packages from requirements file: {FilePath}", requirementsFilePath);

        var pipArgs = new List<string> { "-m", "pip", "install", "-r", requirementsFilePath };
        if (upgrade)
        {
            pipArgs.Add("--upgrade");
        }

        try
        {
            var result = await ExecuteProcessAsync(pipArgs, null, null, null, cancellationToken).ConfigureAwait(false);

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
        catch (RequirementsFileException)
        {
            throw;
        }
        catch (Exception ex)
        {
            throw new RequirementsFileException(
                $"Failed to install packages from requirements file '{requirementsFilePath}': {ex.Message}",
                ex)
            {
                PackageSpecification = requirementsFilePath
            };
        }
    }

    /// <summary>
    /// Installs Python packages from a requirements.txt file (synchronous version).
    /// </summary>
    /// <param name="requirementsFilePath">The path to the requirements.txt file.</param>
    /// <param name="upgrade">Whether to upgrade packages if they're already installed.</param>
    /// <returns>The execution result from pip.</returns>
    public PythonExecutionResult InstallRequirements(
        string requirementsFilePath,
        bool upgrade = false)
    {
        Task<PythonExecutionResult> task = InstallRequirementsAsync(requirementsFilePath, upgrade);
        task.Wait();
        return task.Result;
    }

    /// <summary>
    /// Installs a Python package from a pyproject.toml file (using pip install).
    /// </summary>
    /// <param name="pyProjectFilePath">The path to the directory containing pyproject.toml or the pyproject.toml file itself.</param>
    /// <param name="editable">Whether to install in editable mode (pip install -e).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The execution result from pip.</returns>
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

        this.Logger?.LogInformation("Installing package from pyproject.toml: {Path} (editable: {Editable})",
            pyProjectTomlPath, editable);

        var pipArgs = new List<string> { "-m", "pip", "install" };
        if (editable)
        {
            pipArgs.Add("-e");
        }
        pipArgs.Add(projectDirectory);

        try
        {
            // Execute pip install from the project directory
            var result = await ExecuteProcessAsync(
                pipArgs,
                null,
                null,
                null,
                cancellationToken,
                projectDirectory).ConfigureAwait(false);

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
        catch (PackageInstallationException)
        {
            throw;
        }
        catch (Exception ex)
        {
            throw new PackageInstallationException(
                $"Failed to install package from pyproject.toml '{pyProjectTomlPath}': {ex.Message}",
                ex)
            {
                PackageSpecification = pyProjectTomlPath
            };
        }
    }

    /// <summary>
    /// Installs a Python package from a pyproject.toml file (synchronous version).
    /// </summary>
    /// <param name="pyProjectFilePath">The path to the directory containing pyproject.toml or the pyproject.toml file itself.</param>
    /// <param name="editable">Whether to install in editable mode (pip install -e).</param>
    /// <returns>The execution result from pip.</returns>
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
    public async Task<IReadOnlyList<Models.PackageInfo>> ListInstalledPackagesAsync(CancellationToken cancellationToken = default)
    {
        ValidateInstallation();

        this.Logger?.LogDebug("Listing installed packages");

        var result = await ExecuteProcessAsync(
            ["-m", "pip", "list", "--format=json"],
            null, null, null,
            cancellationToken, null, null).ConfigureAwait(false);

        if (result.ExitCode != 0)
        {
            throw new Exceptions.PackageInstallationException(
                $"Failed to list installed packages. Exit code: {result.ExitCode}")
            {
                InstallationOutput = result.StandardOutput + result.StandardError
            };
        }

        // Parse JSON output from pip list --format=json
        var packages = new List<Models.PackageInfo>();
        try
        {
            var jsonPackages = System.Text.Json.JsonSerializer.Deserialize<List<Dictionary<string, object>>>(result.StandardOutput);
            if (jsonPackages != null)
            {
                foreach (var pkg in jsonPackages)
                {
                    var name = pkg.TryGetValue("name", out var nameObj) ? nameObj?.ToString() ?? "" : "";
                    var version = pkg.TryGetValue("version", out var versionObj) ? versionObj?.ToString() ?? "" : "";
                    packages.Add(new Models.PackageInfo(name, version));
                }
            }
        }
        catch (Exception ex)
        {
            // Fallback: parse legacy format
            foreach (var line in result.StandardOutput.Split('\n', StringSplitOptions.RemoveEmptyEntries))
            {
                var trimmed = line.Trim();
                if (string.IsNullOrWhiteSpace(trimmed) || trimmed.StartsWith("Package", StringComparison.OrdinalIgnoreCase) || trimmed.StartsWith("---", StringComparison.OrdinalIgnoreCase))
                    continue;

                var parts = trimmed.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length >= 2)
                {
                    packages.Add(new Models.PackageInfo(parts[0], parts[1]));
                }
            }
            
            this.Logger?.LogWarning(ex, "Failed to parse pip list JSON output, used fallback parser");
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
    public async Task<string?> GetPackageVersionAsync(string packageName, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(packageName))
            throw new ArgumentException("Package name cannot be null or empty.", nameof(packageName));

        ValidateInstallation();

        var result = await ExecuteProcessAsync(
            ["-m", "pip", "show", packageName],
            null, null, null,
            cancellationToken, null, null).ConfigureAwait(false);

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
    public async Task<bool> IsPackageInstalledAsync(string packageName, CancellationToken cancellationToken = default)
    {
        var version = await GetPackageVersionAsync(packageName, cancellationToken).ConfigureAwait(false);
        return version != null;
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

        var result = await ExecuteProcessAsync(
            ["-m", "pip", "show", packageName],
            null, null, null,
            cancellationToken, null, null).ConfigureAwait(false);

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
    /// Uninstalls a Python package.
    /// </summary>
    /// <param name="packageName">The name of the package to uninstall.</param>
    /// <param name="removeDependencies">Whether to remove dependencies (pip uninstall -y).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The execution result from pip.</returns>
    public async Task<PythonExecutionResult> UninstallPackageAsync(
        string packageName,
        bool removeDependencies = false,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(packageName))
            throw new ArgumentException("Package name cannot be null or empty.", nameof(packageName));

        ValidateInstallation();

        this.Logger?.LogInformation("Uninstalling Python package: {Package}", packageName);

        var pipArgs = new List<string> { "-m", "pip", "uninstall", packageName, "-y" };
        if (removeDependencies)
        {
            // Note: pip uninstall doesn't have a built-in way to remove dependencies
            // This would require additional logic to track and remove dependencies
            // For now, we just uninstall the package itself
        }

        try
        {
            var result = await ExecuteProcessAsync(pipArgs, null, null, null, cancellationToken).ConfigureAwait(false);

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
        catch (Exceptions.PackageInstallationException)
        {
            throw;
        }
        catch (Exception ex)
        {
            throw new Exceptions.PackageInstallationException(
                $"Failed to uninstall package '{packageName}': {ex.Message}",
                ex)
            {
                PackageSpecification = packageName
            };
        }
    }

    /// <summary>
    /// Uninstalls a Python package (synchronous version).
    /// </summary>
    public PythonExecutionResult UninstallPackage(string packageName, bool removeDependencies = false)
    {
        Task<PythonExecutionResult> task = UninstallPackageAsync(packageName, removeDependencies);
        task.Wait();
        return task.Result;
    }

    /// <summary>
    /// Lists packages that have available updates.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A list of outdated packages with their current and latest versions.</returns>
    public async Task<IReadOnlyList<Models.OutdatedPackageInfo>> ListOutdatedPackagesAsync(CancellationToken cancellationToken = default)
    {
        ValidateInstallation();

        this.Logger?.LogDebug("Listing outdated packages");

        var result = await ExecuteProcessAsync(
            ["-m", "pip", "list", "--outdated", "--format=json"],
            null, null, null,
            cancellationToken, null, null).ConfigureAwait(false);

        if (result.ExitCode != 0)
        {
            throw new Exceptions.PackageInstallationException(
                $"Failed to list outdated packages. Exit code: {result.ExitCode}")
            {
                InstallationOutput = result.StandardOutput + result.StandardError
            };
        }

        var outdatedPackages = new List<Models.OutdatedPackageInfo>();
        try
        {
            var jsonPackages = System.Text.Json.JsonSerializer.Deserialize<List<Dictionary<string, object>>>(result.StandardOutput);
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
            foreach (var line in result.StandardOutput.Split('\n', StringSplitOptions.RemoveEmptyEntries))
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
            
            this.Logger?.LogWarning(ex, "Failed to parse pip list --outdated JSON output, used fallback parser");
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
        var outdated = await ListOutdatedPackagesAsync(cancellationToken).ConfigureAwait(false);
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
                    var result = await InstallPackageAsync(pkg.Name, upgrade: true, indexUrl: null, cancellationToken).ConfigureAwait(false);
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
        return await InstallPackageAsync(packageSpec, upgrade: false, indexUrl: null, cancellationToken).ConfigureAwait(false);
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
    /// <returns>The execution result from pip.</returns>
    public async Task<PythonExecutionResult> ExportRequirementsAsync(string outputPath, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(outputPath))
            throw new ArgumentException("Output path cannot be null or empty.", nameof(outputPath));

        ValidateInstallation();

        this.Logger?.LogInformation("Exporting requirements to: {Path}", outputPath);

        var result = await ExecuteProcessAsync(
            ["-m", "pip", "freeze"],
            null, null, null,
            cancellationToken, null, null).ConfigureAwait(false);

        if (result.ExitCode != 0)
        {
            throw new Exceptions.PackageInstallationException(
                $"Failed to export requirements. Exit code: {result.ExitCode}")
            {
                InstallationOutput = result.StandardOutput + result.StandardError
            };
        }

        await File.WriteAllTextAsync(outputPath, result.StandardOutput, cancellationToken).ConfigureAwait(false);

        this.Logger?.LogInformation("Successfully exported requirements to: {Path}", outputPath);
        return result;
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
    /// Exports installed packages as a requirements.txt string (with exact versions from pip freeze).
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The requirements.txt content as a string.</returns>
    public async Task<string> ExportRequirementsFreezeToStringAsync(CancellationToken cancellationToken = default)
    {
        ValidateInstallation();

        var result = await ExecuteProcessAsync(
            ["-m", "pip", "freeze"],
            null, null, null,
            cancellationToken, null, null).ConfigureAwait(false);

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
    /// Exports installed packages to a requirements.txt file (with exact versions from pip freeze).
    /// </summary>
    /// <param name="outputPath">The path where to write the requirements.txt file.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The execution result from pip.</returns>
    public async Task<PythonExecutionResult> ExportRequirementsFreezeAsync(string outputPath, CancellationToken cancellationToken = default)
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
                    var result = await InstallPackageAsync(pkg, upgrade, indexUrl: null, cancellationToken).ConfigureAwait(false);
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
                    results[pkg] = await InstallPackageAsync(pkg, upgrade, indexUrl: null, cancellationToken).ConfigureAwait(false);
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
    /// <param name="removeDependencies">Whether to remove dependencies.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A dictionary mapping package names to their uninstallation results.</returns>
    public async Task<Dictionary<string, PythonExecutionResult>> UninstallPackagesAsync(
        IEnumerable<string> packages,
        bool parallel = false,
        bool removeDependencies = false,
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
                    var result = await UninstallPackageAsync(pkg, removeDependencies, cancellationToken).ConfigureAwait(false);
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
                    results[pkg] = await UninstallPackageAsync(pkg, removeDependencies, cancellationToken).ConfigureAwait(false);
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
    public Dictionary<string, PythonExecutionResult> UninstallPackages(IEnumerable<string> packages, bool parallel = false, bool removeDependencies = false)
    {
        Task<Dictionary<string, PythonExecutionResult>> task = UninstallPackagesAsync(packages, parallel, removeDependencies);
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
        Dictionary<string, string>? environmentVariables = null)
    {
        var processStartInfo = new ProcessStartInfo
        {
            FileName = PythonExecutablePath,
            WorkingDirectory = workingDirectory ?? WorkingDirectory,
            UseShellExecute = false,
            RedirectStandardInput = stdinHandler != null,
            RedirectStandardOutput = stdoutHandler != null,
            RedirectStandardError = stderrHandler != null,
            CreateNoWindow = true,
            StandardInputEncoding = Encoding.UTF8,
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

        // Use the process executor service for execution
        var processResult = await ProcessExecutor.ExecuteAsync(
            processStartInfo,
            stdinHandler,
            stdoutHandler,
            stderrHandler,
            cancellationToken).ConfigureAwait(false);

        return new PythonExecutionResult(
            processResult.ExitCode,
            processResult.StandardOutput,
            processResult.StandardError);
    }
}
