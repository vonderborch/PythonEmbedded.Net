using PythonEmbedded.Net.Exceptions;
using PythonEmbedded.Net.Internals;

namespace PythonEmbedded.Net.Test;

[TestFixture]
public class PythonHostTests
{
    [Test]
    public async Task Install_Then_Reuse_Without_Reinstalling()
    {
        using TempRoot root = new();
        FakeSource source = new("3.13.5");
        PythonHost host = new(root.Options(source));

        PythonInstallation first = await host.GetInstallationAsync("3.13", CancellationToken.None);
        PythonInstallation second = await host.GetInstallationAsync("3.13", CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(source.InstallCount, Is.EqualTo(1), "second call must hit the on-disk install");
            Assert.That(first.Version.ToString(), Is.EqualTo("3.13.5"));
            Assert.That(second.Directory, Is.EqualTo(first.Directory));
            Assert.That(File.Exists(first.PythonExecutable));
            Assert.That(File.Exists(Path.Combine(first.Directory, "install.json")));
        });
    }

    [Test]
    public async Task Sources_Are_Tried_In_Order_With_Fallthrough()
    {
        using TempRoot root = new();
        FakeSource cannotProvide = new(version: null, name: "empty");
        FakeSource canProvide = new("3.12.9", name: "second");
        PythonHost host = new(root.Options(cannotProvide, canProvide));

        PythonInstallation install = await host.GetInstallationAsync("3.12", CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(install.SourceName, Is.EqualTo("second"));
            Assert.That(canProvide.InstallCount, Is.EqualTo(1));
        });
    }

    [Test]
    public void No_Source_Can_Provide_Throws_VersionNotFound()
    {
        using TempRoot root = new();
        PythonHost host = new(root.Options(new FakeSource(version: null)));

        PythonException ex = Assert.ThrowsAsync<PythonException>(
            () => host.GetInstallationAsync("3.11", CancellationToken.None))!;
        Assert.That(ex.Kind, Is.EqualTo(PythonErrorKind.VersionNotFound));
    }

    [Test]
    public async Task Missing_Marker_Causes_Reinstall()
    {
        using TempRoot root = new();
        FakeSource source = new("3.13.5");
        PythonHost host = new(root.Options(source));

        PythonInstallation install = await host.GetInstallationAsync("3.13", CancellationToken.None);
        File.Delete(Path.Combine(install.Directory, "install.json"));

        PythonInstallation reinstalled = await host.GetInstallationAsync("3.13", CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(source.InstallCount, Is.EqualTo(2), "marker-less directory must not count as installed");
            Assert.That(File.Exists(Path.Combine(reinstalled.Directory, "install.json")));
        });
    }

    [Test]
    public async Task Highest_Matching_Version_Wins()
    {
        using TempRoot root = new();
        PythonHost host = new(root.Options(new FakeSource("3.13.2", name: "old")));
        await host.GetInstallationAsync("3.13.2", CancellationToken.None);

        PythonHost host2 = new(root.Options(new FakeSource("3.13.9", name: "new")));
        await host2.GetInstallationAsync("3.13.9", CancellationToken.None);

        PythonInstallation best = await host2.GetInstallationAsync("3.13", CancellationToken.None);
        Assert.That(best.Version.ToString(), Is.EqualTo("3.13.9"));
    }

    [Test]
    public async Task Environment_Created_Once_Then_Reused()
    {
        using TempRoot root = new();
        FakeInstaller installer = new();
        PythonOptions options = root.Options(new FakeSource("3.13.5"));
        options.Installer = installer;
        PythonHost host = new(options);

        PythonVirtualEnvironment first = await host.GetEnvironmentAsync("3.13", "default", CancellationToken.None);
        PythonVirtualEnvironment second = await host.GetEnvironmentAsync("3.13", "default", CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(installer.CreateCount, Is.EqualTo(1));
            Assert.That(second.Directory, Is.EqualTo(first.Directory));
            Assert.That(File.Exists(first.PythonExecutable));
            Assert.That(File.Exists(Path.Combine(first.Directory, "env.json")));
            Assert.That(first.Name, Is.EqualTo("default"));
            Assert.That(first.Installation.Version.ToString(), Is.EqualTo("3.13.5"));
        });
    }

    [Test]
    public async Task Distinct_Environment_Names_Get_Distinct_Directories()
    {
        using TempRoot root = new();
        PythonOptions options = root.Options(new FakeSource("3.13.5"));
        options.Installer = new FakeInstaller();
        PythonHost host = new(options);

        PythonVirtualEnvironment a = await host.GetEnvironmentAsync("3.13", "alpha", CancellationToken.None);
        PythonVirtualEnvironment b = await host.GetEnvironmentAsync("3.13", "beta", CancellationToken.None);

        Assert.That(a.Directory, Is.Not.EqualTo(b.Directory));
    }

    [TestCase("has space")]
    [TestCase("has/slash")]
    [TestCase("")]
    public void Invalid_Environment_Name_Throws(string name)
    {
        using TempRoot root = new();
        PythonOptions options = root.Options(new FakeSource("3.13.5"));
        options.Installer = new FakeInstaller();
        PythonHost host = new(options);

        Assert.That(
            () => host.GetEnvironmentAsync("3.13", name, CancellationToken.None),
            Throws.InstanceOf<ArgumentException>());
    }

    [Test]
    public async Task Remove_Deletes_Install_And_Environments()
    {
        using TempRoot root = new();
        PythonOptions options = root.Options(new FakeSource("3.13.5"));
        options.Installer = new FakeInstaller();
        PythonHost host = new(options);

        PythonVirtualEnvironment env = await host.GetEnvironmentAsync("3.13", "default", CancellationToken.None);
        string installDirectory = env.Installation.Directory;

        await host.RemoveAsync(env.Installation, CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(Directory.Exists(installDirectory), Is.False);
            Assert.That(Directory.Exists(env.Directory), Is.False);
            Assert.That(host.ListInstallationsAsync(CancellationToken.None).Result, Is.Empty);
        });
    }

    [Test]
    public async Task ListInstallations_Returns_All_Valid_Installs()
    {
        using TempRoot root = new();
        PythonHost host = new(root.Options(new FakeSource("3.12.4", name: "a")));
        await host.GetInstallationAsync("3.12", CancellationToken.None);

        PythonHost host2 = new(root.Options(new FakeSource("3.13.5", name: "b")));
        await host2.GetInstallationAsync("3.13", CancellationToken.None);

        IReadOnlyList<PythonInstallation> installs = await host2.ListInstallationsAsync(CancellationToken.None);
        Assert.That(installs.Select(i => i.Version.ToString()), Is.EquivalentTo((string[])["3.12.4", "3.13.5"]));
    }
}
