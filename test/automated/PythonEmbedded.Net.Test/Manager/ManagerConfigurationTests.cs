using Microsoft.Extensions.Logging;
using NUnit.Framework;
using Octokit;
using PythonEmbedded.Net.Models;
using PythonEmbedded.Net.Test.TestUtilities;

namespace PythonEmbedded.Net.Test.Manager;

/// <summary>
/// Tests for manager configuration functionality.
/// </summary>
[TestFixture]
public class ManagerConfigurationTests
{
    private string _testDirectory = null!;
    private GitHubClient _githubClient = null!;

    [SetUp]
    public void SetUp()
    {
        _testDirectory = TestDirectoryHelper.CreateTestDirectory("ManagerConfiguration");
        _githubClient = new GitHubClient(new ProductHeaderValue("PythonEmbedded.Net-Test"));
    }

    [TearDown]
    public void TearDown()
    {
        TestDirectoryHelper.DeleteTestDirectory(_testDirectory);
    }

    [Test]
    public void Constructor_WithCustomConfiguration_UsesConfiguration()
    {
        // Arrange
        var config = new ManagerConfiguration
        {
            DefaultPythonVersion = "3.11",
            DefaultPipIndexUrl = "https://custom-pypi.example.com/simple",
            ProxyUrl = "http://proxy.example.com:8080",
            DefaultTimeout = TimeSpan.FromMinutes(10),
            RetryAttempts = 5,
            RetryDelay = TimeSpan.FromSeconds(2),
            UseExponentialBackoff = false
        };

        // Act
        var manager = new PythonEmbedded.Net.PythonManager(_testDirectory, _githubClient, configuration: config);

        // Assert
        Assert.That(manager.Configuration, Is.Not.Null);
        Assert.That(manager.Configuration.DefaultPythonVersion, Is.EqualTo("3.11"));
        Assert.That(manager.Configuration.DefaultPipIndexUrl, Is.EqualTo("https://custom-pypi.example.com/simple"));
        Assert.That(manager.Configuration.ProxyUrl, Is.EqualTo("http://proxy.example.com:8080"));
        Assert.That(manager.Configuration.DefaultTimeout, Is.EqualTo(TimeSpan.FromMinutes(10)));
        Assert.That(manager.Configuration.RetryAttempts, Is.EqualTo(5));
        Assert.That(manager.Configuration.RetryDelay, Is.EqualTo(TimeSpan.FromSeconds(2)));
        Assert.That(manager.Configuration.UseExponentialBackoff, Is.False);
    }

    [Test]
    public void Constructor_WithoutConfiguration_UsesDefaultConfiguration()
    {
        // Act
        var manager = new PythonEmbedded.Net.PythonManager(_testDirectory, _githubClient);

        // Assert
        Assert.That(manager.Configuration, Is.Not.Null);
        Assert.That(manager.Configuration.RetryAttempts, Is.EqualTo(3)); // Default value
        Assert.That(manager.Configuration.UseExponentialBackoff, Is.True); // Default value
    }

    [Test]
    public void Configuration_CanBeModified()
    {
        // Arrange
        var manager = new PythonEmbedded.Net.PythonManager(_testDirectory, _githubClient);
        var originalRetryAttempts = manager.Configuration.RetryAttempts;

        // Act
        manager.Configuration.RetryAttempts = 10;

        // Assert
        Assert.That(manager.Configuration.RetryAttempts, Is.EqualTo(10));
        Assert.That(manager.Configuration.RetryAttempts, Is.Not.EqualTo(originalRetryAttempts));
    }

    [Test]
    public void Configuration_SetToNull_ThrowsArgumentNullException()
    {
        // Arrange
        var manager = new PythonEmbedded.Net.PythonManager(_testDirectory, _githubClient);

        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => manager.Configuration = null!);
    }

    // Integration tests that require GitHub API access have been moved to:
    // test/automated/PythonEmbedded.Net.IntegrationTest/Manager/ManagerConfigurationIntegrationTests.cs

    [Test]
    public void ManagerConfiguration_DefaultValues_AreCorrect()
    {
        // Act
        var config = new ManagerConfiguration();

        // Assert
        Assert.That(config.RetryAttempts, Is.EqualTo(3));
        Assert.That(config.RetryDelay, Is.EqualTo(TimeSpan.FromSeconds(1)));
        Assert.That(config.UseExponentialBackoff, Is.True);
        Assert.That(config.DefaultPythonVersion, Is.Null);
        Assert.That(config.DefaultPipIndexUrl, Is.Null);
        Assert.That(config.ProxyUrl, Is.Null);
        Assert.That(config.DefaultTimeout, Is.Null);
    }

    [Test]
    public void PythonNetManager_WithCustomConfiguration_UsesConfiguration()
    {
        // Arrange
        var config = new ManagerConfiguration
        {
            DefaultPythonVersion = "3.11",
            RetryAttempts = 7
        };

        // Act
        var manager = new PythonEmbedded.Net.PythonNetManager(_testDirectory, _githubClient, configuration: config);

        // Assert
        Assert.That(manager.Configuration, Is.Not.Null);
        Assert.That(manager.Configuration.DefaultPythonVersion, Is.EqualTo("3.11"));
        Assert.That(manager.Configuration.RetryAttempts, Is.EqualTo(7));
    }
}

