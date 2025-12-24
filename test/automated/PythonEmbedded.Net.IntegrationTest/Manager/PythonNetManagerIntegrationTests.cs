using Microsoft.Extensions.Logging;
using NUnit.Framework;
using Octokit;
using PythonEmbedded.Net.Models;
using PythonEmbedded.Net.Test.TestUtilities;

namespace PythonEmbedded.Net.IntegrationTest.Manager;

/// <summary>
/// Integration tests for PythonNetManager that require real Python installations.
/// These tests require GitHub API access and may hit rate limits.
/// Run these tests separately when needed.
/// </summary>
[TestFixture]
[Category("Integration")]
public class PythonNetManagerIntegrationTests
{
    private string _testDirectory = null!;
    private GitHubClient _githubClient = null!;

    [SetUp]
    public void SetUp()
    {
        _testDirectory = TestDirectoryHelper.CreateTestDirectory("PythonNetManager");
        _githubClient = new GitHubClient(new ProductHeaderValue("PythonEmbedded.Net-IntegrationTest"));
    }

    [TearDown]
    public void TearDown()
    {
        TestDirectoryHelper.DeleteTestDirectory(_testDirectory);
    }

    [Test]
    [Category("Integration")]
    public async Task GetPythonRuntimeForInstance_WithValidMetadata_ReturnsPythonNetRuntime()
    {
        // Arrange
        // This test requires a real Python installation because Python.NET needs
        // actual Python DLLs to initialize.
        var manager = new PythonEmbedded.Net.PythonNetManager(_testDirectory, _githubClient);
        
        // Get a real Python instance
        var baseRuntime = await manager.GetOrCreateInstanceAsync("3.12", cancellationToken: default);
        var metadata = manager.GetInstanceInfo("3.12");

        // Act
        var runtime = manager.GetPythonRuntimeForInstance(metadata!);

        // Assert
        Assert.That(runtime, Is.Not.Null);
        Assert.That(runtime, Is.InstanceOf<PythonEmbedded.Net.PythonNetRootRuntime>());
    }
}

