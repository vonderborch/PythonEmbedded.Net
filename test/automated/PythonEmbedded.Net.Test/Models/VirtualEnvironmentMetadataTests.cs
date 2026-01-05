using NUnit.Framework;
using PythonEmbedded.Net.Models;

namespace PythonEmbedded.Net.Test.Models;

/// <summary>
/// Tests for VirtualEnvironmentMetadata.
/// </summary>
[TestFixture]
public class VirtualEnvironmentMetadataTests
{
    [Test]
    public void Constructor_WithDefaults_SetsDefaultValues()
    {
        // Act
        var metadata = new VirtualEnvironmentMetadata();

        // Assert
        Assert.That(metadata.Name, Is.EqualTo(string.Empty));
        Assert.That(metadata.ExternalPath, Is.Null);
        Assert.That(metadata.IsExternal, Is.False);
        Assert.That(metadata.CreatedDate, Is.Not.EqualTo(DateTime.MinValue));
    }

    [Test]
    public void IsExternal_WithNullExternalPath_ReturnsFalse()
    {
        // Arrange
        var metadata = new VirtualEnvironmentMetadata
        {
            Name = "test_venv",
            ExternalPath = null
        };

        // Assert
        Assert.That(metadata.IsExternal, Is.False);
    }

    [Test]
    public void IsExternal_WithEmptyExternalPath_ReturnsFalse()
    {
        // Arrange
        var metadata = new VirtualEnvironmentMetadata
        {
            Name = "test_venv",
            ExternalPath = ""
        };

        // Assert
        Assert.That(metadata.IsExternal, Is.False);
    }

    [Test]
    public void IsExternal_WithExternalPath_ReturnsTrue()
    {
        // Arrange
        var metadata = new VirtualEnvironmentMetadata
        {
            Name = "test_venv",
            ExternalPath = "/custom/path/to/venv"
        };

        // Assert
        Assert.That(metadata.IsExternal, Is.True);
    }

    [Test]
    public void GetResolvedPath_WithNoExternalPath_ReturnsDefaultPath()
    {
        // Arrange
        var metadata = new VirtualEnvironmentMetadata
        {
            Name = "test_venv",
            ExternalPath = null
        };
        var defaultPath = "/default/venvs/test_venv";

        // Act
        var resolvedPath = metadata.GetResolvedPath(defaultPath);

        // Assert
        Assert.That(resolvedPath, Is.EqualTo(defaultPath));
    }

    [Test]
    public void GetResolvedPath_WithExternalPath_ReturnsExternalPath()
    {
        // Arrange
        var externalPath = "/custom/path/to/venv";
        var metadata = new VirtualEnvironmentMetadata
        {
            Name = "test_venv",
            ExternalPath = externalPath
        };
        var defaultPath = "/default/venvs/test_venv";

        // Act
        var resolvedPath = metadata.GetResolvedPath(defaultPath);

        // Assert
        Assert.That(resolvedPath, Is.EqualTo(externalPath));
    }

    [Test]
    public void Properties_WhenSet_ReturnCorrectValues()
    {
        // Arrange
        var createdDate = new DateTime(2024, 6, 15, 10, 30, 0);
        var metadata = new VirtualEnvironmentMetadata
        {
            Name = "my_venv",
            ExternalPath = "/external/path",
            CreatedDate = createdDate
        };

        // Assert
        Assert.That(metadata.Name, Is.EqualTo("my_venv"));
        Assert.That(metadata.ExternalPath, Is.EqualTo("/external/path"));
        Assert.That(metadata.CreatedDate, Is.EqualTo(createdDate));
        Assert.That(metadata.IsExternal, Is.True);
    }
}

