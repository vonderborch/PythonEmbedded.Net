using Microsoft.Extensions.Logging;
using NUnit.Framework;
using Octokit;
using PythonEmbedded.Net.Models;
using PythonEmbedded.Net.Test.TestUtilities;

namespace PythonEmbedded.Net.IntegrationTest.Manager;

/// <summary>
/// Integration tests for manager configuration that require GitHub API access.
/// Run these tests separately when needed.
/// </summary>
[TestFixture]
[Category("Integration")]
public class ManagerConfigurationIntegrationTests
{
    private string _testDirectory = null!;
    private GitHubClient _githubClient = null!;

    [SetUp]
    public void SetUp()
    {
        _testDirectory = TestDirectoryHelper.CreateTestDirectory("ManagerConfiguration");
        _githubClient = new GitHubClient(new ProductHeaderValue("PythonEmbedded.Net-IntegrationTest"));
    }

    [TearDown]
    public void TearDown()
    {
        TestDirectoryHelper.DeleteTestDirectory(_testDirectory);
    }

    [Test]
    [Category("Integration")]
    public async Task GetOrCreateInstanceAsync_WithDefaultVersion_UsesConfiguration()
    {
        // Arrange
        var config = new ManagerConfiguration
        {
            DefaultPythonVersion = "3.12"
        };
        var manager = new PythonEmbedded.Net.PythonManager(_testDirectory, _githubClient, configuration: config);

        // Act - Don't specify version, should use default from configuration
        var runtime = await manager.GetOrCreateInstanceAsync(pythonVersion: null, cancellationToken: default);

        // Assert
        Assert.That(runtime, Is.Not.Null);
        // The instance should be for the default version from configuration
        var instances = manager.ListInstances();
        Assert.That(instances.Count, Is.GreaterThan(0));
        Assert.That(instances[0].PythonVersion, Does.StartWith("3.12"));
    }

    [Test]
    [Category("Integration")]
    public async Task GetOrCreateInstanceAsync_WithExplicitVersion_OverridesConfiguration()
    {
        // Arrange
        var config = new ManagerConfiguration
        {
            DefaultPythonVersion = "3.11"
        };
        var manager = new PythonEmbedded.Net.PythonManager(_testDirectory, _githubClient, configuration: config);

        // Act - Specify version explicitly, should override default
        var runtime = await manager.GetOrCreateInstanceAsync("3.12", cancellationToken: default);

        // Assert
        Assert.That(runtime, Is.Not.Null);
        var instances = manager.ListInstances();
        Assert.That(instances[0].PythonVersion, Does.StartWith("3.12"));
    }
}

