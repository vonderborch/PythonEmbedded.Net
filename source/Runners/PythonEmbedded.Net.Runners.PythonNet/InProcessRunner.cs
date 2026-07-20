using System.Diagnostics;
using Python.Runtime;
using PythonEmbedded.Net.Extensibility;
using PythonEmbedded.Net.Models;

namespace PythonEmbedded.Net.Runners.PythonNet;

/// <summary>
/// An <see cref="IPythonRunner"/> that executes Python in-process via Python.NET — roughly an
/// order of magnitude faster than a subprocess for short scripts, at the cost of process-lifetime
/// binding to one environment and no mid-run cancellation or timeout (CPython cannot be interrupted
/// from outside). Activate with:
/// <code>PythonEnvironment.Configure(o => o.Runner = new InProcessRunner());</code>
/// </summary>
public sealed class InProcessRunner : PythonRunnerBase
{
    // Mirrors `python -c/-m/script` semantics: argv, __main__, stdio capture, SystemExit → exit code.
    private const string Trampoline = """
        def _pyembed_run(kind, target, args, stdin_text, cwd):
            import sys, io, os, traceback, runpy
            old_argv, old_cwd = sys.argv[:], os.getcwd()
            old_stdout, old_stderr, old_stdin = sys.stdout, sys.stderr, sys.stdin
            out, err = io.StringIO(), io.StringIO()
            sys.stdout, sys.stderr, sys.stdin = out, err, io.StringIO(stdin_text or "")
            exit_code = 0
            try:
                if cwd:
                    os.chdir(cwd)
                if kind == "code":
                    sys.argv = ["-c"] + list(args)
                    exec(compile(target, "<string>", "exec"), {"__name__": "__main__"})
                elif kind == "script":
                    sys.argv = [target] + list(args)
                    with open(target, "r", encoding="utf-8") as f:
                        source = f.read()
                    exec(compile(source, target, "exec"), {"__name__": "__main__", "__file__": target})
                else:
                    sys.argv = [target] + list(args)
                    runpy.run_module(target, run_name="__main__", alter_sys=True)
            except SystemExit as e:
                if e.code is None:
                    exit_code = 0
                elif isinstance(e.code, int):
                    exit_code = e.code
                else:
                    print(e.code, file=sys.stderr)
                    exit_code = 1
            except BaseException:
                traceback.print_exc()
                exit_code = 1
            finally:
                os.chdir(old_cwd)
                sys.argv = old_argv
                sys.stdout, sys.stderr, sys.stdin = old_stdout, old_stderr, old_stdin
            return exit_code, out.getvalue(), err.getvalue()
        """;

    /// <inheritdoc />
    public override Task<PythonResult> RunAsync(PythonVirtualEnvironment env, PythonInvocation invocation, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        PythonNetHost.Initialize(env);

        // Python runs on a worker thread so async callers aren't blocked, but it cannot be
        // cancelled once started — ct is only observed before execution begins.
        return Task.Run(() =>
        {
            long startTimestamp = Stopwatch.GetTimestamp();
            return PythonNetHost.RunInScope(scope =>
            {
                scope.Exec(Trampoline);

                string kind = invocation.Kind switch
                {
                    InvocationKind.Code => "code",
                    InvocationKind.Script => "script",
                    _ => "module",
                };

                using PyObject runner = scope.Get("_pyembed_run");
                using PyObject result = runner.Invoke(
                    kind.ToPython(),
                    invocation.Target.ToPython(),
                    invocation.Args.ToPython(),
                    (invocation.Options.Stdin ?? string.Empty).ToPython(),
                    (invocation.Options.WorkingDirectory ?? string.Empty).ToPython());

                return new PythonResult(
                    result[0].As<int>(),
                    result[1].As<string>(),
                    result[2].As<string>(),
                    Stopwatch.GetElapsedTime(startTimestamp));
            });
        }, ct);
    }
}
