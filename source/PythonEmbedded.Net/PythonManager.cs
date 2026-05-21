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
    
    PythonFactory _factory;
    
    string? _defaultPythonVersion;
    
    string? _defaultPipIndexUrl;
    
    string? _pipProxyUrl;
    
    TimeSpan? _defaultTimeout;
    
    int _retryAttempts;
    
    TimeSpan? _retryDelay;
    
    bool _useExponentialBackoff;
    
    string? _uvPath;
    
    public PythonManager(
        string directory,
        GitHubClient githubClient,
        ILogger<PythonManager>? logger = null,
        ILoggerFactory? loggerFactory = null,
        IMemoryCache? cache = null,
        PythonFactory? instanceFactory = null,
        string? defaultPythonVersion = null,
        string? defaultPipIndexUrl = null,
        string? pipProxyUrl = null,
        TimeSpan? defaultTimeout = null,
        int retryAttempts = 3,
        TimeSpan? retryDelay = null,
        bool useExponentialBackoff = true,
        string? uvPath = null
    )
    {
        _factory = instanceFactory ?? new PythonFactory();
        _githubClient = githubClient;
        _logger = logger;
        _loggerFactory = loggerFactory;
        _cache = cache;
        _defaultPythonVersion = defaultPythonVersion;
        _defaultPipIndexUrl = defaultPipIndexUrl;
        _pipProxyUrl = pipProxyUrl;
        _defaultTimeout = defaultTimeout;
        _retryAttempts = retryAttempts >= 0 ? retryAttempts : throw new ArgumentOutOfRangeException(nameof(retryAttempts), "Retry attempts must be a non-negative integer.");
        _retryDelay = retryDelay ?? TimeSpan.FromSeconds(1);
        _useExponentialBackoff = useExponentialBackoff;
        _uvPath = uvPath;
        
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

    private PythonInstance GetOrCreateRootInstanceAsync(
        Version? pythonVersion = null, DateTime? buildDate = null, CancellationToken cancellationToken = default
        )
    {
        
    }

    public async Task<PythonInstance> GetOrCreateInstanceAsync(string name, bool recreateIfExists = false, string? externalPath = null,
        string? pythonVersion = null, DateTime? buildDate = null, Func<PythonInstance>? postCreationCommand = null, CancellationToken cancellationToken = default
    )
    {
        Version? parsedPythonVersion = pythonVersion is null ? null : new Version(pythonVersion);
        await GetOrCreateInstanceAsync(name, recreateIfExists, externalPath, parsedPythonVersion, buildDate, postCreationCommand, cancellationToken);
    }

    public async Task<PythonInstance> GetOrCreateInstanceAsync(string name, bool recreateIfExists = false, string? externalPath = null,
        Version? pythonVersion = null, DateTime? buildDate = null, Func<PythonInstance>? postCreationCommand = null, CancellationToken cancellationToken = default
    )
    {
        
    }
}
