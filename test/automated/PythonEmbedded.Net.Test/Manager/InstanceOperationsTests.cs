using Microsoft.Extensions.Logging;
using NUnit.Framework;
using Octokit;
using PythonEmbedded.Net.Models;
using PythonEmbedded.Net.Test.TestUtilities;

namespace PythonEmbedded.Net.Test.Manager;

/// <summary>
/// Integration tests for instance export and import operations.
/// </summary>
[TestFixture]
[Category("Integration")]
public class InstanceOperationsTests
{
    private string _testDirectory = null!;
    private PythonEmbedded.Net.PythonManager _manager = null!;

    [SetUp]
    public void SetUp()
    {
        _testDirectory = TestDirectoryHelper.CreateTestDirectory("InstanceOperations");
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
    public async Task ExportInstance_ExportsSuccessfully()
    {
        // Create an instance
        var runtime = await _manager.GetOrCreateInstanceAsync("3.12");
        
        // Export it
        var exportPath = Path.Combine(_testDirectory, "instance_export.zip");
        var resultPath = await _manager.ExportInstanceAsync("3.12", exportPath);
        
        // Verify the archive was created
        Assert.That(File.Exists(resultPath), Is.True);
    }

    [Test]
    [Category("Integration")]
    public async Task CheckDiskSpace_ReturnsCorrectResult()
    {
        var hasSpace = _manager.CheckDiskSpace(1024L * 1024 * 1024); // 1 GB
        
        Assert.That(hasSpace, Is.True); // Should have at least 1GB free
    }

    [Test]
    [Category("Integration")]
    public async Task TestNetworkConnectivity_ReturnsResult()
    {
        var isConnected = await _manager.TestNetworkConnectivityAsync();
        
        // May or may not be connected, but should not throw
        Assert.That(true, Is.True);
    }

    [Test]
    [Category("Integration")]
    public async Task GetSystemRequirements_ReturnsRequirements()
    {
        var requirements = _manager.GetSystemRequirements();
        
        Assert.That(requirements, Is.Not.Null);
        Assert.That(requirements.ContainsKey("Platform"), Is.True);
        Assert.That(requirements.ContainsKey("Architecture"), Is.True);
        Assert.That(requirements.ContainsKey("TargetTriple"), Is.True);
    }

    [Test]
    [Category("Integration")]
    public async Task DiagnoseIssues_ReturnsIssues()
    {
        var issues = await _manager.DiagnoseIssuesAsync();
        
        Assert.That(issues, Is.Not.Null);
        // May or may not have issues, but should return a list
    }
}

