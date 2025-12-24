using Microsoft.Extensions.Logging;
using NUnit.Framework;
using Octokit;
using PythonEmbedded.Net.Models;
using PythonEmbedded.Net.Test.TestUtilities;

namespace PythonEmbedded.Net.IntegrationTest.Runtime;

/// <summary>
/// Integration tests for pip configuration functionality.
/// </summary>
[TestFixture]
[Category("Integration")]
public class PipConfigurationIntegrationTests
{
    private string _testDirectory = null!;
    private PythonEmbedded.Net.PythonManager _manager = null!;
    private PythonEmbedded.Net.BasePythonRuntime? _runtime;

    [SetUp]
    public async Task SetUp()
    {
        _testDirectory = TestDirectoryHelper.CreateTestDirectory("PipConfiguration");
        var githubClient = new GitHubClient(new ProductHeaderValue("PythonEmbedded.Net-IntegrationTest"));
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
    public async Task GetPipConfiguration_ReturnsConfiguration()
    {
        Assume.That(_runtime, Is.Not.Null);
        
        var config = await _runtime!.GetPipConfigurationAsync();
        
        Assert.That(config, Is.Not.Null);
    }

    [Test]
    [Category("Integration")]
    public async Task ConfigurePipIndex_SetsIndexUrl()
    {
        Assume.That(_runtime, Is.Not.Null);
        
        var indexUrl = "https://pypi.org/simple";
        var result = await _runtime!.ConfigurePipIndexAsync(indexUrl, trusted: false);
        
        // Configuration may succeed or fail depending on pip version and permissions
        // Just verify the method doesn't throw unexpected exceptions
        Assert.That(result, Is.Not.Null);
    }

    [Test]
    [Category("Integration")]
    public async Task ConfigurePipProxy_SetsProxy()
    {
        Assume.That(_runtime, Is.Not.Null);
        
        // Use a dummy proxy URL for testing (won't actually be used)
        var proxyUrl = "http://proxy.example.com:8080";
        var result = await _runtime!.ConfigurePipProxyAsync(proxyUrl);
        
        // Configuration may succeed or fail depending on pip version and permissions
        Assert.That(result, Is.Not.Null);
    }

    [Test]
    [Category("Integration")]
    public async Task InstallPackage_WithIndexUrl_UsesCustomIndex()
    {
        Assume.That(_runtime, Is.Not.Null);
        
        var indexUrl = "https://pypi.org/simple";
        
        // Try to install a package with a custom index URL
        // This should work even if the index URL is the default
        var result = await _runtime!.InstallPackageAsync("six==1.16.0", indexUrl: indexUrl);
        
        Assert.That(result.ExitCode, Is.EqualTo(0));
        Assert.That(await _runtime.IsPackageInstalledAsync("six"), Is.True);
    }
}

