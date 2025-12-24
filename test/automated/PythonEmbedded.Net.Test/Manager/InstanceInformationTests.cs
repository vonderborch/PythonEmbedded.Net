// Integration tests that require GitHub API access have been moved to:
// test/automated/PythonEmbedded.Net.IntegrationTest/Manager/InstanceInformationIntegrationTests.cs

using Microsoft.Extensions.Logging;
using NUnit.Framework;
using Octokit;
using PythonEmbedded.Net.Models;
using PythonEmbedded.Net.Test.TestUtilities;

namespace PythonEmbedded.Net.Test.Manager;

/// <summary>
/// Unit tests for instance information that don't require GitHub API.
/// </summary>
[TestFixture]
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
    public void GetInstanceInfo_WithNonExistentInstance_ReturnsNull()
    {
        // This test doesn't require GitHub API - it just checks for non-existent instance
        var info = _manager.GetInstanceInfo("99.99.99");
        
        Assert.That(info, Is.Null);
    }

    [Test]
    public void GetInstanceSize_WithNonExistentInstance_ReturnsZero()
    {
        // This test doesn't require GitHub API - it just checks for non-existent instance
        var size = _manager.GetInstanceSize("99.99.99");
        
        Assert.That(size, Is.EqualTo(0));
    }

    [Test]
    public void ValidateInstanceIntegrity_WithNonExistentInstance_ReturnsFalse()
    {
        // This test doesn't require GitHub API - it just checks for non-existent instance
        var isValid = _manager.ValidateInstanceIntegrity("99.99.99");
        
        Assert.That(isValid, Is.False);
    }

    [Test]
    public void GetSystemRequirements_ReturnsRequirements()
    {
        // This test doesn't require GitHub API - it just checks system info
        var requirements = _manager.GetSystemRequirements();
        
        Assert.That(requirements, Is.Not.Null);
        Assert.That(requirements.ContainsKey("Platform"), Is.True);
        Assert.That(requirements.ContainsKey("Architecture"), Is.True);
        Assert.That(requirements.ContainsKey("TargetTriple"), Is.True);
    }

    [Test]
    public void CheckDiskSpace_ReturnsCorrectResult()
    {
        // This test doesn't require GitHub API - it just checks disk space
        var hasSpace = _manager.CheckDiskSpace(1024L * 1024 * 1024); // 1 GB
        
        Assert.That(hasSpace, Is.True); // Should have at least 1GB free
    }
}

