using Microsoft.Extensions.Logging;
using PythonEmbedded.Net.Helpers;
using PythonEmbedded.Net.Models;
using PythonEmbedded.Net.PythonProviders;
using PythonEmbedded.Net.PythonProviders.PythonBuildStandalone;

namespace PythonEmbedded.Net;

public class PythonRuntime : IDisposable
{
    protected PythonRuntimeBackend _backend;

    private PythonRuntimeData _runtimeData; 
    
    private ILogger<PythonRuntime>? _logger;
    
    private ILoggerFactory? _loggerFactory;
    
    private PythonProvider? _provider;
    
    protected PythonRuntime(
        string directory, string pythonVersion, DateTime? pythonVersionBuildDate = null, string? backend = null, PythonProvider? provider = null, ILogger<PythonRuntime>? logger = null, ILoggerFactory? loggerFactory = null, RetrySettings? defaultRetrySettings = null)
    {
        _logger = logger;
        _loggerFactory = loggerFactory;
        _provider = provider ?? new PythonBuildStandaloneProvider();
        
        var actualBackend = backend ?? Constants.DefaultBackend;
        _logger?.LogDebug("Setting Python runtime for directory `{directory}` tp backend: {Backend}", directory, actualBackend);
        var backendType = PythonImplementationRegistry.Instance.GetBackendType(actualBackend);
        _backend = (PythonRuntimeBackend)Activator.CreateInstance(backendType)!;
        
        Version actualPythonVersion = Version.Parse(pythonVersion);
        var actualPythonVersionBuildDate = pythonVersionBuildDate?.ToString("yyyy-MM-dd") ?? "latest";
        
        _logger?.LogDebug("Creating Python runtime for directory `{directory}` with backend `{backend}`, version `{version}`, build date `{buildDate}`", directory, actualBackend, actualPythonVersion, actualPythonVersionBuildDate);
        _runtimeData = PythonRuntimeData.LoadOrCreate(directory, actualPythonVersion, actualPythonVersionBuildDate, actualBackend);
    }

    /// <summary>
    /// Gets the runtime data associated with the Python runtime instance.
    /// Allows access to metadata such as the Python version, build date, backend,
    /// and details about the managed Python environments.
    /// </summary>
    public PythonRuntimeData Data => _runtimeData;

    /// <summary>
    /// Saves the current state of the runtime data associated with the Python runtime.
    /// This method invokes the <see cref="PythonRuntimeData.Save"/> method to persist
    /// the runtime metadata to the associated file path. If the file path is not
    /// configured in the runtime data, the save operation will not proceed.
    /// </summary>
    public void SaveData()
    {
        _runtimeData.Save();
    }

    public Task ValidateAsync(CancellationToken cancellationToken)
    {
        if (!File.Exists(_runtimeData.PythonExecutableDirectoryPath))
        {
            throw new FileNotFoundException(
                $"Python compiled directory was not found: {_runtimeData.PythonExecutableDirectoryPath}",
                _runtimeData.PythonExecutableDirectoryPath);
        }

        return Task.CompletedTask;
    }

    /// <summary>
    /// Retrieves the list of managed Python environments associated with this runtime.
    /// This method consolidates and returns the collection of environment metadata,
    /// ensuring that they are sorted and up-to-date based on the data from <see cref="ManagedPythonRuntimesData.Environments"/>.
    /// </summary>
    /// <returns>A sorted list of <see cref="PythonEnvironmentData"/> objects representing the managed environments.</returns>
    public List<PythonEnvironmentData> GetManagedEnvironments()
    {
        List<PythonEnvironmentData> environments = _runtimeData.Environments.Select(x => x.Value).ToList();
        environments.Sort();
        return environments;
    }

    public async Task<PythonEnvironment?> GetOrCreateEnvironmentAsync(
        string name, string? externalPath = null,
        CancellationToken cancellationToken = default)
    {
        // Get 
        
        // Step 1 - If we are managing the environment with that name, return it now
        if (_runtimeData.Environments.Any(e => e.Key == name))
        {

        }
        
        // Step 2.1 - Validate that the environment doesn't already exist at the path specified
        
        // Step 2.2 - Use the backend to make sure that we have the Python version actually downloaded
        
        // Step 2.3 - Create the virtual environment
        
        // Step 2.4 - Return the virtual environment
    }

    /// <summary>
    /// Deletes a managed Python environment by its name. This method removes the
    /// specified environment's directory from the file system and unregisters it
    /// from the runtime's managed environments.
    /// </summary>
    /// <param name="name">The name of the environment to be deleted.</param>
    /// <returns>
    /// True if the environment is successfully found and deleted; otherwise, false
    /// if the environment does not exist.
    /// </returns>
    public bool DeleteEnvironment(string name)
    {
        if (!_runtimeData.Environments.TryGetValue(name, out var environment))
        {
            return false;
        }
        
        IOHelpers.DeleteDirectory(environment.Path);
        _runtimeData.RemoveManagedEnvironments(name);
        return true;
    }

    /// <summary>
    /// Deletes the current Python runtime instance, including all its managed environments and the root directory.
    /// This method ensures that all associated resources are properly cleaned up and the instance is disposed.
    /// </summary>
    /// <param name="cancellationToken">
    /// A token that can be used to signal cancellation of the delete operation.
    /// If cancellation is requested, the operation stops after completing any in-progress deletions.
    /// </param>
    public async Task DeleteAsync(CancellationToken cancellationToken = default)
    {
        // Step 1 - Delete all managed environments
        List<PythonEnvironmentData> environments = GetManagedEnvironments();
        foreach (var environment in environments)
        {
            DeleteEnvironment(environment.Name);
            if (cancellationToken.IsCancellationRequested)
            {
                return;
            }
        }
        
        // Step 2 - Delete root directory
        await IOHelpers.DeleteDirectoryAsync(_runtimeData.Directory, cancellationToken);
        
        // Step 3 - This should be disposed
        Dispose();
    }

    /// <summary>
    /// Releases the resources used by the <see cref="PythonRuntime"/> instance.
    /// This method is responsible for cleaning up both managed and unmanaged resources.
    /// It should be called when the instance is no longer needed to ensure proper resource disposal.
    /// </summary>
    public void Dispose()
    {
        // TODO release managed resources here
    }
}
