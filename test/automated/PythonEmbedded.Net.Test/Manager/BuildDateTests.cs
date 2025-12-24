using Microsoft.Extensions.Logging;
using NUnit.Framework;
using Octokit;
using PythonEmbedded.Net.Models;
using PythonEmbedded.Net.Test.TestUtilities;

namespace PythonEmbedded.Net.Test.Manager;

/// <summary>
/// Tests for DateTime-based buildDate functionality.
/// </summary>
[TestFixture]
[Category("Integration")]
public class BuildDateTests
{
    private string _testDirectory = null!;
    private PythonEmbedded.Net.PythonManager _manager = null!;

    [SetUp]
    public void SetUp()
    {
        _testDirectory = TestDirectoryHelper.CreateTestDirectory("BuildDate");
        var githubClient = new GitHubClient(new ProductHeaderValue("PythonEmbedded.Net-Test"));
        _manager = new PythonEmbedded.Net.PythonManager(_testDirectory, githubClient);
    }

    [TearDown]
    public void TearDown()
    {
        TestDirectoryHelper.DeleteTestDirectory(_testDirectory);
    }

    // Tests that require instance management have been moved to:
    // test/automated/PythonEmbedded.Net.IntegrationTest/Manager/BuildDateIntegrationTests.cs
    // These tests require actual Python instances to be created and managed.

    // Integration tests that require GitHub API access have been moved to:
    // test/automated/PythonEmbedded.Net.IntegrationTest/Manager/BuildDateIntegrationTests.cs
}

