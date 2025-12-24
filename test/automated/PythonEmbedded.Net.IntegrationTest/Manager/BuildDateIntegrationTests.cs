using Microsoft.Extensions.Logging;
using NUnit.Framework;
using Octokit;
using PythonEmbedded.Net.Test.TestUtilities;

namespace PythonEmbedded.Net.IntegrationTest.Manager;

/// <summary>
/// Integration tests for DateTime-based buildDate functionality with GitHub API.
/// These tests require GitHub API access and may hit rate limits.
/// Run these tests separately when needed.
/// </summary>
[TestFixture]
[Category("Integration")]
public class BuildDateIntegrationTests
{
    private string _testDirectory = null!;
    private PythonEmbedded.Net.PythonManager _manager = null!;

    [SetUp]
    public void SetUp()
    {
        _testDirectory = TestDirectoryHelper.CreateTestDirectory("BuildDateIntegration");
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
    public async Task GetOrCreateInstance_WithBuildDate_FindsReleaseOnOrAfterDate()
    {
        // Arrange & Act
        // This test verifies that specifying a buildDate finds the first release on or after that date
        var buildDate = new DateTime(2024, 1, 15);
        var runtime = await _manager.GetOrCreateInstanceAsync("3.12.0", buildDate);
        var info = _manager.GetInstanceInfo("3.12.0", buildDate);
        
        // Assert
        Assert.That(info, Is.Not.Null);
        Assert.That(info!.BuildDate.Date, Is.GreaterThanOrEqualTo(buildDate.Date));
    }

    [Test]
    [Category("Integration")]
    public async Task GetOrCreateInstance_WithNullBuildDate_UsesLatestRelease()
    {
        // Arrange & Act
        // This test verifies that null buildDate uses the latest release
        var runtime = await _manager.GetOrCreateInstanceAsync("3.12.0", null);
        var info = _manager.GetInstanceInfo("3.12.0", null);
        
        // Assert
        Assert.That(info, Is.Not.Null);
        Assert.That(info!.WasLatestBuild, Is.True);
    }

    [Test]
    [Category("Integration")]
    public async Task GetOrCreateInstance_WithBuildDate_AndPartialVersion_FindsCorrectRelease()
    {
        // Arrange & Act
        // This test verifies that partial version + buildDate works together
        var buildDate = new DateTime(2024, 1, 15);
        var runtime = await _manager.GetOrCreateInstanceAsync("3.10", buildDate);
        var info = _manager.GetInstanceInfo("3.10", buildDate);
        
        // Assert
        Assert.That(info, Is.Not.Null);
        Assert.That(info!.PythonVersion, Does.StartWith("3.10."));
        Assert.That(info.BuildDate.Date, Is.GreaterThanOrEqualTo(buildDate.Date));
    }
}

