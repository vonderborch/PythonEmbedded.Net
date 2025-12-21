namespace PythonEmbedded.Net.Test.TestUtilities;

/// <summary>
/// Helper class for managing test directories.
/// </summary>
public static class TestDirectoryHelper
{
    /// <summary>
    /// Creates a temporary test directory.
    /// </summary>
    /// <param name="prefix">Optional prefix for the directory name.</param>
    /// <returns>The path to the created directory.</returns>
    public static string CreateTestDirectory(string? prefix = null)
    {
        string tempPath = Path.GetTempPath();
        string directoryName = prefix != null 
            ? $"{prefix}_{Guid.NewGuid():N}" 
            : $"PythonEmbeddedTest_{Guid.NewGuid():N}";
        string testDirectory = Path.Combine(tempPath, directoryName);
        Directory.CreateDirectory(testDirectory);
        return testDirectory;
    }

    /// <summary>
    /// Deletes a test directory and all its contents.
    /// </summary>
    /// <param name="directory">The directory to delete.</param>
    public static void DeleteTestDirectory(string directory)
    {
        if (Directory.Exists(directory))
        {
            try
            {
                Directory.Delete(directory, true);
            }
            catch (IOException)
            {
                // Sometimes on Windows, files might be locked. Try again after a short delay.
                Thread.Sleep(100);
                try
                {
                    Directory.Delete(directory, true);
                }
                catch
                {
                    // Ignore - cleanup failure is not critical for tests
                }
            }
        }
    }
}
