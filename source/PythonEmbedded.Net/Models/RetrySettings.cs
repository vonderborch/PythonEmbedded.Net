namespace PythonEmbedded.Net.Models;

/// <summary>
/// Represents configurable settings for managing retry behavior during operations.
/// This class is designed to encapsulate parameters such as the number of retry attempts,
/// delay between retries, default timeout for operations, and whether to use exponential backoff.
/// </summary>
/// <param name="DefaultTimeout">The default timeout for operations, if any.</param>
/// <param name="RetryAttempt">The number of retry attempts to be made before giving up.</param>
/// <param name="RetryDelay">The delay between retry attempts, if any.</param>
/// <param name="UseExponentialBackoff">Whether to use exponential backoff for retry delays.</param>
public record RetrySettings(
    TimeSpan? DefaultTimeout = null,
    int RetryAttempt = 3,
    TimeSpan? RetryDelay = null,
    bool UseExponentialBackoff = true);
