using PythonEmbedded.Net.Exceptions;

namespace PythonEmbedded.Net.Models;

/// <summary>Optional settings for a single run.</summary>
public sealed record RunOptions
{
    /// <summary>Working directory for the process; defaults to the current directory.</summary>
    public string? WorkingDirectory { get; init; }

    /// <summary>Extra environment variables for the process.</summary>
    public IReadOnlyDictionary<string, string>? Environment { get; init; }

    /// <summary>Kill the process and throw if it runs longer than this.</summary>
    public TimeSpan? Timeout { get; init; }

    /// <summary>Text written to the process's standard input.</summary>
    public string? Stdin { get; init; }

    /// <summary>
    /// When true (the default), a nonzero exit code throws <see cref="PythonProcessException"/>.
    /// Set false to always receive the <see cref="PythonResult"/>.
    /// </summary>
    public bool ThrowOnError { get; init; } = true;
}
