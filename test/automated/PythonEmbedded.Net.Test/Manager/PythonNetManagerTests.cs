using Microsoft.Extensions.Logging;
using NUnit.Framework;
using Octokit;
using PythonEmbedded.Net.Models;
using PythonEmbedded.Net.Test.TestUtilities;

namespace PythonEmbedded.Net.Test.Manager;

/// <summary>
/// Tests for PythonNetManager.
/// </summary>
[TestFixture]
public class PythonNetManagerTests
{
    private string _testDirectory = null!;
    private GitHubClient _githubClient = null!;

    [SetUp]
    public void SetUp()
    {
        _testDirectory = TestDirectoryHelper.CreateTestDirectory("PythonNetManager");
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
        var manager = new PythonNetManager(_testDirectory, _githubClient);

        // Assert
        Assert.That(manager, Is.Not.Null);
    }

    [Test]
    public void Constructor_WithLogger_CreatesInstance()
    {
        // Arrange
        var loggerFactory = LoggerFactory.Create(builder => builder.AddConsole());
        var logger = loggerFactory.CreateLogger<PythonNetManager>();

        // Act
        var manager = new PythonNetManager(_testDirectory, _githubClient, logger, loggerFactory);

        // Assert
        Assert.That(manager, Is.Not.Null);
        loggerFactory.Dispose();
    }
    
    [Test]
    public void GetPythonRuntimeForInstance_WithNullMetadata_ThrowsArgumentNullException()
    {
        // Arrange
        var manager = new PythonNetManager(_testDirectory, _githubClient);

        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => manager.GetPythonRuntimeForInstance(null!));
    }
}
