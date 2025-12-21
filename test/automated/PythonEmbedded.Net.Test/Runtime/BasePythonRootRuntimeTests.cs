using Microsoft.Extensions.Logging;
using NUnit.Framework;
using PythonEmbedded.Net.Exceptions;
using PythonEmbedded.Net.Models;
using PythonEmbedded.Net.Test.TestUtilities;

namespace PythonEmbedded.Net.Test.Runtime;

/// <summary>
/// Tests for BasePythonRootRuntime virtual environment management functionality.
/// </summary>
[TestFixture]
public class BasePythonRootRuntimeTests
{
    private string _testDirectory = null!;
    private InstanceMetadata _instanceMetadata = null!;
    private PythonRootRuntime _runtime = null!;

    [SetUp]
    public void SetUp()
    {
        _testDirectory = TestDirectoryHelper.CreateTestDirectory("BasePythonRootRuntime");
        _instanceMetadata = MockPythonInstanceHelper.CreateMockPythonInstance(_testDirectory, "3.12.0", "20240115");
        _runtime = new PythonRootRuntime(_instanceMetadata);
    }

    [TearDown]
    public void TearDown()
    {
        TestDirectoryHelper.DeleteTestDirectory(_testDirectory);
    }

    [Test]
    public void ListVirtualEnvironments_WhenNoneExist_ReturnsEmptyList()
    {
        // Act
        var venvs = _runtime.ListVirtualEnvironments();

        // Assert
        Assert.That(venvs, Is.Not.Null);
        Assert.That(venvs.Count, Is.EqualTo(0));
    }

    [Test]
    public void ListVirtualEnvironments_WhenDirectoryDoesNotExist_ReturnsEmptyList()
    {
        // Arrange - Delete the venvs directory if it exists
        string venvsDir = Path.Combine(_instanceMetadata.Directory, "venvs");
        if (Directory.Exists(venvsDir))
        {
            Directory.Delete(venvsDir, true);
        }

        // Act
        var venvs = _runtime.ListVirtualEnvironments();

        // Assert
        Assert.That(venvs, Is.Not.Null);
        Assert.That(venvs.Count, Is.EqualTo(0));
    }

    [Test]
    public async Task DeleteVirtualEnvironment_WhenDoesNotExist_ReturnsFalse()
    {
        // Act
        var result = await _runtime.DeleteVirtualEnvironmentAsync("nonexistent");

        // Assert
        Assert.That(result, Is.False);
    }

    [Test]
    public void DeleteVirtualEnvironment_WithNullName_ThrowsArgumentException()
    {
        // Act & Assert - DeleteVirtualEnvironment is async
        Assert.ThrowsAsync<ArgumentException>(async () => await _runtime.DeleteVirtualEnvironmentAsync(null!));
    }

    [Test]
    public void DeleteVirtualEnvironment_WithEmptyName_ThrowsArgumentException()
    {
        // Act & Assert - DeleteVirtualEnvironment is async
        Assert.ThrowsAsync<ArgumentException>(async () => await _runtime.DeleteVirtualEnvironmentAsync(string.Empty));
    }

    [Test]
    public void GetOrCreateVirtualEnvironment_WithNullName_ThrowsArgumentException()
    {
        // Act & Assert
        Assert.ThrowsAsync<ArgumentException>(async () => await _runtime.GetOrCreateVirtualEnvironmentAsync(null!));
    }

    [Test]
    public void GetOrCreateVirtualEnvironment_WithEmptyName_ThrowsArgumentException()
    {
        // Act & Assert
        Assert.ThrowsAsync<ArgumentException>(async () => await _runtime.GetOrCreateVirtualEnvironmentAsync(string.Empty));
    }

    // Note: Tests for actual virtual environment creation would require a real Python installation
    // These are integration tests that would need to be run in an environment with Python installed
}
