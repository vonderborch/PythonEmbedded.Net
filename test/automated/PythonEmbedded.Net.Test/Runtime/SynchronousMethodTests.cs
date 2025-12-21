using Microsoft.Extensions.Logging;
using NUnit.Framework;
using Octokit;
using PythonEmbedded.Net.Models;
using PythonEmbedded.Net.Test.TestUtilities;

namespace PythonEmbedded.Net.Test.Runtime;

/// <summary>
/// Tests for synchronous wrapper methods.
/// </summary>
[TestFixture]
[Category("Integration")]
public class SynchronousMethodTests
{
    private string _testDirectory = null!;
    private PythonEmbedded.Net.PythonManager _manager = null!;
    private PythonEmbedded.Net.BasePythonRuntime? _runtime;

    [SetUp]
    public async Task SetUp()
    {
        _testDirectory = TestDirectoryHelper.CreateTestDirectory("SynchronousMethods");
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
    public void ListInstalledPackages_Synchronous_ReturnsPackages()
    {
        Assume.That(_runtime, Is.Not.Null);
        
        var packages = _runtime!.ListInstalledPackages();
        
        Assert.That(packages, Is.Not.Null);
        Assert.That(packages.Count, Is.GreaterThan(0));
    }

    [Test]
    [Category("Integration")]
    public void GetPackageVersion_Synchronous_ReturnsVersion()
    {
        Assume.That(_runtime, Is.Not.Null);
        
        // Install a package first
        _runtime!.InstallPackage("six==1.16.0");
        
        var version = _runtime.GetPackageVersion("six");
        
        Assert.That(version, Is.Not.Null);
    }

    [Test]
    [Category("Integration")]
    public void IsPackageInstalled_Synchronous_ReturnsBoolean()
    {
        Assume.That(_runtime, Is.Not.Null);
        
        _runtime!.InstallPackage("six==1.16.0");
        
        var isInstalled = _runtime.IsPackageInstalled("six");
        
        Assert.That(isInstalled, Is.True);
    }

    [Test]
    [Category("Integration")]
    public void GetPackageInfo_Synchronous_ReturnsInfo()
    {
        Assume.That(_runtime, Is.Not.Null);
        
        _runtime!.InstallPackage("six==1.16.0");
        
        var info = _runtime.GetPackageInfo("six");
        
        Assert.That(info, Is.Not.Null);
        Assert.That(info!.Name, Is.EqualTo("six"));
    }

    [Test]
    [Category("Integration")]
    public void UninstallPackage_Synchronous_UninstallsPackage()
    {
        Assume.That(_runtime, Is.Not.Null);
        
        _runtime!.InstallPackage("six==1.16.0");
        Assert.That(_runtime.IsPackageInstalled("six"), Is.True);
        
        var result = _runtime.UninstallPackage("six");
        
        Assert.That(result.ExitCode, Is.EqualTo(0));
        Assert.That(_runtime.IsPackageInstalled("six"), Is.False);
    }

    [Test]
    [Category("Integration")]
    public void GetPipVersion_Synchronous_ReturnsVersion()
    {
        Assume.That(_runtime, Is.Not.Null);
        
        var version = _runtime!.GetPipVersion();
        
        Assert.That(version, Is.Not.Null.And.Not.Empty);
    }

    [Test]
    [Category("Integration")]
    public void GetPythonVersionInfo_Synchronous_ReturnsVersionInfo()
    {
        Assume.That(_runtime, Is.Not.Null);
        
        var versionInfo = _runtime!.GetPythonVersionInfo();
        
        Assert.That(versionInfo, Is.Not.Null.And.Not.Empty);
    }

    [Test]
    [Category("Integration")]
    public void ExportRequirementsFreezeToString_Synchronous_ReturnsString()
    {
        Assume.That(_runtime, Is.Not.Null);
        
        _runtime!.InstallPackage("six==1.16.0");
        
        var requirements = _runtime.ExportRequirementsFreezeToString();
        
        Assert.That(requirements, Is.Not.Null.And.Not.Empty);
        Assert.That(requirements, Does.Contain("six=="));
    }
}

