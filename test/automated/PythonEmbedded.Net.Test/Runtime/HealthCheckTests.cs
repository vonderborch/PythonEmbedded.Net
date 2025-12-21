using Microsoft.Extensions.Logging;
using NUnit.Framework;
using Octokit;
using PythonEmbedded.Net.Models;
using PythonEmbedded.Net.Test.TestUtilities;

namespace PythonEmbedded.Net.Test.Runtime;

/// <summary>
/// Integration tests for health check functionality.
/// </summary>
[TestFixture]
[Category("Integration")]
public class HealthCheckTests
{
    private string _testDirectory = null!;
    private PythonEmbedded.Net.PythonManager _manager = null!;
    private PythonEmbedded.Net.BasePythonRuntime? _runtime;

    [SetUp]
    public async Task SetUp()
    {
        _testDirectory = TestDirectoryHelper.CreateTestDirectory("HealthCheck");
        var githubClient = new GitHubClient(new ProductHeaderValue("PythonEmbedded.Net-Test"));
        _manager = new PythonEmbedded.Net.PythonManager(_testDirectory, githubClient);
        _runtime = await _manager.GetOrCreateInstanceAsync("3.12", cancellationToken: default);
    }

    [TearDown]
    public void TearDown()
    {
        TestDirectoryHelper.DeleteTestDirectory(_testDirectory);
    }

    [Test]
    [Category("Integration")]
    public async Task ValidatePythonInstallation_ReturnsHealthResults()
    {
        Assume.That(_runtime, Is.Not.Null);
        
        var results = await _runtime!.ValidatePythonInstallationAsync();
        
        Assert.That(results, Is.Not.Null);
        Assert.That(results.ContainsKey("ExecutableExists"), Is.True);
        Assert.That(results.ContainsKey("WorkingDirectoryExists"), Is.True);
        Assert.That(results.ContainsKey("PythonVersionCheck"), Is.True);
        Assert.That(results.ContainsKey("PipCheck"), Is.True);
        Assert.That(results.ContainsKey("CommandExecution"), Is.True);
        Assert.That(results.ContainsKey("OverallHealth"), Is.True);
        
        Assert.That(results["ExecutableExists"], Is.True);
        Assert.That(results["OverallHealth"], Is.EqualTo("Healthy"));
    }
}

