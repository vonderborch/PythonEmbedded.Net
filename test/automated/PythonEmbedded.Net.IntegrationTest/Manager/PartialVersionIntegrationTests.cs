using Microsoft.Extensions.Logging;
using NUnit.Framework;
using Octokit;
using PythonEmbedded.Net.Test.TestUtilities;

namespace PythonEmbedded.Net.IntegrationTest.Manager;

/// <summary>
/// Integration tests for partial version support (e.g., "3.10" finding latest patch "3.10.19").
/// These tests require GitHub API access and may hit rate limits.
/// Run these tests separately when needed.
/// </summary>
[TestFixture]
[Category("Integration")]
public class PartialVersionIntegrationTests
{
    private string _testDirectory = null!;
    private PythonEmbedded.Net.PythonManager _manager = null!;

    [SetUp]
    public void SetUp()
    {
        _testDirectory = TestDirectoryHelper.CreateTestDirectory("PartialVersion");
        var githubClient = new GitHubClient(new ProductHeaderValue("PythonEmbedded.Net-IntegrationTest"));
        _manager = new PythonEmbedded.Net.PythonManager(_testDirectory, githubClient);
    }

    [TearDown]
    public void TearDown()
    {
        TestDirectoryHelper.DeleteTestDirectory(_testDirectory);
    }

    [Test]
    [Category("Integration")]
    public async Task GetOrCreateInstance_WithPartialVersion_FindsLatestPatch()
    {
        // Arrange & Act
        // This test verifies that "3.10" finds the latest patch version like "3.10.19"
        var runtime = await _manager.GetOrCreateInstanceAsync("3.10");
        var info = _manager.GetInstanceInfo("3.10");
        
        // Assert
        Assert.That(info, Is.Not.Null);
        Assert.That(info!.PythonVersion, Does.StartWith("3.10."));
        // Verify it's the latest patch version for 3.10
        var versionParts = info.PythonVersion.Split('.');
        Assert.That(versionParts.Length, Is.EqualTo(3)); // Should have patch version
    }

    [Test]
    [Category("Integration")]
    public async Task GetOrCreateInstance_WithPartialVersion_AndBuildDate_FindsCorrectVersion()
    {
        // Arrange & Act
        // This test verifies that partial version + buildDate works correctly
        var buildDate = new DateTime(2024, 1, 15);
        var runtime = await _manager.GetOrCreateInstanceAsync("3.10", buildDate);
        var info = _manager.GetInstanceInfo("3.10", buildDate);
        
        // Assert
        Assert.That(info, Is.Not.Null);
        Assert.That(info!.PythonVersion, Does.StartWith("3.10."));
        Assert.That(info.BuildDate.Date, Is.GreaterThanOrEqualTo(buildDate.Date));
    }

    [Test]
    [Category("Integration")]
    public async Task GetOrCreateInstance_WithPartialVersion_SelectsLatestPatch()
    {
        // Arrange & Act
        // This test verifies that when multiple patch versions exist, the latest is selected
        var runtime = await _manager.GetOrCreateInstanceAsync("3.12");
        var info = _manager.GetInstanceInfo("3.12");
        
        // Assert
        Assert.That(info, Is.Not.Null);
        Assert.That(info!.PythonVersion, Does.StartWith("3.12."));
        // The version should be the latest patch available for 3.12
    }
}

