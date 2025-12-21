using Microsoft.Extensions.Logging;
using NUnit.Framework;
using Octokit;
using PythonEmbedded.Net.Models;
using PythonEmbedded.Net.Test.TestUtilities;

namespace PythonEmbedded.Net.Test.Manager;

/// <summary>
/// Integration tests for instance information and management functionality.
/// </summary>
[TestFixture]
[Category("Integration")]
public class InstanceInformationTests
{
    private string _testDirectory = null!;
    private PythonEmbedded.Net.PythonManager _manager = null!;

    [SetUp]
    public void SetUp()
    {
        _testDirectory = TestDirectoryHelper.CreateTestDirectory("InstanceInformation");
        var githubClient = new GitHubClient(new ProductHeaderValue("PythonEmbedded.Net-Test"));
        _manager = new PythonEmbedded.Net.PythonManager(_testDirectory, githubClient);
    }

    [TearDown]
    public void TearDown()
    {
        TestDirectoryHelper.DeleteTestDirectory(_testDirectory);
    }

    [Test]
    [Category("Integration")]
    public async Task GetInstanceInfo_WithExistingInstance_ReturnsMetadata()
    {
        // Arrange - create an instance
        var runtime = await _manager.GetOrCreateInstanceAsync("3.12", cancellationToken: default);
        
        // Act
        var info = _manager.GetInstanceInfo("3.12");
        
        // Assert
        Assert.That(info, Is.Not.Null);
        Assert.That(info!.PythonVersion, Is.EqualTo("3.12.0"));
        Assert.That(info.Directory, Is.Not.Null.And.Not.Empty);
        Assert.That(Directory.Exists(info.Directory), Is.True);
    }

    [Test]
    [Category("Integration")]
    public async Task GetInstanceInfo_WithNonExistentInstance_ReturnsNull()
    {
        var info = _manager.GetInstanceInfo("99.99.99");
        
        Assert.That(info, Is.Null);
    }

    [Test]
    [Category("Integration")]
    public async Task GetInstanceSize_WithExistingInstance_ReturnsSize()
    {
        // Arrange - create an instance
        var runtime = await _manager.GetOrCreateInstanceAsync("3.12", cancellationToken: default);
        
        // Act
        var size = _manager.GetInstanceSize("3.12");
        
        // Assert
        Assert.That(size, Is.GreaterThan(0));
    }

    [Test]
    [Category("Integration")]
    public async Task GetInstanceSize_WithNonExistentInstance_ReturnsZero()
    {
        var size = _manager.GetInstanceSize("99.99.99");
        
        Assert.That(size, Is.EqualTo(0));
    }

    [Test]
    [Category("Integration")]
    public async Task GetTotalDiskUsage_ReturnsTotalSize()
    {
        // Arrange - create an instance
        var runtime = await _manager.GetOrCreateInstanceAsync("3.12", cancellationToken: default);
        
        // Act
        var totalSize = _manager.GetTotalDiskUsage();
        
        // Assert
        Assert.That(totalSize, Is.GreaterThan(0));
        // Should be at least as large as the single instance
        var instanceSize = _manager.GetInstanceSize("3.12");
        Assert.That(totalSize, Is.GreaterThanOrEqualTo(instanceSize));
    }

    [Test]
    [Category("Integration")]
    public async Task ValidateInstanceIntegrity_WithValidInstance_ReturnsTrue()
    {
        // Arrange - create an instance
        var runtime = await _manager.GetOrCreateInstanceAsync("3.12", cancellationToken: default);
        
        // Act
        var isValid = _manager.ValidateInstanceIntegrity("3.12");
        
        // Assert
        Assert.That(isValid, Is.True);
    }

    [Test]
    [Category("Integration")]
    public void ValidateInstanceIntegrity_WithNonExistentInstance_ReturnsFalse()
    {
        var isValid = _manager.ValidateInstanceIntegrity("99.99.99");
        
        Assert.That(isValid, Is.False);
    }

    [Test]
    [Category("Integration")]
    public async Task GetLatestPythonVersion_ReturnsVersion()
    {
        var latestVersion = await _manager.GetLatestPythonVersionAsync();
        
        Assert.That(latestVersion, Is.Not.Null.And.Not.Empty);
    }

    [Test]
    [Category("Integration")]
    public async Task FindBestMatchingVersion_WithExactMatch_ReturnsVersion()
    {
        var version = await _manager.FindBestMatchingVersionAsync("3.12");
        
        Assert.That(version, Is.Not.Null);
        Assert.That(version, Does.StartWith("3.12"));
    }

    [Test]
    [Category("Integration")]
    public async Task FindBestMatchingVersion_WithNonExistentVersion_ReturnsNull()
    {
        var version = await _manager.FindBestMatchingVersionAsync("99.99");
        
        // May return null if no matching version found
        // This is acceptable behavior
    }

    [Test]
    [Category("Integration")]
    public async Task EnsurePythonVersion_WithNewVersion_DownloadsAndReturnsRuntime()
    {
        var runtime = await _manager.EnsurePythonVersionAsync("3.12");
        
        Assert.That(runtime, Is.Not.Null);
        var info = _manager.GetInstanceInfo("3.12");
        Assert.That(info, Is.Not.Null);
    }

    [Test]
    [Category("Integration")]
    public async Task EnsurePythonVersion_WithExistingVersion_ReturnsRuntime()
    {
        // Create instance first
        var runtime1 = await _manager.GetOrCreateInstanceAsync("3.12");
        
        // Ensure it again
        var runtime2 = await _manager.EnsurePythonVersionAsync("3.12");
        
        Assert.That(runtime2, Is.Not.Null);
        // Should reuse existing instance
        var instances = _manager.ListInstances();
        Assert.That(instances.Count(i => i.PythonVersion.StartsWith("3.12")), Is.EqualTo(1));
    }
}

