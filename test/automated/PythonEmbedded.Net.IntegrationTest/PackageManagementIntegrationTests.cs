using PythonEmbedded.Net;
using PythonEmbedded.Net.Internals;
using PythonEmbedded.Net.Models;
using PythonEmbedded.Net.Test;

namespace PythonEmbedded.Net.IntegrationTest;

/// <summary>
/// Real pip-backed tests for <see cref="PackageManager.EnsureRequirementsAsync"/> and
/// <see cref="PackageManager.ListOutdatedAsync"/>. Requires network access (PyPI).
/// </summary>
[TestFixture]
[Category("RequiresNetwork")]
public class PackageManagementIntegrationTests
{
    private static string FixturesDirectory => FixturePaths.FixturesDirectory;

    private TempRoot _root = null!;
    private PythonHost _host = null!;

    [SetUp]
    public void SetUp()
    {
        _root = new TempRoot();
        PythonOptions options = new() { RootDirectory = _root.Path };
        options.Sources.Clear();
        options.AddDirectorySource(FixturesDirectory, "fixtures");
        _host = new PythonHost(options);
    }

    [TearDown]
    public void TearDown() => _root.Dispose();

    [Test]
    public async Task EnsureRequirements_Reports_Change_Then_No_Change_Once_Satisfied()
    {
        PythonVirtualEnvironment env = await _host.GetEnvironmentAsync("3.13", "ensurereq", CancellationToken.None);
        string requirementsFile = Path.Combine(_root.Path, "requirements.txt");
        await File.WriteAllTextAsync(requirementsFile, "six==1.16.0\n");

        bool firstRun = await env.Packages.EnsureRequirementsAsync(requirementsFile);
        bool secondRun = await env.Packages.EnsureRequirementsAsync(requirementsFile);

        Assert.Multiple(() =>
        {
            Assert.That(firstRun, Is.True, "installing a package for the first time should report a change");
            Assert.That(secondRun, Is.False, "re-running against an already-satisfied requirements file should report no change");
        });
    }

    [Test]
    public async Task ListOutdated_Flags_A_Deliberately_Old_Pinned_Package()
    {
        PythonVirtualEnvironment env = await _host.GetEnvironmentAsync("3.13", "outdated", CancellationToken.None);
        await env.Packages.InstallAsync("six==1.10.0");

        IReadOnlyList<OutdatedPackage> outdated = await env.Packages.ListOutdatedAsync();

        Assert.That(outdated, Has.Some.Matches<OutdatedPackage>(p => p.Name == "six" && p.CurrentVersion == "1.10.0"));
    }
}
