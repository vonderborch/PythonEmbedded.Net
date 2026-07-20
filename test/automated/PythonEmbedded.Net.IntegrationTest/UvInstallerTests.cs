using PythonEmbedded.Net;
using PythonEmbedded.Net.PackageManagers.Uv;
using PythonEmbedded.Net.Test;

namespace PythonEmbedded.Net.IntegrationTest;

/// <summary>
/// The uv satellite against a real fixture Python. Network-tagged: uv itself is provisioned
/// from PyPI on first use, and packages are installed from PyPI.
/// </summary>
[TestFixture]
[Category("RequiresNetwork")]
public class UvInstallerTests
{
    [Test]
    public async Task Uv_Creates_Env_Installs_And_Uninstalls()
    {
        using TempRoot root = new();
        PythonOptions options = new() { RootDirectory = root.Path };
        options.Sources.Clear();
        options.AddDirectorySource(FixturePaths.FixturesDirectory, "fixtures");
        options.Installer = new UvInstaller();
        PythonHost host = new(options);

        PythonVirtualEnvironment env = await host.GetEnvironmentAsync("3.13", "uvenv", CancellationToken.None);

        // uv --seed puts pip in the env, so python -m pip also works.
        PythonResult prefix = await env.RunCodeAsync("import sys; print(sys.prefix)");
        Assert.That(prefix.StandardOutput.Trim(), Does.EndWith("uvenv"));

        await env.Packages.InstallAsync("six");
        PythonResult imported = await env.RunCodeAsync("import six; print(six.__version__)");
        Assert.That(imported.StandardOutput.Trim(), Is.Not.Empty);

        IReadOnlyList<InstalledPackage> packages = await env.Packages.ListAsync();
        Assert.That(packages.Select(p => p.Name), Does.Contain("six"));

        await env.Packages.UninstallAsync("six");
        Assert.That((await env.Packages.ListAsync()).Select(p => p.Name), Does.Not.Contain("six"));
    }
}
