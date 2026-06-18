namespace PythonEmbedded.Net;

public abstract class PythonRuntime
{
    protected internal abstract PythonRuntime GetOrCreateRuntime();
}