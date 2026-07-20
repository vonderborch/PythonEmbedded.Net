using PythonEmbedded.Net.Extensibility;
using PythonEmbedded.Net.Models;

namespace PythonEmbedded.Net.Runners;

/// <summary>The default <see cref="IPythonRunner"/>: a buffered one-shot subprocess.</summary>
internal sealed class ProcessRunner : PythonRunnerBase
{
    public override Task<PythonResult> RunAsync(PythonVirtualEnvironment env, PythonInvocation invocation, CancellationToken ct)
    {
        List<string> args = invocation.Kind switch
        {
            InvocationKind.Script => [invocation.Target, .. invocation.Args],
            InvocationKind.Code => ["-c", invocation.Target, .. invocation.Args],
            InvocationKind.Module => ["-m", invocation.Target, .. invocation.Args],
            _ => throw new ArgumentOutOfRangeException(nameof(invocation)),
        };

        Dictionary<string, string> environment = new();
        if (!env.IsBase)
        {
            // Mirror venv activation so child tooling resolves the environment correctly.
            environment["VIRTUAL_ENV"] = env.Directory;
            string binDir = Path.GetDirectoryName(env.PythonExecutable)!;
            string currentPath = System.Environment.GetEnvironmentVariable("PATH") ?? string.Empty;
            environment["PATH"] = binDir + Path.PathSeparator + currentPath;
        }

        if (invocation.Options.Environment is not null)
        {
            foreach ((string key, string value) in invocation.Options.Environment)
            {
                environment[key] = value;
            }
        }

        return Subprocess.RunAsync(
            env.PythonExecutable,
            args,
            invocation.Options.WorkingDirectory,
            environment,
            invocation.Options.Stdin,
            invocation.Options.Timeout,
            ct);
    }
}
