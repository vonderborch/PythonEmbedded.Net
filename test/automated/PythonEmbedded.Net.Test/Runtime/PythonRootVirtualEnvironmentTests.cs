using Microsoft.Extensions.Logging;
using NUnit.Framework;
using PythonEmbedded.Net.Exceptions;
using PythonEmbedded.Net.Test.TestUtilities;

namespace PythonEmbedded.Net.Test.Runtime;

/// <summary>
/// Tests for PythonRootVirtualEnvironment.
/// </summary>
[TestFixture]
public class PythonRootVirtualEnvironmentTests
{
    private string _testDirectory = null!;
    private string _venvPath = null!;

    [SetUp]
    public void SetUp()
    {
        _testDirectory = TestDirectoryHelper.CreateTestDirectory("PythonRootVirtualEnvironment");
        _venvPath = MockPythonInstanceHelper.CreateMockVirtualEnvironment(_testDirectory, "test-venv");
    }

    [TearDown]
    public void TearDown()
    {
        TestDirectoryHelper.DeleteTestDirectory(_testDirectory);
    }

    [Test]
    public void Constructor_WithValidPath_CreatesInstance()
    {
        // Act
        var venv = new PythonRootVirtualEnvironment(_venvPath);

        // Assert
        Assert.That(venv, Is.Not.Null);
    }

    [Test]
    public void Constructor_WithLogger_CreatesInstance()
    {
        // Arrange
        var loggerFactory = LoggerFactory.Create(builder => builder.AddConsole());
        var logger = loggerFactory.CreateLogger<PythonRootVirtualEnvironment>();

        // Act
        var venv = new PythonRootVirtualEnvironment(_venvPath, logger);

        // Assert
        Assert.That(venv, Is.Not.Null);
        loggerFactory.Dispose();
    }

    [Test]
    public void Constructor_WithNullPath_ThrowsArgumentException()
    {
        // Act & Assert
        Assert.Throws<ArgumentException>(() => new PythonRootVirtualEnvironment(null!));
    }

    [Test]
    public void Constructor_WithEmptyPath_ThrowsArgumentException()
    {
        // Act & Assert
        Assert.Throws<ArgumentException>(() => new PythonRootVirtualEnvironment(string.Empty));
    }

    [Test]
    public void Constructor_WithWhitespacePath_ThrowsArgumentException()
    {
        // Act & Assert
        Assert.Throws<ArgumentException>(() => new PythonRootVirtualEnvironment("   "));
    }

    // Note: Tests for ValidateInstallation would require actual Python executables
    // These would be integration tests
}
