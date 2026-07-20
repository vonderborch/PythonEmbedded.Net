using System.Diagnostics;
using System.Text;
using PythonEmbedded.Net.Exceptions;
using PythonEmbedded.Net.Models;

namespace PythonEmbedded.Net.Extensibility;

/// <summary>
/// Runs an external process with buffered output. Used by the built-in runner and pip installer,
/// and public so satellite installers/runners (uv, conda, poetry, ...) can drive their tools
/// without reimplementing process plumbing. Throws <see cref="PythonProcessException"/> with
/// <see cref="PythonErrorKind.Timeout"/> when <paramref name="timeout"/> elapses; otherwise the
/// result is returned regardless of exit code.
/// </summary>
public static class Subprocess
{
    public static async Task<PythonResult> RunAsync(
        string executable,
        IReadOnlyList<string> arguments,
        string? workingDirectory = null,
        IReadOnlyDictionary<string, string>? environment = null,
        string? stdin = null,
        TimeSpan? timeout = null,
        CancellationToken ct = default)
    {
        ProcessStartInfo startInfo = new(executable)
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            RedirectStandardInput = stdin is not null,
            UseShellExecute = false,
            CreateNoWindow = true,
            WorkingDirectory = workingDirectory ?? string.Empty,
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8,
        };
        foreach (string argument in arguments)
        {
            startInfo.ArgumentList.Add(argument);
        }

        if (environment is not null)
        {
            foreach ((string key, string value) in environment)
            {
                startInfo.Environment[key] = value;
            }
        }

        long startTimestamp = Stopwatch.GetTimestamp();
        using Process process = new();
        process.StartInfo = startInfo;

        try
        {
            process.Start();
        }
        catch (Exception ex)
        {
            throw new PythonException(
                PythonErrorKind.ExecutionFailed, $"Failed to start '{executable}': {ex.Message}", ex);
        }

        Task<string> stdoutTask = process.StandardOutput.ReadToEndAsync(ct);
        Task<string> stderrTask = process.StandardError.ReadToEndAsync(ct);

        if (stdin is not null)
        {
            await process.StandardInput.WriteAsync(stdin.AsMemory(), ct).ConfigureAwait(false);
            process.StandardInput.Close();
        }

        using CancellationTokenSource timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        if (timeout is not null)
        {
            timeoutCts.CancelAfter(timeout.Value);
        }

        bool timedOut = false;
        try
        {
            await process.WaitForExitAsync(timeoutCts.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            timedOut = !ct.IsCancellationRequested;
            try
            {
                process.Kill(entireProcessTree: true);
            }
            catch
            {
                // Already exited.
            }

            if (!timedOut)
            {
                throw;
            }
        }

        string stdout = await SafeReadAsync(stdoutTask).ConfigureAwait(false);
        string stderr = await SafeReadAsync(stderrTask).ConfigureAwait(false);
        TimeSpan duration = Stopwatch.GetElapsedTime(startTimestamp);

        if (timedOut)
        {
            throw new PythonProcessException(
                $"'{Path.GetFileName(executable)}' exceeded the {timeout!.Value.TotalSeconds:0}s timeout and was killed.",
                new PythonResult(-1, stdout, stderr, duration),
                PythonErrorKind.Timeout);
        }

        return new PythonResult(process.ExitCode, stdout, stderr, duration);
    }

    private static async Task<string> SafeReadAsync(Task<string> task)
    {
        try
        {
            return await task.ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            return string.Empty;
        }
    }
}
