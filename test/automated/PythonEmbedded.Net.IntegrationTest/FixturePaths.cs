namespace PythonEmbedded.Net.IntegrationTest;

/// <summary>Locates the gitignored <c>test/fixtures/</c> directory (filled by <c>test/tools/fetch-fixtures.sh</c>).</summary>
public static class FixturePaths
{
    public static string FixturesDirectory
    {
        get
        {
            string? directory = TestContext.CurrentContext.TestDirectory;
            while (directory is not null)
            {
                string candidate = Path.Combine(directory, "test", "fixtures");
                if (Directory.Exists(candidate))
                {
                    return candidate;
                }

                directory = Path.GetDirectoryName(directory);
            }

            Assert.Ignore("test/fixtures not found — run test/tools/fetch-fixtures.sh");
            return null!; // unreachable: Assert.Ignore throws
        }
    }
}
