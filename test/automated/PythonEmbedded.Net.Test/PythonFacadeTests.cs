namespace PythonEmbedded.Net.Test;

[TestFixture]
[NonParallelizable] // the facade is process-global state
public class PythonFacadeTests
{
    [TearDown]
    public void TearDown() => PythonEnvironment.Reset();

    [Test]
    public void Configure_After_First_Use_Throws()
    {
        using TempRoot root = new();
        PythonEnvironment.Configure(o =>
        {
            o.RootDirectory = root.Path;
            o.Offline = true;
            o.Sources.Clear();
            o.Sources.Add(new FakeSource("3.13.5"));
            o.Installer = new FakeInstaller();
        });

        _ = PythonEnvironment.GetInstallation("3.13");

        Assert.That(
            () => PythonEnvironment.Configure(o => o.Offline = false),
            Throws.InvalidOperationException);
    }

    [Test]
    public async Task Happy_Path_Through_The_Facade()
    {
        using TempRoot root = new();
        FakeRunner runner = new(0, stdout: "hi");
        PythonEnvironment.Configure(o =>
        {
            o.RootDirectory = root.Path;
            o.Offline = true;
            o.Sources.Clear();
            o.Sources.Add(new FakeSource("3.13.5"));
            o.Installer = new FakeInstaller();
            o.Runner = runner;
        });

        PythonVirtualEnvironment env = await PythonEnvironment.GetEnvironmentAsync("3.13", "default");
        PythonResult result = await env.RunCodeAsync("print('hi')");

        Assert.Multiple(() =>
        {
            Assert.That(result.StandardOutput, Is.EqualTo("hi"));
            Assert.That(env.Installation.Version.ToString(), Is.EqualTo("3.13.5"));
        });
    }

    [Test]
    public void Reset_Allows_Reconfiguration()
    {
        PythonEnvironment.Configure(o => o.Offline = true);
        PythonEnvironment.Reset();
        Assert.That(() => PythonEnvironment.Configure(o => o.Offline = true), Throws.Nothing);
    }
}
