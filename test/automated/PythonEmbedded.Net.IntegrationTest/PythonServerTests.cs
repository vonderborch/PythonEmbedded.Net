using PythonEmbedded.Net;
using PythonEmbedded.Net.Internals;
using PythonEmbedded.Net.Test;

namespace PythonEmbedded.Net.IntegrationTest;

/// <summary>
/// Exercises <see cref="PythonServer"/> against a real Python process that speaks
/// newline-delimited JSON: echoes each request's <c>id</c> back with a computed result,
/// occasionally emits an id-less line, and replies to a "sleep" request after a delay
/// so cancellation-isolation can be verified.
/// </summary>
[TestFixture]
public class PythonServerTests
{
    private static string FixturesDirectory => FixturePaths.FixturesDirectory;

    private TempRoot _root = null!;
    private PythonHost _host = null!;
    private string _scriptPath = null!;

    [SetUp]
    public async Task SetUp()
    {
        _root = new TempRoot();
        PythonOptions options = new() { RootDirectory = _root.Path, Offline = true };
        options.Sources.Clear();
        options.AddDirectorySource(FixturesDirectory, "fixtures");
        _host = new PythonHost(options);

        _scriptPath = Path.Combine(_root.Path, "server_echo.py");
        await File.WriteAllTextAsync(_scriptPath, """
            import json
            import sys
            import time

            print("hello from server")
            for raw in sys.stdin:
                raw = raw.strip()
                if not raw:
                    continue
                request = json.loads(raw)
                if request.get("op") == "sleep":
                    time.sleep(request.get("seconds", 0))
                print(json.dumps({"id": request["id"], "result": request.get("value", 0) * 2}))
                sys.stdout.flush()
            """);
    }

    [TearDown]
    public void TearDown() => _root.Dispose();

    [Test]
    public async Task RequestAsync_Correlates_Response_By_Id()
    {
        PythonVirtualEnvironment env = await _host.GetEnvironmentAsync("3.13", "default", CancellationToken.None);
        await using PythonServer server = PythonServer.Start(env, _scriptPath);

        System.Text.Json.JsonElement response = await server.RequestAsync(new { op = "double", value = 21 });

        Assert.That(response.GetProperty("result").GetInt32(), Is.EqualTo(42));
    }

    [Test]
    public async Task Multiple_Requests_Match_Responses_By_Id_Even_Out_Of_Order()
    {
        PythonVirtualEnvironment env = await _host.GetEnvironmentAsync("3.13", "default", CancellationToken.None);
        await using PythonServer server = PythonServer.Start(env, _scriptPath);

        Task<System.Text.Json.JsonElement> first = server.RequestAsync(new { op = "double", value = 1 });
        Task<System.Text.Json.JsonElement> second = server.RequestAsync(new { op = "double", value = 2 });
        Task<System.Text.Json.JsonElement> third = server.RequestAsync(new { op = "double", value = 3 });

        System.Text.Json.JsonElement[] results = await Task.WhenAll(first, second, third);

        Assert.Multiple(() =>
        {
            Assert.That(results[0].GetProperty("result").GetInt32(), Is.EqualTo(2));
            Assert.That(results[1].GetProperty("result").GetInt32(), Is.EqualTo(4));
            Assert.That(results[2].GetProperty("result").GetInt32(), Is.EqualTo(6));
        });
    }

    [Test]
    public async Task UnsolicitedLine_Fires_For_Lines_Without_An_Id()
    {
        PythonVirtualEnvironment env = await _host.GetEnvironmentAsync("3.13", "default", CancellationToken.None);
        await using PythonServer server = PythonServer.Start(env, _scriptPath);

        TaskCompletionSource<string> unsolicited = new(TaskCreationOptions.RunContinuationsAsynchronously);
        server.UnsolicitedLine += line => unsolicited.TrySetResult(line);

        string line = await unsolicited.Task.WaitAsync(TimeSpan.FromSeconds(15));
        Assert.That(line, Is.EqualTo("hello from server"));
    }

    [Test]
    public async Task Cancelling_One_Pending_Request_Does_Not_Affect_Others()
    {
        PythonVirtualEnvironment env = await _host.GetEnvironmentAsync("3.13", "default", CancellationToken.None);
        await using PythonServer server = PythonServer.Start(env, _scriptPath);

        using CancellationTokenSource cts = new();
        Task<System.Text.Json.JsonElement> slow = server.RequestAsync(new { op = "sleep", seconds = 10, value = 5 }, cts.Token);
        Task<System.Text.Json.JsonElement> fast = server.RequestAsync(new { op = "double", value = 4 });

        cts.Cancel();

        Assert.That(async () => await slow, Throws.InstanceOf<OperationCanceledException>());
        System.Text.Json.JsonElement fastResult = await fast.WaitAsync(TimeSpan.FromSeconds(15));
        Assert.That(fastResult.GetProperty("result").GetInt32(), Is.EqualTo(8));
    }
}
