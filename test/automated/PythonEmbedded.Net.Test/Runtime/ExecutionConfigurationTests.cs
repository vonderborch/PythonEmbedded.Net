using Microsoft.Extensions.Logging;
using NUnit.Framework;
using Octokit;
using PythonEmbedded.Net.Models;
using PythonEmbedded.Net.Test.TestUtilities;

namespace PythonEmbedded.Net.Test.Runtime;

/// <summary>
/// Integration tests for execution configuration (environment variables, working directory).
/// </summary>
[TestFixture]
[Category("Integration")]
public class ExecutionConfigurationTests
{
    private string _testDirectory = null!;
    private PythonEmbedded.Net.PythonManager _manager = null!;
    private PythonEmbedded.Net.BasePythonRuntime? _runtime;

    [SetUp]
    public async Task SetUp()
    {
        _testDirectory = TestDirectoryHelper.CreateTestDirectory("ExecutionConfiguration");
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
    public async Task ExecuteCommand_WithEnvironmentVariables_UsesEnvironmentVariables()
    {
        Assume.That(_runtime, Is.Not.Null);
        
        var envVars = new Dictionary<string, string>
        {
            ["TEST_VAR"] = "test_value"
        };
        
        var result = await _runtime!.ExecuteCommandAsync(
            "import os; print(os.environ.get('TEST_VAR', 'NOT_FOUND'))",
            environmentVariables: envVars);
        
        Assert.That(result.ExitCode, Is.EqualTo(0));
        Assert.That(result.StandardOutput.Trim(), Is.EqualTo("test_value"));
    }

    [Test]
    [Category("Integration")]
    public async Task ExecuteScript_WithEnvironmentVariables_UsesEnvironmentVariables()
    {
        Assume.That(_runtime, Is.Not.Null);
        
        var scriptPath = Path.Combine(_testDirectory, "test_env.py");
        await File.WriteAllTextAsync(scriptPath, "import os; print(os.environ.get('TEST_VAR', 'NOT_FOUND'))");
        
        var envVars = new Dictionary<string, string>
        {
            ["TEST_VAR"] = "script_test_value"
        };
        
        var result = await _runtime!.ExecuteScriptAsync(scriptPath, environmentVariables: envVars);
        
        Assert.That(result.ExitCode, Is.EqualTo(0));
        Assert.That(result.StandardOutput.Trim(), Is.EqualTo("script_test_value"));
    }

    [Test]
    [Category("Integration")]
    public async Task ExecuteScript_WithWorkingDirectory_UsesWorkingDirectory()
    {
        Assume.That(_runtime, Is.Not.Null);
        
        var workingDir = Path.Combine(_testDirectory, "workdir");
        Directory.CreateDirectory(workingDir);
        
        var scriptPath = Path.Combine(_testDirectory, "test_cwd.py");
        await File.WriteAllTextAsync(scriptPath, "import os; print(os.getcwd())");
        
        var result = await _runtime!.ExecuteScriptAsync(scriptPath, workingDirectory: workingDir);
        
        Assert.That(result.ExitCode, Is.EqualTo(0));
        // The output should contain the working directory path
        Assert.That(result.StandardOutput.Trim(), Does.Contain(Path.GetFileName(workingDir)));
    }

    [Test]
    [Category("Integration")]
    public async Task ExecuteCommand_WithWorkingDirectory_UsesWorkingDirectory()
    {
        Assume.That(_runtime, Is.Not.Null);
        
        var workingDir = Path.Combine(_testDirectory, "workdir");
        Directory.CreateDirectory(workingDir);
        
        var result = await _runtime!.ExecuteCommandAsync(
            "import os; print(os.getcwd())",
            workingDirectory: workingDir);
        
        Assert.That(result.ExitCode, Is.EqualTo(0));
        Assert.That(result.StandardOutput.Trim(), Does.Contain(Path.GetFileName(workingDir)));
    }
}

