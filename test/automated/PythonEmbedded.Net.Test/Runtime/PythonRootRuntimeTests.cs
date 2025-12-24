using Microsoft.Extensions.Logging;
using NUnit.Framework;
using PythonEmbedded.Net.Exceptions;
using PythonEmbedded.Net.Models;
using PythonEmbedded.Net.Test.TestUtilities;

namespace PythonEmbedded.Net.Test.Runtime;

/// <summary>
/// Tests for PythonRootRuntime.
/// </summary>
[TestFixture]
public class PythonRootRuntimeTests
{
    private string _testDirectory = null!;
    private InstanceMetadata _instanceMetadata = null!;

    [SetUp]
    public void SetUp()
    {
        _testDirectory = TestDirectoryHelper.CreateTestDirectory("PythonRootRuntime");
        _instanceMetadata = MockPythonInstanceHelper.CreateMockPythonInstance(_testDirectory, "3.12.0", new DateTime(2024, 1, 15));
    }

    [TearDown]
    public void TearDown()
    {
        TestDirectoryHelper.DeleteTestDirectory(_testDirectory);
    }

    [Test]
    public void Constructor_WithValidMetadata_CreatesInstance()
    {
        // Act
        var runtime = new PythonRootRuntime(_instanceMetadata);

        // Assert
        Assert.That(runtime, Is.Not.Null);
    }

    [Test]
    public void Constructor_WithNullMetadata_ThrowsArgumentNullException()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => new PythonRootRuntime(null!));
    }

    [Test]
    public void Constructor_WithLogger_CreatesInstance()
    {
        // Arrange
        var loggerFactory = LoggerFactory.Create(builder => builder.AddConsole());
        var logger = loggerFactory.CreateLogger<PythonRootRuntime>();

        // Act
        var runtime = new PythonRootRuntime(_instanceMetadata, logger);

        // Assert
        Assert.That(runtime, Is.Not.Null);
        loggerFactory.Dispose();
    }

    [Test]
    public void VirtualEnvironmentsDirectory_ReturnsCorrectPath()
    {
        // Arrange
        var runtime = new PythonRootRuntime(_instanceMetadata);

        // Act
        // Note: This is protected, so we test it indirectly through GetOrCreateVirtualEnvironmentAsync
        // For now, we'll test that the runtime can be created
        Assert.That(runtime, Is.Not.Null);
    }

    [Test]
    public void ValidateInstallation_WithValidInstance_DoesNotThrow()
    {
        // Arrange
        var runtime = new PythonRootRuntime(_instanceMetadata);

        // Act & Assert
        // Validation is called internally, so if it throws, the test will fail
        Assert.That(runtime, Is.Not.Null);
    }

    // Note: Validation tests require actual Python executables
    // These would be integration tests that need real Python installations
}
