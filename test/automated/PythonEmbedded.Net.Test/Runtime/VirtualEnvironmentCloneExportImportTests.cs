using Microsoft.Extensions.Logging;
using NUnit.Framework;
using Octokit;
using PythonEmbedded.Net.Models;
using PythonEmbedded.Net.Test.TestUtilities;

namespace PythonEmbedded.Net.Test.Runtime;

/// <summary>
/// Integration tests for virtual environment clone, export, and import operations.
/// </summary>
[TestFixture]
[Category("Integration")]
public class VirtualEnvironmentCloneExportImportTests
{
    private string _testDirectory = null!;
    private PythonEmbedded.Net.PythonManager _manager = null!;
    private PythonEmbedded.Net.PythonRootRuntime? _runtime;

    [SetUp]
    public async Task SetUp()
    {
        _testDirectory = TestDirectoryHelper.CreateTestDirectory("VirtualEnvironmentCloneExportImport");
        var githubClient = new GitHubClient(new ProductHeaderValue("PythonEmbedded.Net-Test"));
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
    public async Task CloneVirtualEnvironment_ClonesSuccessfully()
    {
        Assume.That(_runtime, Is.Not.Null);
        
        // Create source virtual environment
        var sourceVenv = await _runtime!.GetOrCreateVirtualEnvironmentAsync("source_venv");
        
        // Install a package in the source venv
        await sourceVenv.InstallPackageAsync("six==1.16.0");
        
        // Clone it
        var clonedVenv = await _runtime.CloneVirtualEnvironmentAsync("source_venv", "cloned_venv");
        
        // Verify the clone has the package
        Assert.That(clonedVenv, Is.Not.Null);
        Assert.That(await clonedVenv.IsPackageInstalledAsync("six"), Is.True);
    }

    [Test]
    [Category("Integration")]
    public void CloneVirtualEnvironment_WithNonExistentSource_ThrowsDirectoryNotFoundException()
    {
        Assume.That(_runtime, Is.Not.Null);
        
        Assert.ThrowsAsync<DirectoryNotFoundException>(() => _runtime!.CloneVirtualEnvironmentAsync("nonexistent", "target"));
    }

    [Test]
    [Category("Integration")]
    public async Task ExportVirtualEnvironment_ExportsSuccessfully()
    {
        Assume.That(_runtime, Is.Not.Null);
        
        // Create and set up a virtual environment
        var venv = await _runtime!.GetOrCreateVirtualEnvironmentAsync("export_test");
        await venv.InstallPackageAsync("six==1.16.0");
        
        // Export it
        var outputPath = Path.Combine(_testDirectory, "venv_export.zip");
        var resultPath = await _runtime.ExportVirtualEnvironmentAsync("export_test", outputPath);
        
        // Verify the archive was created
        Assert.That(File.Exists(resultPath), Is.True);
        Assert.That(resultPath, Is.EqualTo(outputPath));
    }

    [Test]
    [Category("Integration")]
    public async Task ImportVirtualEnvironment_ImportsSuccessfully()
    {
        Assume.That(_runtime, Is.Not.Null);
        
        // Create and export a virtual environment
        var sourceVenv = await _runtime!.GetOrCreateVirtualEnvironmentAsync("source_for_import");
        await sourceVenv.InstallPackageAsync("six==1.16.0");
        
        var exportPath = Path.Combine(_testDirectory, "venv_import_test.zip");
        await _runtime.ExportVirtualEnvironmentAsync("source_for_import", exportPath);
        
        // Delete the original
        await _runtime.DeleteVirtualEnvironmentAsync("source_for_import");
        
        // Import it
        var importedVenv = await _runtime.ImportVirtualEnvironmentAsync("imported_venv", exportPath);
        
        // Verify the import
        Assert.That(importedVenv, Is.Not.Null);
        Assert.That(await importedVenv.IsPackageInstalledAsync("six"), Is.True);
    }
}

