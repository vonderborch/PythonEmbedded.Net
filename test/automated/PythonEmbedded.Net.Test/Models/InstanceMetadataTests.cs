using NUnit.Framework;
using PythonEmbedded.Net.Models;
using PythonEmbedded.Net.Test.TestUtilities;

namespace PythonEmbedded.Net.Test.Models;

/// <summary>
/// Tests for InstanceMetadata.
/// </summary>
[TestFixture]
public class InstanceMetadataTests
{
    private string _testDirectory = null!;

    [SetUp]
    public void SetUp()
    {
        _testDirectory = TestDirectoryHelper.CreateTestDirectory("InstanceMetadata");
    }

    [TearDown]
    public void TearDown()
    {
        TestDirectoryHelper.DeleteTestDirectory(_testDirectory);
    }

    [Test]
    public void Save_WithValidDirectory_SavesMetadata()
    {
        // Arrange
        var metadata = new InstanceMetadata
        {
            PythonVersion = "3.12.0",
            BuildDate = "20240115",
            WasLatestBuild = false,
            InstallationDate = DateTime.Now
        };

        // Act
        metadata.Save(_testDirectory);

        // Assert
        Assert.That(InstanceMetadata.Exists(_testDirectory), Is.True);
    }

    [Test]
    public void Load_WithExistingMetadata_LoadsMetadata()
    {
        // Arrange
        var originalMetadata = new InstanceMetadata
        {
            PythonVersion = "3.12.0",
            BuildDate = "20240115",
            WasLatestBuild = true,
            InstallationDate = DateTime.Now
        };
        originalMetadata.Save(_testDirectory);

        // Act
        var loadedMetadata = InstanceMetadata.Load(_testDirectory);

        // Assert
        Assert.That(loadedMetadata, Is.Not.Null);
        Assert.That(loadedMetadata!.PythonVersion, Is.EqualTo("3.12.0"));
        Assert.That(loadedMetadata.BuildDate, Is.EqualTo("20240115"));
        Assert.That(loadedMetadata.WasLatestBuild, Is.True);
        Assert.That(loadedMetadata.Directory, Is.EqualTo(_testDirectory));
    }

    [Test]
    public void Load_WithNonExistentMetadata_ReturnsNull()
    {
        // Act
        var loadedMetadata = InstanceMetadata.Load(_testDirectory);

        // Assert
        Assert.That(loadedMetadata, Is.Null);
    }

    [Test]
    public void Exists_WithExistingMetadata_ReturnsTrue()
    {
        // Arrange
        var metadata = new InstanceMetadata
        {
            PythonVersion = "3.12.0",
            BuildDate = "20240115"
        };
        metadata.Save(_testDirectory);

        // Act
        var exists = InstanceMetadata.Exists(_testDirectory);

        // Assert
        Assert.That(exists, Is.True);
    }

    [Test]
    public void Exists_WithNonExistentMetadata_ReturnsFalse()
    {
        // Act
        var exists = InstanceMetadata.Exists(_testDirectory);

        // Assert
        Assert.That(exists, Is.False);
    }
}
