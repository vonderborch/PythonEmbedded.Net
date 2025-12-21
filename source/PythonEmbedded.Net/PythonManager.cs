using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Octokit;
using PythonEmbedded.Net.Models;

namespace PythonEmbedded.Net;

/// <summary>
/// Manager for direct Python runtime instances (not using Python.NET).
/// </summary>
public class PythonManager : BasePythonManager
{
    private readonly ILoggerFactory? _loggerFactory;

    /// <summary>
    /// Initializes a new instance of the PythonManager class.
    /// </summary>
    /// <param name="directory">The directory where Python instances will be stored.</param>
    /// <param name="githubClient">The GitHub client for downloading Python distributions.</param>
    /// <param name="logger">Optional logger for this manager.</param>
    /// <param name="loggerFactory">Optional logger factory for creating loggers for runtime instances.</param>
    /// <param name="cache">Optional memory cache for caching GitHub API responses.</param>
    /// <param name="configuration">Optional configuration settings for this manager.</param>
    public PythonManager(
        string directory,
        GitHubClient githubClient,
        ILogger<PythonManager>? logger = null,
        ILoggerFactory? loggerFactory = null,
        IMemoryCache? cache = null,
        ManagerConfiguration? configuration = null)
        : base(directory, githubClient, logger, loggerFactory, cache, configuration)
    {
        _loggerFactory = loggerFactory;
    }

    /// <summary>
    /// Gets a Python runtime instance for the specified instance metadata.
    /// </summary>
    /// <param name="instanceMetadata">The metadata for the Python instance.</param>
    /// <returns>A Python root runtime instance.</returns>
    public override BasePythonRuntime GetPythonRuntimeForInstance(InstanceMetadata instanceMetadata)
    {
        if (instanceMetadata == null)
            throw new ArgumentNullException(nameof(instanceMetadata));

        ILogger<PythonRootRuntime>? logger = _loggerFactory?.CreateLogger<PythonRootRuntime>();
        return new PythonRootRuntime(instanceMetadata, logger);
    }
}
