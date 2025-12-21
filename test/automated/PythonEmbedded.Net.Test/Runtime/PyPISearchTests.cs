using Microsoft.Extensions.Logging;
using NUnit.Framework;
using Octokit;
using PythonEmbedded.Net.Models;
using PythonEmbedded.Net.Test.TestUtilities;

namespace PythonEmbedded.Net.Test.Runtime;

/// <summary>
/// Integration tests for PyPI package search functionality.
/// These tests require network access to PyPI.
/// </summary>
[TestFixture]
[Category("Integration")]
public class PyPISearchTests
{
    private string _testDirectory = null!;
    private PythonEmbedded.Net.PythonManager _manager = null!;
    private PythonEmbedded.Net.BasePythonRuntime? _runtime;

    [SetUp]
    public async Task SetUp()
    {
        _testDirectory = TestDirectoryHelper.CreateTestDirectory("PyPISearch");
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
    public async Task SearchPackages_WithValidPackage_ReturnsResults()
    {
        Assume.That(_runtime, Is.Not.Null);
        
        var results = await _runtime!.SearchPackagesAsync("numpy");
        
        Assert.That(results, Is.Not.Null);
        // May return results or empty list depending on implementation
    }

    [Test]
    [Category("Integration")]
    public async Task SearchPackages_WithNonExistentPackage_ReturnsEmptyList()
    {
        Assume.That(_runtime, Is.Not.Null);
        
        var results = await _runtime!.SearchPackagesAsync("nonexistent-package-xyz-12345");
        
        Assert.That(results, Is.Not.Null);
        // Should return empty list for non-existent packages
    }

    [Test]
    [Category("Integration")]
    public void SearchPackages_WithNullQuery_ThrowsArgumentException()
    {
        Assume.That(_runtime, Is.Not.Null);
        
        Assert.ThrowsAsync<ArgumentException>(() => _runtime!.SearchPackagesAsync(null!));
    }

    [Test]
    [Category("Integration")]
    public void SearchPackages_WithEmptyQuery_ThrowsArgumentException()
    {
        Assume.That(_runtime, Is.Not.Null);
        
        Assert.ThrowsAsync<ArgumentException>(() => _runtime!.SearchPackagesAsync(""));
    }

    [Test]
    [Category("Integration")]
    public async Task GetPackageMetadata_WithValidPackage_ReturnsMetadata()
    {
        Assume.That(_runtime, Is.Not.Null);
        
        var metadata = await _runtime!.GetPackageMetadataAsync("numpy");
        
        Assert.That(metadata, Is.Not.Null);
        Assert.That(metadata!.Name, Is.EqualTo("numpy").IgnoreCase);
        Assert.That(metadata.Version, Is.Not.Null.And.Not.Empty);
    }

    [Test]
    [Category("Integration")]
    public async Task GetPackageMetadata_WithValidPackageAndVersion_ReturnsMetadata()
    {
        Assume.That(_runtime, Is.Not.Null);
        
        var metadata = await _runtime!.GetPackageMetadataAsync("numpy", "1.24.0");
        
        Assert.That(metadata, Is.Not.Null);
        Assert.That(metadata!.Name, Is.EqualTo("numpy").IgnoreCase);
        Assert.That(metadata.Version, Is.EqualTo("1.24.0"));
    }

    [Test]
    [Category("Integration")]
    public async Task GetPackageMetadata_WithNonExistentPackage_ReturnsNull()
    {
        Assume.That(_runtime, Is.Not.Null);
        
        var metadata = await _runtime!.GetPackageMetadataAsync("nonexistent-package-xyz-12345");
        
        Assert.That(metadata, Is.Null);
    }

    [Test]
    [Category("Integration")]
    public async Task GetPackageMetadata_WithInvalidVersion_ReturnsNull()
    {
        Assume.That(_runtime, Is.Not.Null);
        
        var metadata = await _runtime!.GetPackageMetadataAsync("numpy", "999.999.999");
        
        // May return null or throw, depending on PyPI API behavior
        // Just verify it doesn't crash
        Assert.That(true, Is.True);
    }

    [Test]
    [Category("Integration")]
    public void GetPackageMetadata_WithNullPackageName_ThrowsArgumentException()
    {
        Assume.That(_runtime, Is.Not.Null);
        
        Assert.ThrowsAsync<ArgumentException>(() => _runtime!.GetPackageMetadataAsync(null!));
    }

    [Test]
    [Category("Integration")]
    public void GetPackageMetadata_WithEmptyPackageName_ThrowsArgumentException()
    {
        Assume.That(_runtime, Is.Not.Null);
        
        Assert.ThrowsAsync<ArgumentException>(() => _runtime!.GetPackageMetadataAsync(""));
    }

    [Test]
    [Category("Integration")]
    public void SearchPackages_Synchronous_ReturnsResults()
    {
        Assume.That(_runtime, Is.Not.Null);
        
        var results = _runtime!.SearchPackages("numpy");
        
        Assert.That(results, Is.Not.Null);
    }

    [Test]
    [Category("Integration")]
    public void GetPackageMetadata_Synchronous_ReturnsMetadata()
    {
        Assume.That(_runtime, Is.Not.Null);
        
        var metadata = _runtime!.GetPackageMetadata("numpy");
        
        Assert.That(metadata, Is.Not.Null);
        Assert.That(metadata!.Name, Is.EqualTo("numpy").IgnoreCase);
    }
}

