using PythonEmbedded.Net.Internals;
using PythonEmbedded.Net.Models;

namespace PythonEmbedded.Net.Test;

[TestFixture]
public class EnvironmentCloneExportImportTests
{
    [Test]
    public async Task Clone_Replays_Package_List_Into_A_New_Environment()
    {
        using TempRoot root = new();
        FakeInstaller installer = new();
        PythonOptions options = root.Options(new FakeSource("3.13.5"));
        options.Installer = installer;
        PythonHost host = new(options);

        PythonVirtualEnvironment source = await host.GetEnvironmentAsync("3.13", "source", CancellationToken.None);
        await source.Packages.InstallAsync("requests==2.31.0");

        PythonVirtualEnvironment clone = await source.CloneAsync("clone");

        Assert.Multiple(() =>
        {
            Assert.That(clone.Name, Is.EqualTo("clone"));
            Assert.That(clone.Directory, Is.Not.EqualTo(source.Directory));
        });

        IReadOnlyList<InstalledPackage> clonedPackages = await clone.Packages.ListAsync();
        Assert.That(clonedPackages.Select(p => (p.Name, p.Version)), Is.EquivalentTo(new[] { ("requests", "2.31.0") }));
    }

    [Test]
    public async Task ExportManifest_Writes_The_Installed_Package_List()
    {
        using TempRoot root = new();
        FakeInstaller installer = new();
        PythonOptions options = root.Options(new FakeSource("3.13.5"));
        options.Installer = installer;
        PythonHost host = new(options);

        PythonVirtualEnvironment env = await host.GetEnvironmentAsync("3.13", "default", CancellationToken.None);
        await env.Packages.InstallAsync("requests==2.31.0");

        string manifestPath = Path.Combine(root.Path, "manifest.json");
        await env.ExportManifestAsync(manifestPath);

        Assert.That(File.Exists(manifestPath));
        System.Text.Json.JsonSerializerOptions jsonOptions = new(System.Text.Json.JsonSerializerDefaults.Web);
        EnvironmentManifest manifest = System.Text.Json.JsonSerializer.Deserialize<EnvironmentManifest>(
            await File.ReadAllTextAsync(manifestPath), jsonOptions)!;

        Assert.Multiple(() =>
        {
            Assert.That(manifest.InstallerName, Is.EqualTo("fake"));
            Assert.That(manifest.PythonVersion, Is.EqualTo("3.13.5"));
            Assert.That(manifest.Packages.Select(p => (p.Name, p.Version)), Is.EquivalentTo(new[] { ("requests", "2.31.0") }));
        });
    }

    [Test]
    public async Task Import_Recreates_An_Equivalent_Environment_From_A_Manifest()
    {
        using TempRoot root = new();
        FakeInstaller installer = new();
        PythonOptions options = root.Options(new FakeSource("3.13.5"));
        options.Installer = installer;
        PythonHost host = new(options);

        PythonInstallation install = await host.GetInstallationAsync("3.13", CancellationToken.None);
        EnvironmentManifest manifest = new(
            "fake", "3.13.5", [new InstalledPackage("requests", "2.31.0")], DateTimeOffset.UtcNow);
        string manifestPath = Path.Combine(root.Path, "manifest.json");
        await File.WriteAllTextAsync(manifestPath, System.Text.Json.JsonSerializer.Serialize(manifest));

        PythonVirtualEnvironment imported = await install.ImportEnvironmentAsync("imported", manifestPath, installer: installer);

        IReadOnlyList<InstalledPackage> packages = await imported.Packages.ListAsync();
        Assert.That(packages.Select(p => (p.Name, p.Version)), Is.EquivalentTo(new[] { ("requests", "2.31.0") }));
    }
}
