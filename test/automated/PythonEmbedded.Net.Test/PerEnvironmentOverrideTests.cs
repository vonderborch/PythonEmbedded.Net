using PythonEmbedded.Net.Exceptions;
using PythonEmbedded.Net.Internals;
using PythonEmbedded.Net.Models;

namespace PythonEmbedded.Net.Test;

[TestFixture]
public class PerEnvironmentOverrideTests
{
    [Test]
    public async Task Explicit_Installer_At_Creation_Is_Recorded_And_Used()
    {
        using TempRoot root = new();
        FakeInstaller custom = new("custom");
        PythonOptions options = root.Options(new FakeSource("3.13.5"));
        options.Installer = new FakeInstaller("default-installer");
        PythonHost host = new(options);

        PythonVirtualEnvironment env = await host.GetEnvironmentAsync("3.13", "default", CancellationToken.None, installer: custom);

        Assert.That(custom.CreateCount, Is.EqualTo(1));
    }

    [Test]
    public async Task Reopening_With_Same_Installer_Succeeds_Without_Recreating()
    {
        using TempRoot root = new();
        FakeInstaller custom = new("custom");
        PythonOptions options = root.Options(new FakeSource("3.13.5"));
        options.Installer = new FakeInstaller("default-installer");
        PythonHost host = new(options);

        await host.GetEnvironmentAsync("3.13", "default", CancellationToken.None, installer: custom);
        await host.GetEnvironmentAsync("3.13", "default", CancellationToken.None, installer: custom);

        Assert.That(custom.CreateCount, Is.EqualTo(1));
    }

    [Test]
    public async Task Reopening_With_A_Different_Installer_Throws()
    {
        using TempRoot root = new();
        PythonOptions options = root.Options(new FakeSource("3.13.5"));
        options.Installer = new FakeInstaller("default-installer");
        PythonHost host = new(options);

        await host.GetEnvironmentAsync("3.13", "default", CancellationToken.None, installer: new FakeInstaller("a"));

        PythonException ex = Assert.ThrowsAsync<PythonException>(
            () => host.GetEnvironmentAsync("3.13", "default", CancellationToken.None, installer: new FakeInstaller("b")))!;
        Assert.That(ex.Kind, Is.EqualTo(PythonErrorKind.EnvironmentFailed));
    }

    [Test]
    public async Task Reopening_Without_An_Override_Falls_Back_To_Global_Default_And_Enforces_The_Recorded_Installer()
    {
        using TempRoot root = new();
        PythonOptions options = root.Options(new FakeSource("3.13.5"));
        options.Installer = new FakeInstaller("default-installer");
        PythonHost host = new(options);

        await host.GetEnvironmentAsync("3.13", "default", CancellationToken.None, installer: new FakeInstaller("custom"));

        // No override passed this time; the global default ("default-installer") doesn't match what
        // the environment was actually created with ("custom") — this must be rejected, not silently
        // substituted, since an environment's installer is fixed for its lifetime.
        PythonException ex = Assert.ThrowsAsync<PythonException>(
            () => host.GetEnvironmentAsync("3.13", "default", CancellationToken.None))!;
        Assert.That(ex.Kind, Is.EqualTo(PythonErrorKind.EnvironmentFailed));
    }

    [Test]
    public async Task Runner_Override_Is_Not_Recorded_And_Can_Differ_Per_Fetch()
    {
        using TempRoot root = new();
        PythonOptions options = root.Options(new FakeSource("3.13.5"));
        options.Installer = new FakeInstaller();
        PythonHost host = new(options);

        FakeRunner runnerA = new(0, stdout: "from-a");
        FakeRunner runnerB = new(0, stdout: "from-b");

        PythonVirtualEnvironment first = await host.GetEnvironmentAsync("3.13", "default", CancellationToken.None, runner: runnerA);
        PythonResult firstResult = await first.RunCodeAsync("print(1)");

        PythonVirtualEnvironment second = await host.GetEnvironmentAsync("3.13", "default", CancellationToken.None, runner: runnerB);
        PythonResult secondResult = await second.RunCodeAsync("print(1)");

        Assert.Multiple(() =>
        {
            Assert.That(firstResult.StandardOutput, Is.EqualTo("from-a"));
            Assert.That(secondResult.StandardOutput, Is.EqualTo("from-b"));
        });
    }
}
