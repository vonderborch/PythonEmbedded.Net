using PythonEmbedded.Net;
using PythonEmbedded.Net.Extensibility;
using PythonEmbedded.Net.Internals;
using PythonEmbedded.Net.Models;
using PythonEmbedded.Net.PackageManagers.Conda;
using PythonEmbedded.Net.PackageManagers.Poetry;
using PythonEmbedded.Net.Test;

namespace PythonEmbedded.Net.IntegrationTest;

/// <summary>Poetry and conda satellites against real tooling (both self-provision from the network).</summary>
[TestFixture]
[Category("RequiresNetwork")]
public class SatelliteInstallerTests
{
    private static PythonHost Host(TempRoot root, IPackageInstaller installer)
    {
        PythonOptions options = new() { RootDirectory = root.Path, Installer = installer };
        options.Sources.Clear();
        options.AddDirectorySource(FixturePaths.FixturesDirectory, "fixtures");
        return new PythonHost(options);
    }

    [Test]
    public async Task Poetry_Installs_A_PyProject_Into_The_Env()
    {
        using TempRoot root = new();
        string project = Path.Combine(root.Path, "sample-project");
        Directory.CreateDirectory(project);
        await File.WriteAllTextAsync(Path.Combine(project, "pyproject.toml"), """
            [project]
            name = "sample-project"
            version = "0.1.0"
            description = "test fixture"
            authors = [{name = "Test"}]
            requires-python = ">=3.10"
            dependencies = ["six >= 1.16"]

            [tool.poetry]
            package-mode = false

            [build-system]
            requires = ["poetry-core"]
            build-backend = "poetry.core.masonry.api"
            """);

        PythonHost host = Host(root, new PoetryInstaller());
        PythonVirtualEnvironment env = await host.GetEnvironmentAsync("3.13", "poetryenv", CancellationToken.None);

        await env.Packages.InstallAsync(new PackageRequest { ProjectDirectory = project });

        PythonResult result = await env.RunCodeAsync("import six; print(six.__version__)");
        Assert.That(result.StandardOutput.Trim(), Is.Not.Empty);

        // Ad-hoc install falls back to pip semantics.
        await env.Packages.InstallAsync("packaging");
        Assert.That((await env.Packages.ListAsync()).Select(p => p.Name), Does.Contain("packaging"));
    }

    [Test]
    public async Task Conda_Creates_Env_And_Installs_Via_Micromamba()
    {
        using TempRoot root = new();
        PythonHost host = Host(root, new CondaInstaller());

        PythonVirtualEnvironment env = await host.GetEnvironmentAsync("3.12", "condaenv", CancellationToken.None);

        // The conda env is self-contained; python must match the requested minor.
        PythonResult version = await env.RunCodeAsync("import sys; print(sys.version_info[:2])");
        Assert.That(version.StandardOutput.Trim(), Is.EqualTo("(3, 12)"));

        await env.Packages.InstallAsync("six");
        Assert.That((await env.Packages.ListAsync()).Select(p => p.Name), Does.Contain("six"));

        PythonResult imported = await env.RunCodeAsync("import six; print(six.__version__)");
        Assert.That(imported.StandardOutput.Trim(), Is.Not.Empty);

        await env.Packages.UninstallAsync("six");
        Assert.That((await env.Packages.ListAsync()).Select(p => p.Name), Does.Not.Contain("six"));
    }
}
