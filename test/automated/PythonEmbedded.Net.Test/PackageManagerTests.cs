using PythonEmbedded.Net.Internals;
using PythonEmbedded.Net.Models;

namespace PythonEmbedded.Net.Test;

[TestFixture]
public class PackageManagerTests
{
    [Test]
    public async Task EnsureRequirements_Delegates_To_Installer_And_Reports_Change()
    {
        using TempRoot root = new();
        FakeInstaller installer = new();
        PythonOptions options = root.Options(new FakeSource("3.13.5"));
        options.Installer = installer;
        PythonHost host = new(options);
        PythonVirtualEnvironment env = await host.GetEnvironmentAsync("3.13", "default", CancellationToken.None);

        installer.EnsureRequirementsChanged = true;
        bool changed = await env.Packages.EnsureRequirementsAsync("requirements.txt");

        Assert.Multiple(() =>
        {
            Assert.That(changed, Is.True);
            Assert.That(installer.LastRequirementsFile, Is.EqualTo("requirements.txt"));
        });
    }

    [Test]
    public async Task EnsureRequirements_Reports_No_Change_When_Already_Satisfied()
    {
        using TempRoot root = new();
        FakeInstaller installer = new() { EnsureRequirementsChanged = false };
        PythonOptions options = root.Options(new FakeSource("3.13.5"));
        options.Installer = installer;
        PythonHost host = new(options);
        PythonVirtualEnvironment env = await host.GetEnvironmentAsync("3.13", "default", CancellationToken.None);

        bool changed = await env.Packages.EnsureRequirementsAsync("requirements.txt");
        Assert.That(changed, Is.False);
    }

    [Test]
    public async Task ListOutdated_Delegates_To_Installer()
    {
        using TempRoot root = new();
        FakeInstaller installer = new();
        PythonOptions options = root.Options(new FakeSource("3.13.5"));
        options.Installer = installer;
        PythonHost host = new(options);
        PythonVirtualEnvironment env = await host.GetEnvironmentAsync("3.13", "default", CancellationToken.None);
        installer.OutdatedToReturn = [new OutdatedPackage("six", "1.10.0", "1.16.0")];

        IReadOnlyList<OutdatedPackage> outdated = await env.Packages.ListOutdatedAsync();

        Assert.That(outdated, Has.Count.EqualTo(1));
        Assert.That(outdated[0].Name, Is.EqualTo("six"));
    }

    [Test]
    public async Task InstallerName_Reflects_The_Configured_Installer()
    {
        using TempRoot root = new();
        FakeInstaller installer = new(name: "fake-pm");
        PythonOptions options = root.Options(new FakeSource("3.13.5"));
        options.Installer = installer;
        PythonHost host = new(options);
        PythonVirtualEnvironment env = await host.GetEnvironmentAsync("3.13", "default", CancellationToken.None);

        Assert.That(env.Packages.InstallerName, Is.EqualTo("fake-pm"));
    }
}
