using Microsoft.Extensions.Logging;
using NUnit.Framework;
using Octokit;
using PythonEmbedded.Net.Models;
using PythonEmbedded.Net.Test.TestUtilities;

namespace PythonEmbedded.Net.IntegrationTest.Runtime;

/// <summary>
/// Integration tests for virtual environment operations.
/// These tests require GitHub API access and may hit rate limits.
/// Run these tests separately when needed.
/// </summary>
[TestFixture]
[Category("Integration")]
public class VirtualEnvironmentOperationsIntegrationTests
{
    private string _testDirectory = null!;
    private PythonEmbedded.Net.PythonManager _manager = null!;
    private PythonEmbedded.Net.PythonRootRuntime? _runtime;

    [SetUp]
    public async Task SetUp()
    {
        _testDirectory = TestDirectoryHelper.CreateTestDirectory("VirtualEnvironmentOperations");
        var githubClient = new GitHubClient(new ProductHeaderValue("PythonEmbedded.Net-IntegrationTest"));
        _manager = new PythonEmbedded.Net.PythonManager(_testDirectory, githubClient);
        var baseRuntime = await _manager.GetOrCreateInstanceAsync("3.12", cancellationToken: default);
        _runtime = baseRuntime as PythonEmbedded.Net.PythonRootRuntime;
    }

    [TearDown]
    public void TearDown()
    {
        TestDirectoryHelper.DeleteTestDirectory(_testDirectory);
    }

    [Test]
    [Category("Integration")]
    public async Task GetVirtualEnvironmentSize_WithExistingVenv_ReturnsSize()
    {
        Assume.That(_runtime, Is.Not.Null);
        
        // Create a virtual environment
        var venv = await _runtime!.GetOrCreateVirtualEnvironmentAsync("test_venv");
        
        // Act
        var size = _runtime.GetVirtualEnvironmentSize("test_venv");
        
        // Assert
        Assert.That(size, Is.GreaterThan(0));
    }

    [Test]
    [Category("Integration")]
    public void GetVirtualEnvironmentSize_WithNonExistentVenv_ThrowsDirectoryNotFoundException()
    {
        Assume.That(_runtime, Is.Not.Null);
        
        Assert.Throws<DirectoryNotFoundException>(() => _runtime!.GetVirtualEnvironmentSize("nonexistent"));
    }

    [Test]
    [Category("Integration")]
    public async Task GetVirtualEnvironmentInfo_WithExistingVenv_ReturnsInfo()
    {
        Assume.That(_runtime, Is.Not.Null);
        
        // Create a virtual environment
        var venv = await _runtime!.GetOrCreateVirtualEnvironmentAsync("test_venv");
        
        // Act
        var info = _runtime.GetVirtualEnvironmentInfo("test_venv");
        
        // Assert
        Assert.That(info, Is.Not.Null);
        Assert.That(info.ContainsKey("Name"), Is.True);
        Assert.That(info.ContainsKey("Path"), Is.True);
        Assert.That(info.ContainsKey("SizeBytes"), Is.True);
        Assert.That(info.ContainsKey("Exists"), Is.True);
        Assert.That(info["Name"], Is.EqualTo("test_venv"));
        Assert.That(info["Exists"], Is.True);
        Assert.That((long)info["SizeBytes"], Is.GreaterThan(0));
    }

    [Test]
    [Category("Integration")]
    public void GetVirtualEnvironmentInfo_WithNonExistentVenv_ThrowsDirectoryNotFoundException()
    {
        Assume.That(_runtime, Is.Not.Null);
        
        Assert.Throws<DirectoryNotFoundException>(() => _runtime!.GetVirtualEnvironmentInfo("nonexistent"));
    }
}

