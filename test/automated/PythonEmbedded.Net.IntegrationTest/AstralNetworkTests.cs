using PythonEmbedded.Net;
using PythonEmbedded.Net.Internals;
using PythonEmbedded.Net.Models;
using PythonEmbedded.Net.Test;

namespace PythonEmbedded.Net.IntegrationTest;

/// <summary>Tests against the real astral-sh/python-build-standalone GitHub releases.</summary>
[TestFixture]
[Category("RequiresNetwork")]
public class AstralNetworkTests
{
    [Test]
    public async Task Magic_Path_Downloads_Installs_And_Runs_Real_Python()
    {
        using TempRoot root = new();
        PythonOptions options = new() { RootDirectory = root.Path };
        // Default sources: bundled (empty here) then astral — the zero-config path.
        PythonHost host = new(options);

        PythonVirtualEnvironment env = await host.GetEnvironmentAsync("3.13", "default", CancellationToken.None);
        PythonResult result = await env.RunCodeAsync("import sys; print(sys.version_info[:2])");

        Assert.Multiple(() =>
        {
            Assert.That(env.Installation.SourceName, Is.EqualTo("astral"));
            Assert.That(result.StandardOutput.Trim(), Is.EqualTo("(3, 13)"));
        });
    }
}
