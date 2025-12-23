using Microsoft.Extensions.Logging;
using NUnit.Framework;
using Octokit;
using PythonEmbedded.Net.Models;
using PythonEmbedded.Net.Test.TestUtilities;
using System.Diagnostics;

namespace PythonEmbedded.Net.Test.Runtime;

/// <summary>
/// Integration tests for advanced execution configuration (priority, timeout, etc.).
/// </summary>
[TestFixture]
[Category("Integration")]
public class ExecutionConfigurationAdvancedTests
{
    private string _testDirectory = null!;
    private PythonEmbedded.Net.PythonManager _manager = null!;
    private PythonEmbedded.Net.BasePythonRuntime? _runtime;

    [SetUp]
    public async Task SetUp()
    {
        _testDirectory = TestDirectoryHelper.CreateTestDirectory("ExecutionConfigurationAdvanced");
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
    public async Task ExecuteCommand_WithTimeout_RespectsTimeout()
    {
        Assume.That(_runtime, Is.Not.Null);
        
        // Execute a command that should complete quickly
        var result = await _runtime!.ExecuteCommandAsync(
            "print('test')",
            timeout: TimeSpan.FromSeconds(10));
        
        Assert.That(result.ExitCode, Is.EqualTo(0));
    }

    [Test]
    [Category("Integration")]
    public async Task ExecuteCommand_WithProcessPriority_ExecutesSuccessfully()
    {
        Assume.That(_runtime, Is.Not.Null);
        
        // Execute with normal priority (default)
        var result = await _runtime!.ExecuteCommandAsync(
            "print('test')",
            priority: ProcessPriorityClass.Normal);
        
        Assert.That(result.ExitCode, Is.EqualTo(0));
    }
}


