using Microsoft.Extensions.Logging;
using NUnit.Framework;
using Octokit;
using PythonEmbedded.Net.Models;
using PythonEmbedded.Net.Test.TestUtilities;

namespace PythonEmbedded.Net.Test.Integration;

/// <summary>
/// Integration tests for Python virtual environment functionality.
/// These tests download and use real Python installations from GitHub.
/// Note: These tests require network access and may take longer to run.
/// </summary>
[TestFixture]
[Category("Integration")]
public class PythonRuntimeVirtualEnvironmentTests
{
    private string _testDirectory = null!;
    private PythonEmbedded.Net.PythonManager _manager = null!;
    private PythonEmbedded.Net.BasePythonRootRuntime? _runtime;

    [SetUp]
    public async Task SetUp()
    {
        _testDirectory = TestDirectoryHelper.CreateTestDirectory("PythonRuntimeVirtualEnvironment");
        
        // Create a GitHub client (using unauthenticated client for tests)
        var githubClient = new GitHubClient(new ProductHeaderValue("PythonEmbedded.Net-Test"));
        
        // Create the manager
        _manager = new PythonEmbedded.Net.PythonManager(_testDirectory, githubClient);
        
        // Download and set up a real Python 3.12 instance (this may take some time)
        var runtimeBase = await _manager.GetOrCreateInstanceAsync("3.12", cancellationToken: default);
        _runtime = runtimeBase as PythonEmbedded.Net.PythonRootRuntime;
        
        Assume.That(_runtime, Is.Not.Null, "Runtime must be a PythonRootRuntime for virtual environment tests");
    }

    [TearDown]
    public void TearDown()
    {
        TestDirectoryHelper.DeleteTestDirectory(_testDirectory);
    }

    [Test]
    [Category("Integration")]
    // Note: This test may take 10+ minutes due to Python download/extraction and venv creation
    public async Task GetOrCreateVirtualEnvironment_WhenDoesNotExist_CreatesVirtualEnvironment()
    {
        // Arrange
        Assume.That(_runtime, Is.Not.Null, "Python runtime was not successfully set up");
        var venvName = $"test-venv-{Guid.NewGuid():N}";
        
        // Act
        var venv = await _runtime!.GetOrCreateVirtualEnvironmentAsync(venvName);
        
        // Assert
        Assert.That(venv, Is.Not.Null);
        var venvList = _runtime.ListVirtualEnvironments();
        Assert.That(venvList, Contains.Item(venvName));
    }

    [Test]
    [Category("Integration")]
    // Note: This test may take 10+ minutes due to Python download/extraction and venv creation
    public async Task DeleteVirtualEnvironment_WhenExists_DeletesVirtualEnvironment()
    {
        // Arrange
        Assume.That(_runtime, Is.Not.Null, "Python runtime was not successfully set up");
        var venvName = $"test-venv-{Guid.NewGuid():N}";
        await _runtime!.GetOrCreateVirtualEnvironmentAsync(venvName);
        
        // Act
        var result = await _runtime.DeleteVirtualEnvironmentAsync(venvName);
        
        // Assert
        Assert.That(result, Is.True);
        var venvList = _runtime.ListVirtualEnvironments();
        Assert.That(venvList, Does.Not.Contain(venvName));
    }
}
