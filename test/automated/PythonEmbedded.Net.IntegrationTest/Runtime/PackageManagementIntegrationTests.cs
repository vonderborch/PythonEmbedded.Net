using Microsoft.Extensions.Logging;
using NUnit.Framework;
using Octokit;
using PythonEmbedded.Net.Models;
using PythonEmbedded.Net.Test.TestUtilities;

namespace PythonEmbedded.Net.IntegrationTest.Runtime;

/// <summary>
/// Integration tests for package management functionality.
/// These tests require real Python installations.
/// </summary>
[TestFixture]
[Category("Integration")]
public class PackageManagementIntegrationTests
{
    private string _testDirectory = null!;
    private PythonEmbedded.Net.PythonManager _manager = null!;
    private PythonEmbedded.Net.BasePythonRuntime? _runtime;

    [SetUp]
    public async Task SetUp()
    {
        _testDirectory = TestDirectoryHelper.CreateTestDirectory("PackageManagement");
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
    public async Task ListInstalledPackages_ReturnsPackages()
    {
        Assume.That(_runtime, Is.Not.Null);
        
        var packages = await _runtime!.ListInstalledPackagesAsync();
        
        Assert.That(packages, Is.Not.Null);
        Assert.That(packages.Count, Is.GreaterThan(0));
    }

    [Test]
    [Category("Integration")]
    public async Task GetPackageVersion_WithInstalledPackage_ReturnsVersion()
    {
        Assume.That(_runtime, Is.Not.Null);
        
        // Install a package first
        await _runtime!.InstallPackageAsync("six==1.16.0");
        
        var version = await _runtime.GetPackageVersionAsync("six");
        
        Assert.That(version, Is.Not.Null);
        Assert.That(version, Does.Contain("1.16"));
    }

    [Test]
    [Category("Integration")]
    public async Task GetPackageVersion_WithNonInstalledPackage_ReturnsNull()
    {
        Assume.That(_runtime, Is.Not.Null);
        
        var version = await _runtime!.GetPackageVersionAsync("nonexistent-package-xyz");
        
        Assert.That(version, Is.Null);
    }

    [Test]
    [Category("Integration")]
    public async Task IsPackageInstalled_WithInstalledPackage_ReturnsTrue()
    {
        Assume.That(_runtime, Is.Not.Null);
        
        await _runtime!.InstallPackageAsync("six==1.16.0");
        
        var isInstalled = await _runtime.IsPackageInstalledAsync("six");
        
        Assert.That(isInstalled, Is.True);
    }

    [Test]
    [Category("Integration")]
    public async Task IsPackageInstalled_WithNonInstalledPackage_ReturnsFalse()
    {
        Assume.That(_runtime, Is.Not.Null);
        
        var isInstalled = await _runtime!.IsPackageInstalledAsync("nonexistent-package-xyz");
        
        Assert.That(isInstalled, Is.False);
    }

    [Test]
    [Category("Integration")]
    public async Task GetPackageInfo_WithInstalledPackage_ReturnsInfo()
    {
        Assume.That(_runtime, Is.Not.Null);
        
        await _runtime!.InstallPackageAsync("six==1.16.0");
        
        var info = await _runtime.GetPackageInfoAsync("six");
        
        Assert.That(info, Is.Not.Null);
        Assert.That(info!.Name, Is.EqualTo("six"));
        Assert.That(info.Version, Is.Not.Null.And.Not.Empty);
    }

    [Test]
    [Category("Integration")]
    public async Task GetPackageInfo_WithNonInstalledPackage_ReturnsNull()
    {
        Assume.That(_runtime, Is.Not.Null);
        
        var info = await _runtime!.GetPackageInfoAsync("nonexistent-package-xyz");
        
        Assert.That(info, Is.Null);
    }

    [Test]
    [Category("Integration")]
    public async Task UninstallPackage_WithInstalledPackage_UninstallsPackage()
    {
        Assume.That(_runtime, Is.Not.Null);
        
        // Install first
        await _runtime!.InstallPackageAsync("six==1.16.0");
        Assert.That(await _runtime.IsPackageInstalledAsync("six"), Is.True);
        
        // Uninstall
        var result = await _runtime.UninstallPackageAsync("six");
        
        Assert.That(result.ExitCode, Is.EqualTo(0));
        Assert.That(await _runtime.IsPackageInstalledAsync("six"), Is.False);
    }

    [Test]
    [Category("Integration")]
    public async Task ListOutdatedPackages_ReturnsOutdatedPackages()
    {
        Assume.That(_runtime, Is.Not.Null);
        
        // Install an older version
        await _runtime!.InstallPackageAsync("six==1.16.0");
        
        var outdated = await _runtime.ListOutdatedPackagesAsync();
        
        Assert.That(outdated, Is.Not.Null);
        // May or may not have outdated packages depending on versions available
    }

    [Test]
    [Category("Integration")]
    public async Task UpgradeAllPackages_UpgradesPackages()
    {
        Assume.That(_runtime, Is.Not.Null);
        
        // Install an older version
        await _runtime!.InstallPackageAsync("six==1.16.0");
        
        var result = await _runtime.UpgradeAllPackagesAsync();
        
        Assert.That(result, Is.Not.Null);
        // May succeed even if no packages need upgrading
    }

    [Test]
    [Category("Integration")]
    public async Task DowngradePackage_DowngradesToSpecificVersion()
    {
        Assume.That(_runtime, Is.Not.Null);
        
        // Install a newer version first (if available)
        await _runtime!.InstallPackageAsync("six");
        
        // Downgrade to specific version
        var result = await _runtime.DowngradePackageAsync("six", "1.16.0");
        
        Assert.That(result.ExitCode, Is.EqualTo(0));
        var version = await _runtime.GetPackageVersionAsync("six");
        Assert.That(version, Does.Contain("1.16"));
    }

    [Test]
    [Category("Integration")]
    public async Task ExportRequirementsFreezeToString_ReturnsRequirementsString()
    {
        Assume.That(_runtime, Is.Not.Null);
        
        await _runtime!.InstallPackageAsync("six==1.16.0");
        
        var requirements = await _runtime.ExportRequirementsFreezeToStringAsync();
        
        Assert.That(requirements, Is.Not.Null.And.Not.Empty);
        Assert.That(requirements, Does.Contain("six=="));
    }

    [Test]
    [Category("Integration")]
    public async Task ExportRequirementsFreeze_ExportsToFile()
    {
        Assume.That(_runtime, Is.Not.Null);
        
        await _runtime!.InstallPackageAsync("six==1.16.0");
        
        var outputPath = Path.Combine(_testDirectory, "requirements.txt");
        var result = await _runtime.ExportRequirementsFreezeAsync(outputPath);
        
        Assert.That(result.ExitCode, Is.EqualTo(0));
        Assert.That(File.Exists(outputPath), Is.True);
        var content = await File.ReadAllTextAsync(outputPath);
        Assert.That(content, Does.Contain("six=="));
    }

    [Test]
    [Category("Integration")]
    public async Task InstallPackages_BatchInstallsMultiplePackages()
    {
        Assume.That(_runtime, Is.Not.Null);
        
        var packages = new[] { "six==1.16.0", "pyparsing==3.0.9" };
        var results = await _runtime!.InstallPackagesAsync(packages, parallel: false);
        
        Assert.That(results, Is.Not.Null);
        Assert.That(results.Count, Is.EqualTo(2));
        Assert.That(await _runtime.IsPackageInstalledAsync("six"), Is.True);
        Assert.That(await _runtime.IsPackageInstalledAsync("pyparsing"), Is.True);
    }

    [Test]
    [Category("Integration")]
    public async Task UninstallPackages_BatchUninstallsMultiplePackages()
    {
        Assume.That(_runtime, Is.Not.Null);
        
        // Install first
        await _runtime!.InstallPackageAsync("six==1.16.0");
        await _runtime.InstallPackageAsync("pyparsing==3.0.9");
        
        var packages = new[] { "six", "pyparsing" };
        var results = await _runtime.UninstallPackagesAsync(packages, parallel: false);
        
        Assert.That(results, Is.Not.Null);
        Assert.That(results.Count, Is.EqualTo(2));
        Assert.That(await _runtime.IsPackageInstalledAsync("six"), Is.False);
        Assert.That(await _runtime.IsPackageInstalledAsync("pyparsing"), Is.False);
    }

    [Test]
    [Category("Integration")]
    public async Task GetPipVersion_ReturnsVersion()
    {
        Assume.That(_runtime, Is.Not.Null);
        
        var version = await _runtime!.GetPipVersionAsync();
        
        Assert.That(version, Is.Not.Null.And.Not.Empty);
    }

    [Test]
    [Category("Integration")]
    public async Task GetPythonVersionInfo_ReturnsVersionInfo()
    {
        Assume.That(_runtime, Is.Not.Null);
        
        var versionInfo = await _runtime!.GetPythonVersionInfoAsync();
        
        Assert.That(versionInfo, Is.Not.Null.And.Not.Empty);
        Assert.That(versionInfo, Does.Contain("3.12"));
    }
}

