using Microsoft.Extensions.Logging;

namespace PythonEmbedded.Net.Helpers;

/// <summary>
/// Provides helper methods for performing common input/output operations.
/// </summary>
public static class IOHelpers
{
    /// <summary>
    /// Deletes an entire directory, including all its files and subdirectories.
    /// </summary>
    /// <param name="path">The path to the directory to be deleted.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <param name="logger">An optional logger for logging errors encountered during the deletion process.</param>
    /// <returns>A task representing the asynchronous directory deletion operation.</returns>
    public static async Task DeleteDirectoryAsync(string path, CancellationToken cancellationToken = default,
        ILogger? logger = null)
    {
        try
        {
            // Ensure all files are deletable before removing the directory
            foreach (string file in Directory.GetFiles(path, "*", SearchOption.AllDirectories))
            {
                try
                {
                    File.SetAttributes(file, FileAttributes.Normal); // Remove read-only flag
                    File.Delete(file);
                    if (cancellationToken.IsCancellationRequested)
                    {
                        return;
                    }
                }
                catch (Exception ex)
                {
                    logger?.LogError($"Failed to delete file '{file}': {ex.Message}");
                }
            }

            Directory.Delete(path, recursive: true);
        }
        catch (Exception ex)
        {
            logger?.LogError($"Error deleting directory '{path}': {ex.Message}");
            throw; // Rethrow so caller can handle
        }
    }
    /// <summary>
    /// Deletes an entire directory, including all its files and subdirectories.
    /// </summary>
    /// <param name="path">The path to the directory to be deleted.</param>
    /// <param name="logger">An optional logger for logging errors encountered during the deletion process.</param>
    /// <returns>A task representing the asynchronous directory deletion operation.</returns>
    public static void DeleteDirectory(string path, ILogger? logger = null)
    {
        try
        {
            // Ensure all files are deletable before removing the directory
            foreach (string file in Directory.GetFiles(path, "*", SearchOption.AllDirectories))
            {
                try
                {
                    File.SetAttributes(file, FileAttributes.Normal); // Remove read-only flag
                    File.Delete(file);
                }
                catch (Exception ex)
                {
                    logger?.LogError($"Failed to delete file '{file}': {ex.Message}");
                }
            }

            Directory.Delete(path, recursive: true);
        }
        catch (Exception ex)
        {
            logger?.LogError($"Error deleting directory '{path}': {ex.Message}");
            throw; // Rethrow so caller can handle
        }
    }
}
