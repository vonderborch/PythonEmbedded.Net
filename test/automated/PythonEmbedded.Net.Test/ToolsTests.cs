using PythonEmbedded.Net.Exceptions;
using PythonEmbedded.Net.Extensibility;
using PythonEmbedded.Net.Internals;

namespace PythonEmbedded.Net.Test;

[TestFixture]
[NonParallelizable] // some tests manipulate process-wide environment variables
public class ToolsTests
{
    private static async Task<PythonInstallation> InstallAsync(TempRoot root)
    {
        PythonHost host = new(root.Options(new FakeSource("3.13.5")));
        return await host.GetInstallationAsync("3.13", CancellationToken.None);
    }

    private static string ToolFileName(string name) => OperatingSystem.IsWindows() ? name + ".exe" : name;

    [Test]
    public async Task Existing_Tool_Next_To_Interpreter_Is_Used_Without_Provisioning()
    {
        using TempRoot root = new();
        PythonInstallation install = await InstallAsync(root);
        string toolPath = Path.Combine(Path.GetDirectoryName(install.PythonExecutable)!, ToolFileName("mytool"));
        File.WriteAllText(toolPath, "tool");

        bool provisioned = false;
        string resolved = await Tools.EnsureAsync(install, "mytool", (_, _) =>
        {
            provisioned = true;
            return Task.CompletedTask;
        });

        Assert.Multiple(() =>
        {
            Assert.That(resolved, Is.EqualTo(toolPath));
            Assert.That(provisioned, Is.False);
        });
    }

    [Test]
    public async Task Provision_Runs_Once_Then_Tool_Is_Cached()
    {
        using TempRoot root = new();
        PythonInstallation install = await InstallAsync(root);

        int provisionCount = 0;
        Func<ToolContext, CancellationToken, Task> provision = (context, _) =>
        {
            provisionCount++;
            File.WriteAllText(Path.Combine(context.ToolsDirectory, ToolFileName("mytool")), "tool");
            return Task.CompletedTask;
        };

        string first = await Tools.EnsureAsync(install, "mytool", provision);
        string second = await Tools.EnsureAsync(install, "mytool", provision);

        Assert.Multiple(() =>
        {
            Assert.That(provisionCount, Is.EqualTo(1));
            Assert.That(second, Is.EqualTo(first));
            Assert.That(File.Exists(first));
        });
    }

    [Test]
    public async Task Environment_Variable_Override_Wins()
    {
        using TempRoot root = new();
        PythonInstallation install = await InstallAsync(root);
        string overridePath = Path.Combine(root.Path, "custom-tool");
        File.WriteAllText(overridePath, "tool");

        Environment.SetEnvironmentVariable("PYEMBED_TOOL_MYTOOL", overridePath);
        try
        {
            string resolved = await Tools.EnsureAsync(install, "mytool", (_, _) => Task.CompletedTask);
            Assert.That(resolved, Is.EqualTo(overridePath));
        }
        finally
        {
            Environment.SetEnvironmentVariable("PYEMBED_TOOL_MYTOOL", null);
        }
    }

    [Test]
    public async Task Broken_Environment_Variable_Override_Throws()
    {
        using TempRoot root = new();
        PythonInstallation install = await InstallAsync(root);

        Environment.SetEnvironmentVariable("PYEMBED_TOOL_MYTOOL", Path.Combine(root.Path, "missing"));
        try
        {
            PythonException ex = Assert.ThrowsAsync<PythonException>(
                () => Tools.EnsureAsync(install, "mytool", (_, _) => Task.CompletedTask))!;
            Assert.That(ex.Kind, Is.EqualTo(PythonErrorKind.ToolMissing));
        }
        finally
        {
            Environment.SetEnvironmentVariable("PYEMBED_TOOL_MYTOOL", null);
        }
    }

    [Test]
    public async Task Provision_That_Produces_Nothing_Throws_ToolMissing()
    {
        using TempRoot root = new();
        PythonInstallation install = await InstallAsync(root);

        PythonException ex = Assert.ThrowsAsync<PythonException>(
            () => Tools.EnsureAsync(install, "mytool", (_, _) => Task.CompletedTask))!;
        Assert.That(ex.Kind, Is.EqualTo(PythonErrorKind.ToolMissing));
    }
}
