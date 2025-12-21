using Microsoft.Extensions.Logging;
using NUnit.Framework;
using Octokit;
using PythonEmbedded.Net.Models;
using PythonEmbedded.Net.Test.TestUtilities;

namespace PythonEmbedded.Net.Test.Manager;

/// <summary>
/// Tests for PythonManager.
/// </summary>
[TestFixture]
public class PythonManagerTests
{
    private string _testDirectory = null!;
    private GitHubClient _githubClient = null!;

    [SetUp]
    public void SetUp()
    {
        _testDirectory = TestDirectoryHelper.CreateTestDirectory("PythonManager");
        _githubClient = new GitHubClient(new ProductHeaderValue("PythonEmbedded.Net-Test"));
    }

    [TearDown]
    public void TearDown()
    {
        TestDirectoryHelper.DeleteTestDirectory(_testDirectory);
    }

    [Test]
    public void Constructor_WithValidParameters_CreatesInstance()
    {
        // Act
        var manager = new PythonManager(_testDirectory, _githubClient);

        // Assert
        Assert.That(manager, Is.Not.Null);
    }

    [Test]
    public void Constructor_WithLogger_CreatesInstance()
    {
        // Arrange
        var loggerFactory = LoggerFactory.Create(builder => builder.AddConsole());
        var logger = loggerFactory.CreateLogger<PythonManager>();

        // Act
        var manager = new PythonManager(_testDirectory, _githubClient, logger, loggerFactory);

        // Assert
        Assert.That(manager, Is.Not.Null);
        loggerFactory.Dispose();
    }

    [Test]
    public void Constructor_WithNullDirectory_ThrowsArgumentException()
    {
        // Act & Assert
        Assert.Throws<ArgumentException>(() => new PythonManager(null!, _githubClient));
    }

    [Test]
    public void Constructor_WithEmptyDirectory_ThrowsArgumentException()
    {
        // Act & Assert
        Assert.Throws<ArgumentException>(() => new PythonManager(string.Empty, _githubClient));
    }

    [Test]
    public void GetPythonRuntimeForInstance_WithValidMetadata_ReturnsRuntime()
    {
        // Arrange
        var manager = new PythonManager(_testDirectory, _githubClient);
        var metadata = MockPythonInstanceHelper.CreateMockPythonInstance(_testDirectory, "3.12.0", "20240115");

        // Act
        var runtime = manager.GetPythonRuntimeForInstance(metadata);

        // Assert
        Assert.That(runtime, Is.Not.Null);
        Assert.That(runtime, Is.InstanceOf<PythonEmbedded.Net.PythonRootRuntime>());
    }

    [Test]
    public void GetPythonRuntimeForInstance_WithNullMetadata_ThrowsArgumentNullException()
    {
        // Arrange
        var manager = new PythonManager(_testDirectory, _githubClient);

        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => manager.GetPythonRuntimeForInstance(null!));
    }

    [Test]
    public void ListInstances_Initially_ReturnsEmptyList()
    {
        // Arrange
        var manager = new PythonManager(_testDirectory, _githubClient);

        // Act
        var instances = manager.ListInstances();

        // Assert
        Assert.That(instances, Is.Not.Null);
        Assert.That(instances.Count, Is.EqualTo(0));
    }

    // Note: Integration tests for GetOrCreateInstanceAsync would require:
    // - GitHub API access
    // - Actual Python distribution downloads
    // These should be run separately as integration tests
}
