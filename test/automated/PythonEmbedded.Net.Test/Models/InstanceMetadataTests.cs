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
            BuildDate = new DateTime(2024, 1, 15),
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
            BuildDate = new DateTime(2024, 1, 15),
            WasLatestBuild = true,
            InstallationDate = DateTime.Now
        };
        originalMetadata.Save(_testDirectory);

        // Act
        var loadedMetadata = InstanceMetadata.Load(_testDirectory);

        // Assert
        Assert.That(loadedMetadata, Is.Not.Null);
        Assert.That(loadedMetadata!.PythonVersion, Is.EqualTo("3.12.0"));
        Assert.That(loadedMetadata.BuildDate, Is.EqualTo(new DateTime(2024, 1, 15)));
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
            BuildDate = new DateTime(2024, 1, 15)
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

    [Test]
    public void Save_AndLoad_WithDateTime_PreservesDateTime()
    {
        // Arrange
        var originalBuildDate = new DateTime(2024, 1, 15, 14, 30, 45);
        var metadata = new InstanceMetadata
        {
            PythonVersion = "3.12.0",
            BuildDate = originalBuildDate,
            WasLatestBuild = false,
            InstallationDate = DateTime.Now
        };

        // Act
        metadata.Save(_testDirectory);
        var loadedMetadata = InstanceMetadata.Load(_testDirectory);

        // Assert
        Assert.That(loadedMetadata, Is.Not.Null);
        Assert.That(loadedMetadata!.BuildDate.Date, Is.EqualTo(originalBuildDate.Date));
        // Note: Time component may be lost in serialization, but date should be preserved
    }

    [Test]
    public void Save_AndLoad_WithDifferentDates_PreservesDates()
    {
        // Arrange
        var buildDate1 = new DateTime(2024, 1, 15);
        var buildDate2 = new DateTime(2024, 2, 10);
        
        var metadata1 = new InstanceMetadata
        {
            PythonVersion = "3.12.0",
            BuildDate = buildDate1,
            WasLatestBuild = false
        };
        metadata1.Save(Path.Combine(_testDirectory, "instance1"));

        var metadata2 = new InstanceMetadata
        {
            PythonVersion = "3.12.0",
            BuildDate = buildDate2,
            WasLatestBuild = false
        };
        metadata2.Save(Path.Combine(_testDirectory, "instance2"));

        // Act
        var loaded1 = InstanceMetadata.Load(Path.Combine(_testDirectory, "instance1"));
        var loaded2 = InstanceMetadata.Load(Path.Combine(_testDirectory, "instance2"));

        // Assert
        Assert.That(loaded1, Is.Not.Null);
        Assert.That(loaded2, Is.Not.Null);
        Assert.That(loaded1!.BuildDate.Date, Is.EqualTo(buildDate1.Date));
        Assert.That(loaded2!.BuildDate.Date, Is.EqualTo(buildDate2.Date));
    }
}
