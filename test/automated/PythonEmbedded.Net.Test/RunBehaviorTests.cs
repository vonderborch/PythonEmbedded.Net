namespace PythonEmbedded.Net.Test;

[TestFixture]
public class RunBehaviorTests
{
    private static async Task<(PythonVirtualEnvironment Env, FakeRunner Runner)> BuildEnvAsync(TempRoot root, int exitCode)
    {
        FakeRunner runner = new(exitCode, stdout: "out", stderr: exitCode == 0 ? "" : "boom");
        PythonOptions options = root.Options(new FakeSource("3.13.5"));
        options.Installer = new FakeInstaller();
        options.Runner = runner;
        PythonHost host = new(options);
        return (await host.GetEnvironmentAsync("3.13", "default", CancellationToken.None), runner);
    }

    [Test]
    public async Task Nonzero_Exit_Throws_By_Default_With_Result_Attached()
    {
        using TempRoot root = new();
        (PythonVirtualEnvironment env, _) = await BuildEnvAsync(root, exitCode: 3);

        PythonProcessException ex = Assert.ThrowsAsync<PythonProcessException>(
            () => env.RunCodeAsync("import sys; sys.exit(3)"))!;

        Assert.Multiple(() =>
        {
            Assert.That(ex.Result.ExitCode, Is.EqualTo(3));
            Assert.That(ex.Result.StandardError, Is.EqualTo("boom"));
            Assert.That(ex.Kind, Is.EqualTo(PythonErrorKind.ExecutionFailed));
        });
    }

    [Test]
    public async Task Nonzero_Exit_Returns_Result_When_ThrowOnError_Disabled()
    {
        using TempRoot root = new();
        (PythonVirtualEnvironment env, _) = await BuildEnvAsync(root, exitCode: 3);

        PythonResult result = await env.RunCodeAsync("whatever", new RunOptions { ThrowOnError = false });

        Assert.Multiple(() =>
        {
            Assert.That(result.ExitCode, Is.EqualTo(3));
            Assert.That(result.Success, Is.False);
            Assert.That(() => result.EnsureSuccess(), Throws.TypeOf<PythonProcessException>());
        });
    }

    [Test]
    public async Task Invocations_Map_To_Kinds_And_Args()
    {
        using TempRoot root = new();
        (PythonVirtualEnvironment env, FakeRunner runner) = await BuildEnvAsync(root, exitCode: 0);

        await env.RunAsync("script.py", ["--flag"]);
        Assert.That(runner.LastInvocation, Is.EqualTo(new PythonInvocation(
            InvocationKind.Script, "script.py", ["--flag"], new RunOptions())).Using<PythonInvocation>(
            (a, b) => a.Kind == b.Kind && a.Target == b.Target && a.Args.SequenceEqual(b.Args)));

        await env.RunModuleAsync("http.server");
        Assert.That(runner.LastInvocation!.Kind, Is.EqualTo(InvocationKind.Module));

        await env.RunCodeAsync("print(1)");
        Assert.That(runner.LastInvocation!.Kind, Is.EqualTo(InvocationKind.Code));
    }

    [Test]
    public async Task Sync_Twins_Work()
    {
        using TempRoot root = new();
        (PythonVirtualEnvironment env, _) = await BuildEnvAsync(root, exitCode: 0);

        PythonResult result = env.RunCode("print(1)");
        Assert.That(result.StandardOutput, Is.EqualTo("out"));
    }
}
