using System.Collections.Concurrent;
using System.Text.Json;
using System.Text.Json.Nodes;
using PythonEmbedded.Net.Models;

namespace PythonEmbedded.Net;

/// <summary>
/// Correlates newline-delimited JSON request/response messages over a long-lived <see cref="PythonProcess"/>'s
/// stdin/stdout — for Python processes implementing a simple RPC-style protocol where each request gets an
/// injected <c>"id"</c> and each response line echoes it back. <see cref="RequestAsync{T}(T, CancellationToken)"/>'s
/// payload must serialize to a JSON object so an <c>"id"</c> field can be injected; response lines that aren't a
/// JSON object, or whose <c>"id"</c> doesn't match a pending request, are raised via <see cref="UnsolicitedLine"/>.
/// </summary>
public sealed class PythonServer : IAsyncDisposable
{
    private readonly PythonProcess _process;
    private readonly ConcurrentDictionary<string, TaskCompletionSource<JsonElement>> _pending = new();
    private long _nextId;

    private PythonServer(PythonProcess process)
    {
        _process = process;
        _process.OutputLine += OnOutputLine;
        _process.ErrorLine += line => ErrorLine?.Invoke(line);
    }

    /// <summary>Starts <paramref name="scriptPath"/> in <paramref name="env"/> as a correlated request/response server.</summary>
    public static PythonServer Start(PythonVirtualEnvironment env, string scriptPath, string[]? args = null, RunOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(env);
        return new PythonServer(env.Start(scriptPath, args, options));
    }

    /// <summary>Raised for stdout lines that aren't a JSON object, or whose <c>"id"</c> matches no pending request.</summary>
    public event Action<string>? UnsolicitedLine;

    /// <summary>Raised for each line the process writes to stderr.</summary>
    public event Action<string>? ErrorLine;

    /// <summary>
    /// Serializes <paramref name="payload"/> to a JSON object, injects a unique <c>"id"</c>, writes it as one
    /// line to the process's stdin, and awaits a response line echoing the same <c>"id"</c>. Cancelling
    /// <paramref name="ct"/> only affects this request — other pending requests are unaffected.
    /// </summary>
    public async Task<JsonElement> RequestAsync<T>(T payload, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(payload);
        JsonNode? node = JsonSerializer.SerializeToNode(payload);
        if (node is not JsonObject obj)
        {
            throw new ArgumentException("Payload must serialize to a JSON object so an 'id' field can be injected.", nameof(payload));
        }

        string id = Interlocked.Increment(ref _nextId).ToString();
        obj["id"] = id;

        TaskCompletionSource<JsonElement> tcs = new(TaskCreationOptions.RunContinuationsAsynchronously);
        _pending[id] = tcs;

        await using CancellationTokenRegistration registration = ct.Register(() =>
        {
            if (_pending.TryRemove(id, out TaskCompletionSource<JsonElement>? removed))
            {
                removed.TrySetCanceled(ct);
            }
        });

        await _process.StandardInput.WriteLineAsync(obj.ToJsonString()).ConfigureAwait(false);
        await _process.StandardInput.FlushAsync(ct).ConfigureAwait(false);

        return await tcs.Task.ConfigureAwait(false);
    }

    /// <inheritdoc cref="RequestAsync{T}(T, CancellationToken)"/>
    public JsonElement Request<T>(T payload) => RequestAsync(payload).GetAwaiter().GetResult();

    private void OnOutputLine(string line)
    {
        JsonElement root;
        try
        {
            using JsonDocument document = JsonDocument.Parse(line);
            root = document.RootElement.Clone();
        }
        catch (JsonException)
        {
            UnsolicitedLine?.Invoke(line);
            return;
        }

        if (root.ValueKind != JsonValueKind.Object
            || !root.TryGetProperty("id", out JsonElement idElement)
            || idElement.ValueKind != JsonValueKind.String)
        {
            UnsolicitedLine?.Invoke(line);
            return;
        }

        string? id = idElement.GetString();
        if (id is null || !_pending.TryRemove(id, out TaskCompletionSource<JsonElement>? tcs))
        {
            UnsolicitedLine?.Invoke(line);
            return;
        }

        tcs.TrySetResult(root);
    }

    /// <summary>Kills the underlying process and cancels any pending requests.</summary>
    public async ValueTask DisposeAsync()
    {
        foreach (string id in _pending.Keys)
        {
            if (_pending.TryRemove(id, out TaskCompletionSource<JsonElement>? tcs))
            {
                tcs.TrySetCanceled();
            }
        }

        await _process.DisposeAsync().ConfigureAwait(false);
    }
}
