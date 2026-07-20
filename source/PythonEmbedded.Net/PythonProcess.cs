using System.Diagnostics;

namespace PythonEmbedded.Net;

/// <summary>
/// A live, long-running Python process (server, worker, supervised loop): streamed output lines,
/// writable stdin, kill support. Disposing kills the process if it is still running.
/// Subscribe to <see cref="OutputLine"/>/<see cref="ErrorLine"/> promptly — lines emitted before
/// a handler is attached are not replayed.
/// </summary>
public sealed class PythonProcess : IAsyncDisposable
{
    private readonly Process _process;

    private PythonProcess(Process process)
    {
        _process = process;
    }

    internal static PythonProcess Start(PythonVirtualEnvironment env, PythonInvocation invocation)
    {
        ProcessStartInfo startInfo = new(env.PythonExecutable)
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            RedirectStandardInput = true,
            UseShellExecute = false,
            CreateNoWindow = true,
            WorkingDirectory = invocation.Options.WorkingDirectory ?? string.Empty,
        };

        startInfo.ArgumentList.Add(invocation.Target);
        foreach (string arg in invocation.Args)
        {
            startInfo.ArgumentList.Add(arg);
        }

        // Line streaming is useless if Python buffers; force unbuffered stdio.
        startInfo.Environment["PYTHONUNBUFFERED"] = "1";
        if (!env.IsBase)
        {
            startInfo.Environment["VIRTUAL_ENV"] = env.Directory;
            string binDir = Path.GetDirectoryName(env.PythonExecutable)!;
            startInfo.Environment["PATH"] = binDir + Path.PathSeparator
                + (System.Environment.GetEnvironmentVariable("PATH") ?? string.Empty);
        }

        if (invocation.Options.Environment is not null)
        {
            foreach ((string key, string value) in invocation.Options.Environment)
            {
                startInfo.Environment[key] = value;
            }
        }

        Process process = new() { StartInfo = startInfo, EnableRaisingEvents = true };
        PythonProcess handle = new(process);
        process.OutputDataReceived += (_, e) =>
        {
            if (e.Data is not null)
            {
                handle.OutputLine?.Invoke(e.Data);
            }
        };
        process.ErrorDataReceived += (_, e) =>
        {
            if (e.Data is not null)
            {
                handle.ErrorLine?.Invoke(e.Data);
            }
        };

        try
        {
            process.Start();
        }
        catch (Exception ex)
        {
            process.Dispose();
            throw new PythonException(
                PythonErrorKind.ExecutionFailed, $"Failed to start '{env.PythonExecutable}': {ex.Message}", ex);
        }

        process.BeginOutputReadLine();
        process.BeginErrorReadLine();
        return handle;
    }

    /// <summary>Raised for each line the process writes to stdout.</summary>
    public event Action<string>? OutputLine;

    /// <summary>Raised for each line the process writes to stderr.</summary>
    public event Action<string>? ErrorLine;

    /// <summary>The process's standard input.</summary>
    public StreamWriter StandardInput => _process.StandardInput;

    /// <summary>The OS process id.</summary>
    public int Id => _process.Id;

    /// <summary>Whether the process has exited.</summary>
    public bool HasExited => _process.HasExited;

    /// <summary>Waits for the process to exit and returns its exit code.</summary>
    public async Task<int> WaitForExitAsync(CancellationToken ct = default)
    {
        await _process.WaitForExitAsync(ct).ConfigureAwait(false);
        return _process.ExitCode;
    }

    /// <inheritdoc cref="WaitForExitAsync"/>
    public int WaitForExit() => WaitForExitAsync().GetAwaiter().GetResult();

    /// <summary>Kills the process (and its children) if it is still running.</summary>
    public void Kill()
    {
        try
        {
            if (!_process.HasExited)
            {
                _process.Kill(entireProcessTree: true);
            }
        }
        catch (InvalidOperationException)
        {
            // Exited between the check and the kill.
        }
    }

    /// <summary>Kills the process if still running and releases resources.</summary>
    public async ValueTask DisposeAsync()
    {
        Kill();
        try
        {
            await _process.WaitForExitAsync().ConfigureAwait(false);
        }
        catch (InvalidOperationException)
        {
            // Never started or already reaped.
        }

        _process.Dispose();
    }
}
