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

    [Test]
    [Category("Integration")]
    public async Task GetOrCreateVirtualEnvironment_WithExternalPath_CreatesAtExternalLocation()
    {
        Assume.That(_runtime, Is.Not.Null);
        
        var externalPath = Path.Combine(_testDirectory, "external_venvs", "my_external_venv");
        
        // Act
        var venv = await _runtime!.GetOrCreateVirtualEnvironmentAsync("external_venv", externalPath: externalPath);
        
        // Assert
        Assert.That(Directory.Exists(externalPath), Is.True);
        Assert.That(_runtime.VirtualEnvironmentExists("external_venv"), Is.True);
        
        var info = _runtime.GetVirtualEnvironmentInfo("external_venv");
        Assert.That(info["IsExternal"], Is.True);
        Assert.That(info["Path"], Is.EqualTo(externalPath));
    }

    [Test]
    [Category("Integration")]
    public async Task GetOrCreateVirtualEnvironment_WithExternalPath_TracksInMetadata()
    {
        Assume.That(_runtime, Is.Not.Null);
        
        var externalPath = Path.Combine(_testDirectory, "external_venvs", "tracked_venv");
        
        // Create external venv
        await _runtime!.GetOrCreateVirtualEnvironmentAsync("tracked_venv", externalPath: externalPath);
        
        // Act - Get metadata
        var metadata = _runtime.GetVirtualEnvironmentMetadata("tracked_venv");
        
        // Assert
        Assert.That(metadata, Is.Not.Null);
        Assert.That(metadata!.Name, Is.EqualTo("tracked_venv"));
        Assert.That(metadata.IsExternal, Is.True);
        Assert.That(metadata.ExternalPath, Is.EqualTo(Path.GetFullPath(externalPath)));
    }

    [Test]
    [Category("Integration")]
    public async Task ListVirtualEnvironments_IncludesExternalVenvs()
    {
        Assume.That(_runtime, Is.Not.Null);
        
        var externalPath = Path.Combine(_testDirectory, "external_venvs", "listed_venv");
        
        // Create both standard and external venvs
        await _runtime!.GetOrCreateVirtualEnvironmentAsync("standard_venv");
        await _runtime.GetOrCreateVirtualEnvironmentAsync("external_venv", externalPath: externalPath);
        
        // Act
        var venvs = _runtime.ListVirtualEnvironments();
        
        // Assert
        Assert.That(venvs, Does.Contain("standard_venv"));
        Assert.That(venvs, Does.Contain("external_venv"));
    }

    [Test]
    [Category("Integration")]
    public async Task ResolveVirtualEnvironmentPath_WithExternalVenv_ReturnsExternalPath()
    {
        Assume.That(_runtime, Is.Not.Null);
        
        var externalPath = Path.Combine(_testDirectory, "external_venvs", "resolved_venv");
        
        // Create external venv
        await _runtime!.GetOrCreateVirtualEnvironmentAsync("resolved_venv", externalPath: externalPath);
        
        // Act
        var resolvedPath = _runtime.ResolveVirtualEnvironmentPath("resolved_venv");
        
        // Assert
        Assert.That(resolvedPath, Is.EqualTo(Path.GetFullPath(externalPath)));
    }

    [Test]
    [Category("Integration")]
    public async Task DeleteVirtualEnvironment_WithExternalVenv_DeletesBothMetadataAndFiles()
    {
        Assume.That(_runtime, Is.Not.Null);
        
        var externalPath = Path.Combine(_testDirectory, "external_venvs", "deleted_venv");
        
        // Create external venv
        await _runtime!.GetOrCreateVirtualEnvironmentAsync("deleted_venv", externalPath: externalPath);
        Assert.That(Directory.Exists(externalPath), Is.True);
        
        // Act
        var result = await _runtime.DeleteVirtualEnvironmentAsync("deleted_venv");
        
        // Assert
        Assert.That(result, Is.True);
        Assert.That(Directory.Exists(externalPath), Is.False);
        Assert.That(_runtime.VirtualEnvironmentExists("deleted_venv"), Is.False);
    }

    [Test]
    [Category("Integration")]
    public async Task DeleteVirtualEnvironment_WithExternalVenv_KeepsFilesWhenRequested()
    {
        Assume.That(_runtime, Is.Not.Null);
        
        var externalPath = Path.Combine(_testDirectory, "external_venvs", "kept_venv");
        
        // Create external venv
        await _runtime!.GetOrCreateVirtualEnvironmentAsync("kept_venv", externalPath: externalPath);
        Assert.That(Directory.Exists(externalPath), Is.True);
        
        // Act - Delete but keep external files
        var result = await _runtime.DeleteVirtualEnvironmentAsync("kept_venv", deleteExternalFiles: false);
        
        // Assert
        Assert.That(result, Is.True);
        Assert.That(Directory.Exists(externalPath), Is.True); // Files kept
        Assert.That(_runtime.VirtualEnvironmentExists("kept_venv"), Is.False); // Metadata removed
    }

    [Test]
    [Category("Integration")]
    public async Task GetOrCreateVirtualEnvironment_WithDuplicateName_ThrowsException()
    {
        Assume.That(_runtime, Is.Not.Null);
        
        // Create first venv
        await _runtime!.GetOrCreateVirtualEnvironmentAsync("duplicate_test");
        
        // Act & Assert - Try to create another with same name but different path
        var externalPath = Path.Combine(_testDirectory, "external_venvs", "duplicate_external");
        Assert.ThrowsAsync<InvalidOperationException>(async () =>
        {
            await _runtime.GetOrCreateVirtualEnvironmentAsync("duplicate_test", externalPath: externalPath);
        });
    }

    [Test]
    [Category("Integration")]
    public async Task UvIsAvailable_AfterInstanceCreation_ReturnsTrue()
    {
        Assume.That(_runtime, Is.Not.Null);
        
        // uv should be auto-installed when creating the instance
        Assert.That(_runtime!.IsUvAvailable, Is.True);
        Assert.That(_runtime.UvPath, Is.Not.Null.And.Not.Empty);
    }
}

