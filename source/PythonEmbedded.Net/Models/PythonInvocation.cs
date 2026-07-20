using PythonEmbedded.Net.Extensibility;

namespace PythonEmbedded.Net.Models;

/// <summary>Describes what to run; an <see cref="IPythonRunner"/> decides how.</summary>
public sealed record PythonInvocation(InvocationKind Kind, string Target, string[] Args, RunOptions Options);
