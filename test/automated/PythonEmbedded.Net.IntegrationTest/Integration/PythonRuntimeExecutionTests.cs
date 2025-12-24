using Octokit;
using PythonEmbedded.Net.Test.TestUtilities;

namespace PythonEmbedded.Net.IntegrationTest.Integration;

/// <summary>
/// Integration tests for Python runtime execution functionality.
/// These tests download and use real Python installations from GitHub.
/// Note: These tests require network access and may take longer to run.
/// </summary>
[TestFixture]
[Category("Integration")]
public class PythonRuntimeExecutionTests
{
    private string _testDirectory = null!;
    private PythonEmbedded.Net.PythonManager _manager = null!;
    private PythonEmbedded.Net.BasePythonRuntime? _runtime;

    [SetUp]
    public async Task SetUp()
    {
        this._testDirectory = TestDirectoryHelper.CreateTestDirectory("PythonRuntimeExecution");
        
        // Create a GitHub client (using unauthenticated client for tests)
        var githubClient = new GitHubClient(new ProductHeaderValue("PythonEmbedded.Net-Test"));
        
        // Create the manager
        this._manager = new PythonEmbedded.Net.PythonManager(this._testDirectory, githubClient);
        
        // Download and set up a real Python 3.12 instance (this may take some time)
        // Using a common stable version that should be available
        this._runtime = await this._manager.GetOrCreateInstanceAsync("3.12", cancellationToken: default);
    }

    [TearDown]
    public void TearDown()
    {
        TestDirectoryHelper.DeleteTestDirectory(this._testDirectory);
    }

    [Test]
    [Category("Integration")]
    // Note: This test may take several minutes due to Python download/extraction
    public async Task ExecuteCommand_WithSimpleCommand_ReturnsResult()
    {
        // Arrange
        Assume.That(this._runtime, Is.Not.Null, "Python runtime was not successfully set up");
        
        // Act
        var result = await this._runtime!.ExecuteCommandAsync("print('Hello, World!')");

        // Assert
        Assert.That(result.ExitCode, Is.EqualTo(0));
        Assert.That(result.StandardOutput, Does.Contain("Hello, World!"));
    }

    [Test]
    [Category("Integration")]
    // Note: This test may take several minutes due to Python download/extraction
    public async Task ExecuteScript_WithValidScript_ReturnsResult()
    {
        // Arrange
        Assume.That(this._runtime, Is.Not.Null, "Python runtime was not successfully set up");
        
        var scriptPath = Path.Combine(this._testDirectory, "test_script.py");
        await File.WriteAllTextAsync(scriptPath, "print('Hello from script')");
        
        // Act
        var result = await this._runtime!.ExecuteScriptAsync(scriptPath);
        
        // Assert
        Assert.That(result.ExitCode, Is.EqualTo(0));
        Assert.That(result.StandardOutput, Does.Contain("Hello from script"));
    }

    [Test]
    [Category("Integration")]
    // Note: This test may take 10+ minutes due to Python download/extraction and package installation
    public async Task InstallPackage_WithValidPackage_InstallsPackage()
    {
        // Arrange
        Assume.That(this._runtime, Is.Not.Null, "Python runtime was not successfully set up");
        
        // Use a small, simple package for testing
        // Act
        var result = await this._runtime!.InstallPackageAsync("six");
        
        // Assert
        Assert.That(result.ExitCode, Is.EqualTo(0));
        // Verify the package is installed by trying to import it
        var importResult = await this._runtime.ExecuteCommandAsync("import six; print(six.__version__)");
        Assert.That(importResult.ExitCode, Is.EqualTo(0));
    }

    [Test]
    [Category("Integration")]
    // Note: This test may take 10+ minutes due to Python download/extraction and package installation
    public async Task InstallRequirements_WithValidFile_InstallsPackages()
    {
        // Arrange
        Assume.That(this._runtime, Is.Not.Null, "Python runtime was not successfully set up");
        
        var requirementsPath = Path.Combine(this._testDirectory, "requirements.txt");
        await File.WriteAllTextAsync(requirementsPath, "six==1.16.0\n");
        
        // Act
        var result = await this._runtime!.InstallRequirementsAsync(requirementsPath);
        
        // Assert
        Assert.That(result.ExitCode, Is.EqualTo(0));
        // Verify the package is installed
        var importResult = await this._runtime.ExecuteCommandAsync("import six; print(six.__version__)");
        Assert.That(importResult.ExitCode, Is.EqualTo(0));
    }
}
