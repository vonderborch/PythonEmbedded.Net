using NUnit.Framework;
using PythonEmbedded.Net.Models;
using PythonEmbedded.Net.Test.TestUtilities;

namespace PythonEmbedded.Net.Test.Models;

/// <summary>
/// Tests for ManagerMetadata class, especially DateTime-based instance finding.
/// </summary>
[TestFixture]
public class ManagerMetadataTests
{
    private string _testDirectory = null!;

    [SetUp]
    public void SetUp()
    {
        _testDirectory = TestDirectoryHelper.CreateTestDirectory("ManagerMetadata");
    }

    [TearDown]
    public void TearDown()
    {
        TestDirectoryHelper.DeleteTestDirectory(_testDirectory);
    }

    [Test]
    public void FindInstance_WithExactBuildDate_ReturnsInstance()
    {
        // Arrange
        var buildDate = new DateTime(2024, 1, 15);
        var metadata1 = MockPythonInstanceHelper.CreateMockPythonInstance(_testDirectory, "3.12.0", buildDate);
        var metadata2 = MockPythonInstanceHelper.CreateMockPythonInstance(_testDirectory, "3.12.0", new DateTime(2024, 2, 10));
        
        var managerMetadata = new ManagerMetadata(_testDirectory);

        // Act
        var found = managerMetadata.FindInstance("3.12.0", buildDate);

        // Assert
        Assert.That(found, Is.Not.Null);
        Assert.That(found!.PythonVersion, Is.EqualTo("3.12.0"));
        Assert.That(found.BuildDate.Date, Is.EqualTo(buildDate.Date));
    }

    [Test]
    public void FindInstance_WithNullBuildDate_ReturnsLatestBuild()
    {
        // Arrange
        var metadata1 = MockPythonInstanceHelper.CreateMockPythonInstance(_testDirectory, "3.12.0", new DateTime(2024, 1, 15));
        var metadata2 = MockPythonInstanceHelper.CreateMockPythonInstance(_testDirectory, "3.12.0", new DateTime(2024, 2, 10));
        
        // Mark the second one as latest
        metadata2.WasLatestBuild = true;
        metadata2.Save(metadata2.Directory);
        
        var managerMetadata = new ManagerMetadata(_testDirectory);

        // Act
        var found = managerMetadata.FindInstance("3.12.0", null);

        // Assert
        Assert.That(found, Is.Not.Null);
        Assert.That(found!.WasLatestBuild, Is.True);
    }

    [Test]
    public void FindInstance_WithDifferentBuildDate_ReturnsNull()
    {
        // Arrange
        var buildDate = new DateTime(2024, 1, 15);
        MockPythonInstanceHelper.CreateMockPythonInstance(_testDirectory, "3.12.0", buildDate);
        
        var managerMetadata = new ManagerMetadata(_testDirectory);

        // Act
        var found = managerMetadata.FindInstance("3.12.0", new DateTime(2024, 3, 1));

        // Assert
        Assert.That(found, Is.Null);
    }

    [Test]
    public void RemoveInstance_WithBuildDate_RemovesCorrectInstance()
    {
        // Arrange
        var buildDate1 = new DateTime(2024, 1, 15);
        var buildDate2 = new DateTime(2024, 2, 10);
        var metadata1 = MockPythonInstanceHelper.CreateMockPythonInstance(_testDirectory, "3.12.0", buildDate1);
        var metadata2 = MockPythonInstanceHelper.CreateMockPythonInstance(_testDirectory, "3.12.0", buildDate2);
        
        var managerMetadata = new ManagerMetadata(_testDirectory);

        // Act
        var removed = managerMetadata.RemoveInstance("3.12.0", buildDate1);

        // Assert
        Assert.That(removed, Is.True);
        Assert.That(managerMetadata.FindInstance("3.12.0", buildDate1), Is.Null);
        Assert.That(managerMetadata.FindInstance("3.12.0", buildDate2), Is.Not.Null);
    }
}

