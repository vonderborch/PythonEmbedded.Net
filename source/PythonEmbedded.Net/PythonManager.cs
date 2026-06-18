using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Octokit;

namespace PythonEmbedded.Net;


public class PythonManager
{
    string _directory;
    
    GitHubClient _githubClient;
    
    ILogger<PythonManager>? _logger;
    
    ILoggerFactory? _loggerFactory;
    
    IMemoryCache? _cache;
    
    PythonImplementationRegistry _implementationRegistry;
    
    Version? _defaultPythonVersion;
    
    string? _defaultPipIndexUrl;
    
    string? _pipProxyUrl;
    
    TimeSpan? _defaultTimeout;
    
    int _retryAttempts;
    
    TimeSpan? _retryDelay;
    
    bool _useExponentialBackoff;
    
    public PythonManager(
        string directory,
        GitHubClient githubClient,
        ILogger<PythonManager>? logger = null,
        ILoggerFactory? loggerFactory = null,
        IMemoryCache? cache = null,
        PythonImplementationRegistry? instanceFactory = null,
        string? defaultPythonVersion = null,
        string? defaultPipIndexUrl = null,
        string? pipProxyUrl = null,
        TimeSpan? defaultTimeout = null,
        int retryAttempts = 3,
        TimeSpan? retryDelay = null,
        bool useExponentialBackoff = true
    )
    {
        _implementationRegistry = instanceFactory ?? new PythonImplementationRegistry();
        _githubClient = githubClient;
        _logger = logger;
        _loggerFactory = loggerFactory;
        _cache = cache;
        _defaultPythonVersion = defaultPythonVersion is null ? null : new Version(defaultPythonVersion);
        _defaultPipIndexUrl = defaultPipIndexUrl;
        _pipProxyUrl = pipProxyUrl;
        _defaultTimeout = defaultTimeout;
        _retryAttempts = retryAttempts >= 0 ? retryAttempts : throw new ArgumentOutOfRangeException(nameof(retryAttempts), "Retry attempts must be a non-negative integer.");
        _retryDelay = retryDelay ?? TimeSpan.FromSeconds(1);
        _useExponentialBackoff = useExponentialBackoff;
        
        if (string.IsNullOrWhiteSpace(directory))
        {
            throw new ArgumentException("Directory cannot be null or whitespace", nameof(directory));
        }

        _directory = directory;
        if (!Path.IsPathRooted(directory))
        {
            _directory = Path.Combine(Directory.GetCurrentDirectory(), directory);
        }

        if (Directory.Exists(_directory))
        {
            _logger?.LogDebug("Using existing root directory: {RootDirectory}", _directory);
        }
        else
        {
            Directory.CreateDirectory(_directory);
            _logger?.LogInformation("Created new root directory: {RootDirectory}", _directory);
        }
    }

    public async Task<PythonEnvironment> GetOrCreateEnvironmentAsync(string name, string? pythonVersion = null, string? externalPath = null,
        DateTime? buildDate = null, Func<PythonRuntime>? postCreationCommand = null, CancellationToken cancellationToken = default
    )
    {
        Version? parsedPythonVersion = pythonVersion is null ? null : new Version(pythonVersion);
        await GetOrCreateEnvironmentAsync(name, parsedPythonVersion, externalPath, buildDate, postCreationCommand, cancellationToken);
    }

    public async Task<PythonEnvironment?> GetOrCreateEnvironmentAsync(string name, Version? pythonVersion = null, string? externalPath = null,
        DateTime? buildDate = null, Func<PythonRuntime>? postCreationCommand = null, CancellationToken cancellationToken = default
    )
    {
        Version? actualPythonVersion = pythonVersion ?? _defaultPythonVersion;
        if (actualPythonVersion is null)
        {
            throw new ArgumentException("Python version must be specified");
        }
        string buildDateString = buildDate?.ToString("yyyy-MM-dd") ?? "latest");
        if (cancellationToken.IsCancellationRequested)
        {
            return null;
        }
        
        // Get or create the Python runtime instance
        this._logger?.LogDebug("Getting or creating Python Runtime for Version: {Version}, BuildDate={BuildDate}", pythonVersion, buildDateString);
        var runtime = await InternalGetOrCreateRuntimeAsync(actualPythonVersion, buildDate, cancellationToken);
        if (cancellationToken.IsCancellationRequested || runtime is null)
        {
            return null;
        }
        
        // Get or create the Python Virtual Environment
        this._logger?.LogDebug("Getting or creating Python Environment `{Name}` with Python Version: {Version}, BuildDate={BuildDate}", name, pythonVersion, buildDateString);
        var environment = await InternalGetOrCreateEnvironmentAsync(runtime, name, externalPath, postCreationCommand, cancellationToken);
        
        return environment;
    }

    protected Task<PythonRuntime?> InternalGetOrCreateRuntimeAsync(
        Version? pythonVersion = null, DateTime? buildDate = null, CancellationToken cancellationToken = default
    )
    {
        
    }

    protected Task<PythonEnvironment?> InternalGetOrCreateEnvironmentAsync(
        PythonRuntime runtime, string name, string? externalPath = null, Func<PythonRuntime>? postCreationCommand = null, CancellationToken cancellationToken = default
    )
    {
        
    }
}
