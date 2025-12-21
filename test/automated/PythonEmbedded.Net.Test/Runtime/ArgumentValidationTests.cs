using Microsoft.Extensions.Logging;
using NUnit.Framework;
using Octokit;
using PythonEmbedded.Net.Models;
using PythonEmbedded.Net.Test.TestUtilities;

namespace PythonEmbedded.Net.Test.Runtime;

/// <summary>
/// Tests for argument validation in runtime methods.
/// </summary>
[TestFixture]
[Category("Integration")]
public class ArgumentValidationTests
{
    private string _testDirectory = null!;
    private PythonEmbedded.Net.PythonManager _manager = null!;
    private PythonEmbedded.Net.BasePythonRuntime? _runtime;

    [SetUp]
    public async Task SetUp()
    {
        _testDirectory = TestDirectoryHelper.CreateTestDirectory("ArgumentValidation");
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
    public void ExecuteCommand_WithNullCommand_ThrowsArgumentException()
    {
        Assume.That(_runtime, Is.Not.Null);
        
        Assert.ThrowsAsync<ArgumentException>(() => _runtime!.ExecuteCommandAsync(null!));
    }

    [Test]
    [Category("Integration")]
    public void ExecuteCommand_WithEmptyCommand_ThrowsArgumentException()
    {
        Assume.That(_runtime, Is.Not.Null);
        
        Assert.ThrowsAsync<ArgumentException>(() => _runtime!.ExecuteCommandAsync(""));
    }

    [Test]
    [Category("Integration")]
    public void ExecuteScript_WithNullPath_ThrowsArgumentException()
    {
        Assume.That(_runtime, Is.Not.Null);
        
        Assert.ThrowsAsync<ArgumentException>(() => _runtime!.ExecuteScriptAsync(null!));
    }

    [Test]
    [Category("Integration")]
    public void ExecuteScript_WithNonExistentFile_ThrowsFileNotFoundException()
    {
        Assume.That(_runtime, Is.Not.Null);
        
        Assert.ThrowsAsync<FileNotFoundException>(() => _runtime!.ExecuteScriptAsync("/nonexistent/script.py"));
    }

    [Test]
    [Category("Integration")]
    public void InstallPackage_WithNullPackageSpec_ThrowsArgumentException()
    {
        Assume.That(_runtime, Is.Not.Null);
        
        Assert.ThrowsAsync<ArgumentException>(() => _runtime!.InstallPackageAsync(null!));
    }

    [Test]
    [Category("Integration")]
    public void InstallPackage_WithEmptyPackageSpec_ThrowsArgumentException()
    {
        Assume.That(_runtime, Is.Not.Null);
        
        Assert.ThrowsAsync<ArgumentException>(() => _runtime!.InstallPackageAsync(""));
    }

    [Test]
    [Category("Integration")]
    public void InstallRequirements_WithNullPath_ThrowsArgumentException()
    {
        Assume.That(_runtime, Is.Not.Null);
        
        Assert.ThrowsAsync<ArgumentException>(() => _runtime!.InstallRequirementsAsync(null!));
    }

    [Test]
    [Category("Integration")]
    public void InstallRequirements_WithNonExistentFile_ThrowsFileNotFoundException()
    {
        Assume.That(_runtime, Is.Not.Null);
        
        Assert.ThrowsAsync<FileNotFoundException>(() => _runtime!.InstallRequirementsAsync("/nonexistent/requirements.txt"));
    }

    [Test]
    [Category("Integration")]
    public void GetPackageVersion_WithNullPackageName_ThrowsArgumentException()
    {
        Assume.That(_runtime, Is.Not.Null);
        
        Assert.ThrowsAsync<ArgumentException>(() => _runtime!.GetPackageVersionAsync(null!));
    }

    [Test]
    [Category("Integration")]
    public void GetPackageInfo_WithNullPackageName_ThrowsArgumentException()
    {
        Assume.That(_runtime, Is.Not.Null);
        
        Assert.ThrowsAsync<ArgumentException>(() => _runtime!.GetPackageInfoAsync(null!));
    }

    [Test]
    [Category("Integration")]
    public void UninstallPackage_WithNullPackageName_ThrowsArgumentException()
    {
        Assume.That(_runtime, Is.Not.Null);
        
        Assert.ThrowsAsync<ArgumentException>(() => _runtime!.UninstallPackageAsync(null!));
    }

    [Test]
    [Category("Integration")]
    public void DowngradePackage_WithNullPackageName_ThrowsArgumentException()
    {
        Assume.That(_runtime, Is.Not.Null);
        
        Assert.ThrowsAsync<ArgumentException>(() => _runtime!.DowngradePackageAsync(null!, "1.0.0"));
    }

    [Test]
    [Category("Integration")]
    public void DowngradePackage_WithNullVersion_ThrowsArgumentException()
    {
        Assume.That(_runtime, Is.Not.Null);
        
        Assert.ThrowsAsync<ArgumentException>(() => _runtime!.DowngradePackageAsync("package", null!));
    }

    [Test]
    [Category("Integration")]
    public void ExportRequirements_WithNullPath_ThrowsArgumentException()
    {
        Assume.That(_runtime, Is.Not.Null);
        
        Assert.ThrowsAsync<ArgumentException>(() => _runtime!.ExportRequirementsAsync(null!));
    }

    [Test]
    [Category("Integration")]
    public void InstallPackages_WithNullPackages_ThrowsArgumentNullException()
    {
        Assume.That(_runtime, Is.Not.Null);
        
        Assert.ThrowsAsync<ArgumentNullException>(() => _runtime!.InstallPackagesAsync(null!));
    }

    [Test]
    [Category("Integration")]
    public void UninstallPackages_WithNullPackages_ThrowsArgumentNullException()
    {
        Assume.That(_runtime, Is.Not.Null);
        
        Assert.ThrowsAsync<ArgumentNullException>(() => _runtime!.UninstallPackagesAsync(null!));
    }

    [Test]
    [Category("Integration")]
    public void ConfigurePipIndex_WithNullIndexUrl_ThrowsArgumentException()
    {
        Assume.That(_runtime, Is.Not.Null);
        
        Assert.ThrowsAsync<ArgumentException>(() => _runtime!.ConfigurePipIndexAsync(null!));
    }

    [Test]
    [Category("Integration")]
    public void ConfigurePipProxy_WithNullProxyUrl_ThrowsArgumentException()
    {
        Assume.That(_runtime, Is.Not.Null);
        
        Assert.ThrowsAsync<ArgumentException>(() => _runtime!.ConfigurePipProxyAsync(null!));
    }
}

