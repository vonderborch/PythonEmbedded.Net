using Microsoft.Extensions.Logging;

namespace PythonEmbedded.Net.Extensibility;

/// <summary>What a tool provisioning callback gets to work with.</summary>
public sealed class ToolContext
{
    internal ToolContext(PythonInstallation installation, string toolsDirectory, SourceContext sources)
    {
        Installation = installation;
        ToolsDirectory = toolsDirectory;
        Sources = sources;
    }

    /// <summary>The installation the tool is being provisioned for (pip-install into its base interpreter, etc.).</summary>
    public PythonInstallation Installation { get; }

    /// <summary>The runtime-local tools directory (<c>&lt;root&gt;/tools</c>); place downloaded binaries here.</summary>
    public string ToolsDirectory { get; }

    /// <summary>Download/caching plumbing (HTTP, checksum-verified downloads).</summary>
    public SourceContext Sources { get; }

    /// <summary>Logger for diagnostics.</summary>
    public ILogger Logger => Sources.Logger;
}
