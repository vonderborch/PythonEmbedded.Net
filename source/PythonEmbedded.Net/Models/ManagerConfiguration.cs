namespace PythonEmbedded.Net.Models;

/// <summary>
/// Configuration settings for a Python manager.
/// </summary>
public class ManagerConfiguration
{
    /// <summary>
    /// Gets or sets the default Python version to use when version is not specified.
    /// </summary>
    public string? DefaultPythonVersion { get; set; }

    /// <summary>
    /// Gets or sets the default PyPI index URL.
    /// </summary>
    public string? DefaultPipIndexUrl { get; set; }

    /// <summary>
    /// Gets or sets the proxy URL for pip operations.
    /// </summary>
    public string? ProxyUrl { get; set; }

    /// <summary>
    /// Gets or sets the default timeout for operations.
    /// </summary>
    public TimeSpan? DefaultTimeout { get; set; }

    /// <summary>
    /// Gets or sets the number of retry attempts for failed operations.
    /// </summary>
    public int RetryAttempts { get; set; } = 3;

    /// <summary>
    /// Gets or sets the delay between retry attempts.
    /// </summary>
    public TimeSpan RetryDelay { get; set; } = TimeSpan.FromSeconds(1);

    /// <summary>
    /// Gets or sets whether to use exponential backoff for retries.
    /// </summary>
    public bool UseExponentialBackoff { get; set; } = true;

    /// <summary>
    /// Gets or sets a custom path to the uv executable.
    /// If null, uv will be auto-detected from common installation locations.
    /// </summary>
    /// <remarks>
    /// uv is a fast Rust-based Python package installer (10-100x faster than pip)
    /// and is required for all package operations.
    /// </remarks>
    public string? UvPath { get; set; }
}

