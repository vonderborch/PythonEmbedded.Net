using PythonEmbedded.Net;
using PythonEmbedded.Net.Runners.PythonNet;
using PythonEmbedded.Net.Test;

namespace PythonEmbedded.Net.IntegrationTest;

/// <summary>
/// The Python.NET in-process runner against a fixture Python. Fully offline.
/// The engine binds to one environment for the whole process, so all tests share one
/// fixture-backed environment and this fixture must not run in parallel with itself.
/// </summary>
[TestFixture]
[NonParallelizable]
public class InProcessRunnerTests
{
    private static TempRoot _root = null!;
    private static PythonVirtualEnvironment _env = null!;

    [OneTimeSetUp]
    public static async Task OneTimeSetUp()
    {
        _root = new TempRoot();
        PythonOptions options = new()
        {
            RootDirectory = _root.Path,
            Offline = true,
            Runner = new InProcessRunner(),
        };
        options.Sources.Clear();
        options.AddDirectorySource(FixturePaths.FixturesDirectory, "fixtures");
        PythonHost host = new(options);
        _env = await host.GetEnvironmentAsync("3.13", "pythonnet", CancellationToken.None);
        PythonNetHost.Initialize(_env);
    }

    [OneTimeTearDown]
    public static void OneTimeTearDown() => _root.Dispose();
    // The temp root's Python stays loaded in-process, so directory deletion is best-effort.

    [Test]
    public async Task RunCode_Captures_Stdout()
    {
        PythonResult result = await _env.RunCodeAsync("print('in-process hello')");
        Assert.That(result.StandardOutput.Trim(), Is.EqualTo("in-process hello"));
    }

    [Test]
    public async Task Script_With_Args_And_SystemExit()
    {
        string script = Path.Combine(_root.Path, "argcheck.py");
        await File.WriteAllTextAsync(script, "import sys; print(sys.argv[1]); sys.exit(4)");

        PythonProcessException ex = Assert.ThrowsAsync<PythonProcessException>(
            () => _env.RunAsync(script, ["banana"]))!;

        Assert.Multiple(() =>
        {
            Assert.That(ex.Result.ExitCode, Is.EqualTo(4));
            Assert.That(ex.Result.StandardOutput.Trim(), Is.EqualTo("banana"));
        });
    }

    [Test]
    public async Task Errors_Produce_Traceback_On_Stderr()
    {
        PythonResult result = await _env.RunCodeAsync(
            "raise ValueError('boom')", new RunOptions { ThrowOnError = false });

        Assert.Multiple(() =>
        {
            Assert.That(result.ExitCode, Is.EqualTo(1));
            Assert.That(result.StandardError, Does.Contain("ValueError: boom"));
        });
    }

    [Test]
    public async Task Module_Execution_Works()
    {
        PythonResult result = await _env.RunCodeAsync("import sys; print(sys.prefix)");
        Assert.That(result.StandardOutput.Trim(), Does.Contain("pythonnet"), "venv must be active in-process");

        PythonResult moduleRun = await _env.RunModuleAsync("json.tool", ["--help"], new RunOptions { ThrowOnError = false });
        Assert.That(moduleRun.ExitCode, Is.Zero);
    }

    [Test]
    public void Direct_Interop_Via_RunInScope()
    {
        int sum = PythonNetHost.RunInScope(scope =>
        {
            scope.Exec("total = sum(range(10))");
            return scope.Get("total").As<int>();
        });
        Assert.That(sum, Is.EqualTo(45));
    }
}
