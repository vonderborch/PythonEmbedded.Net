using System.Diagnostics;
using System.Text;
using Microsoft.Extensions.Logging;
using PythonEmbedded.Net.Services;

namespace PythonEmbedded.Net.Services;

/// <summary>
/// Default implementation of IProcessExecutor for executing external processes.
/// </summary>
public class ProcessExecutor : IProcessExecutor
{
    private readonly ILogger<ProcessExecutor>? _logger;

    /// <summary>
    /// Initializes a new instance of the ProcessExecutor class.
    /// </summary>
    /// <param name="logger">Optional logger for this executor.</param>
    public ProcessExecutor(ILogger<ProcessExecutor>? logger = null)
    {
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<ProcessExecutionResult> ExecuteAsync(
        ProcessStartInfo startInfo,
        Func<string?>? stdinHandler = null,
        Action<string>? stdoutHandler = null,
        Action<string>? stderrHandler = null,
        CancellationToken cancellationToken = default)
    {
        var outputBuilder = StringBuilderPool.Get();
        var errorBuilder = StringBuilderPool.Get();

        using var process = new Process { StartInfo = startInfo };

        // Setup output/error handlers
        if (startInfo.RedirectStandardOutput || true) // Always capture for result
        {
            process.OutputDataReceived += (sender, e) =>
            {
                if (e.Data != null)
                {
                    outputBuilder.AppendLine(e.Data);
                    stdoutHandler?.Invoke(e.Data);
                }
            };
        }

        if (startInfo.RedirectStandardError || true) // Always capture for result
        {
            process.ErrorDataReceived += (sender, e) =>
            {
                if (e.Data != null)
                {
                    errorBuilder.AppendLine(e.Data);
                    stderrHandler?.Invoke(e.Data);
                }
            };
        }

        process.Start();

        // Begin asynchronous read operations
        if (startInfo.RedirectStandardOutput)
        {
            process.BeginOutputReadLine();
        }

        if (startInfo.RedirectStandardError)
        {
            process.BeginErrorReadLine();
        }

        // Handle stdin if provided
        if (stdinHandler != null && startInfo.RedirectStandardInput)
        {
            await Task.Run(async () =>
            {
                try
                {
                    using var writer = process.StandardInput;
                    string? input;
                    while ((input = stdinHandler()) != null && !cancellationToken.IsCancellationRequested)
                    {
                        await writer.WriteLineAsync(input).ConfigureAwait(false);
                    }
                    writer.Close();
                }
                catch (Exception ex)
                {
                    _logger?.LogWarning(ex, "Error writing to stdin");
                }
            }, cancellationToken).ConfigureAwait(false);
        }

        // Register cancellation handler
        var cancellationRegistration = cancellationToken.Register(() =>
        {
            try
            {
                if (!process.HasExited)
                    process.Kill();
            }
            catch { }
        });

        try
        {
            await process.WaitForExitAsync(cancellationToken).ConfigureAwait(false);
            
            // Wait for async read operations to complete
            if (startInfo.RedirectStandardOutput)
            {
                process.CancelOutputRead();
            }

            if (startInfo.RedirectStandardError)
            {
                process.CancelErrorRead();
            }

            var result = new ProcessExecutionResult(
                process.ExitCode,
                outputBuilder.ToString().TrimEnd(),
                errorBuilder.ToString().TrimEnd());

            if (result.ExitCode != 0)
            {
                _logger?.LogWarning(
                    "Process exited with code {ExitCode}. StdErr: {StdErr}",
                    result.ExitCode,
                    result.StandardError);
            }

            return result;
        }
        finally
        {
            // Return StringBuilder instances to pool
            StringBuilderPool.Return(outputBuilder);
            StringBuilderPool.Return(errorBuilder);
            await cancellationRegistration.DisposeAsync().ConfigureAwait(false);
        }
    }
}

