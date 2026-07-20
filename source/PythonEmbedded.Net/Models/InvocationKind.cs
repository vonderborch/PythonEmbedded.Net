namespace PythonEmbedded.Net;

/// <summary>What kind of thing a <see cref="PythonInvocation"/> targets.</summary>
public enum InvocationKind
{
    /// <summary>A script file path (<c>python script.py</c>).</summary>
    Script,

    /// <summary>Inline code (<c>python -c "..."</c>).</summary>
    Code,

    /// <summary>A module (<c>python -m module</c>).</summary>
    Module,
}
