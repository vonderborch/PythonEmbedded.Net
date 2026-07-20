using PythonEmbedded.Net;
using PythonEmbedded.Net.Test;

namespace PythonEmbedded.Net.IntegrationTest;

/// <summary>
/// Full-stack tests against a real Python from the fixture archives in <c>test/fixtures/</c>
/// (run <c>test/tools/fetch-fixtures.sh</c> first). Everything is offline except tests
/// tagged <c>RequiresNetwork</c>.
/// </summary>
[TestFixture]
public class EndToEndTests
{
    private static string FixturesDirectory => FixturePaths.FixturesDirectory;

    private TempRoot _root = null!;
    private PythonHost _host = null!;

    [SetUp]
    public void SetUp()
    {
        _root = new TempRoot();
        PythonOptions options = new() { RootDirectory = _root.Path, Offline = true };
        options.Sources.Clear();
        options.AddDirectorySource(FixturesDirectory, "fixtures");
        _host = new PythonHost(options);
    }

    [TearDown]
    public void TearDown() => _root.Dispose();

    [Test]
    public async Task Install_RealPython_And_RunCode()
    {
        PythonVirtualEnvironment env = await _host.GetEnvironmentAsync("3.13", "default", CancellationToken.None);

        PythonResult result = await env.RunCodeAsync("import sys; print(sys.version_info[:2])");

        Assert.Multiple(() =>
        {
            Assert.That(result.StandardOutput.Trim(), Is.EqualTo("(3, 13)"));
            Assert.That(env.PythonExecutable, Does.StartWith(env.Directory));
        });
    }

    [Test]
    public async Task Venv_Is_Isolated_And_Pip_Lists_Packages()
    {
        PythonVirtualEnvironment env = await _host.GetEnvironmentAsync("3.12", "default", CancellationToken.None);

        // The venv's sys.prefix must be the env directory, not the base install.
        PythonResult prefix = await env.RunCodeAsync("import sys; print(sys.prefix)");
        Assert.That(Path.GetFullPath(prefix.StandardOutput.Trim()), Is.EqualTo(Path.GetFullPath(env.Directory)));

        IReadOnlyList<InstalledPackage> packages = await env.Packages.ListAsync();
        Assert.That(packages.Select(p => p.Name), Does.Contain("pip"));
    }

    [Test]
    public async Task Script_Failure_Throws_With_Stderr()
    {
        PythonVirtualEnvironment env = await _host.GetEnvironmentAsync("3.13", "default", CancellationToken.None);

        PythonProcessException ex = Assert.ThrowsAsync<PythonProcessException>(
            () => env.RunCodeAsync("import sys; sys.stderr.write('kaboom'); sys.exit(7)"))!;

        Assert.Multiple(() =>
        {
            Assert.That(ex.Result.ExitCode, Is.EqualTo(7));
            Assert.That(ex.Result.StandardError, Does.Contain("kaboom"));
        });
    }

    [Test]
    public async Task RunScript_With_Args_And_Workdir()
    {
        PythonVirtualEnvironment env = await _host.GetEnvironmentAsync("3.13", "default", CancellationToken.None);
        string scriptDir = Path.Combine(_root.Path, "scripts");
        Directory.CreateDirectory(scriptDir);
        string script = Path.Combine(scriptDir, "echo_args.py");
        await File.WriteAllTextAsync(script, "import sys, os; print(' '.join(sys.argv[1:])); print(os.getcwd())");

        PythonResult result = await env.RunAsync(script, ["hello", "world"], new RunOptions { WorkingDirectory = scriptDir });

        string[] lines = result.StandardOutput.Trim().Split('\n');
        Assert.Multiple(() =>
        {
            Assert.That(lines[0], Is.EqualTo("hello world"));
            // macOS temp paths live behind the /var -> /private/var symlink; compare the resolved tail.
            Assert.That(lines[1].Trim(), Does.EndWith(scriptDir));
        });
    }

    [Test]
    public async Task Timeout_Kills_The_Process()
    {
        PythonVirtualEnvironment env = await _host.GetEnvironmentAsync("3.13", "default", CancellationToken.None);

        PythonProcessException ex = Assert.ThrowsAsync<PythonProcessException>(
            () => env.RunCodeAsync("import time; time.sleep(60)", new RunOptions { Timeout = TimeSpan.FromSeconds(2) }))!;
        Assert.That(ex.Kind, Is.EqualTo(PythonErrorKind.Timeout));
    }

    [Test]
    public async Task Live_Process_Streams_Lines_And_Accepts_Stdin()
    {
        PythonVirtualEnvironment env = await _host.GetEnvironmentAsync("3.13", "default", CancellationToken.None);
        string script = Path.Combine(_root.Path, "echo_server.py");
        await File.WriteAllTextAsync(script, """
            import sys
            print("ready")
            for line in sys.stdin:
                text = line.strip()
                if text == "quit":
                    break
                print(f"echo:{text}")
            """);

        List<string> lines = [];
        await using PythonProcess process = env.Start(script);
        SemaphoreSlim lineArrived = new(0);
        process.OutputLine += line =>
        {
            lock (lines)
            {
                lines.Add(line);
            }

            lineArrived.Release();
        };

        Assert.That(await lineArrived.WaitAsync(TimeSpan.FromSeconds(15)), "expected 'ready' line");
        await process.StandardInput.WriteLineAsync("hello");
        await process.StandardInput.FlushAsync();
        Assert.That(await lineArrived.WaitAsync(TimeSpan.FromSeconds(15)), "expected echo line");
        await process.StandardInput.WriteLineAsync("quit");
        await process.StandardInput.FlushAsync();

        int exitCode = await process.WaitForExitAsync(new CancellationTokenSource(TimeSpan.FromSeconds(15)).Token);

        Assert.Multiple(() =>
        {
            Assert.That(exitCode, Is.EqualTo(0));
            Assert.That(lines, Is.EqualTo((string[])["ready", "echo:hello"]));
        });
    }

    [Test]
    [Category("RequiresNetwork")]
    public async Task Pip_Install_From_PyPI_And_Import()
    {
        PythonOptions options = new() { RootDirectory = _root.Path };
        options.Sources.Clear();
        options.AddDirectorySource(FixturesDirectory, "fixtures");
        PythonHost host = new(options);

        PythonVirtualEnvironment env = await host.GetEnvironmentAsync("3.13", "netenv", CancellationToken.None);
        await env.Packages.InstallAsync("six");

        PythonResult result = await env.RunCodeAsync("import six; print(six.__version__)");
        Assert.That(result.StandardOutput.Trim(), Is.Not.Empty);

        IReadOnlyList<InstalledPackage> packages = await env.Packages.ListAsync();
        Assert.That(packages.Select(p => p.Name), Does.Contain("six"));

        await env.Packages.UninstallAsync("six");
        packages = await env.Packages.ListAsync();
        Assert.That(packages.Select(p => p.Name), Does.Not.Contain("six"));
    }
}
