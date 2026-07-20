using System.Reflection;
using Microsoft.Extensions.Logging;

namespace PythonEmbedded.Net;

/// <summary>
/// Configuration for the library, set once at startup via <see cref="PythonEnvironment.Configure"/>
/// (or passed to an internal host in tests). Defaults are chosen so no configuration is needed.
/// </summary>
public sealed class PythonOptions
{
    /// <summary>Creates options with all defaults.</summary>
    public PythonOptions()
    {
        Sources =
        [
            new DirectorySource("bundled", Path.Combine(AppContext.BaseDirectory, "python-embedded-runtimes")),
            new AstralSource(),
        ];
        Installer = new PipInstaller();
        Runner = new ProcessRunner();
        RootDirectory = DefaultRootDirectory();
        GitHubToken = System.Environment.GetEnvironmentVariable("GITHUB_TOKEN");
    }

    /// <summary>
    /// Where installations, environments, and caches live. Defaults to an app-local directory
    /// derived from the entry assembly name under the user's local application data folder.
    /// </summary>
    public string RootDirectory { get; set; }

    /// <summary>
    /// Where Python installations come from, tried in order. Defaults to bundled runtime-package
    /// archives, then downloads from astral-sh/python-build-standalone.
    /// </summary>
    public IList<IPythonSource> Sources { get; }

    /// <summary>How environments are created and packages managed. Defaults to venv + pip.</summary>
    public IPackageInstaller Installer { get; set; }

    /// <summary>How Python code executes. Defaults to a buffered subprocess.</summary>
    public IPythonRunner Runner { get; set; }

    /// <summary>GitHub API token used for release lookups; defaults to the <c>GITHUB_TOKEN</c> environment variable.</summary>
    public string? GitHubToken { get; set; }

    /// <summary>How long cached release metadata stays fresh before being re-checked. Default 24 hours.</summary>
    public TimeSpan ReleaseCacheTtl { get; set; } = TimeSpan.FromHours(24);

    /// <summary>When true, no network access is performed; only bundled/cached data is used.</summary>
    public bool Offline { get; set; }

    /// <summary>Optional logger for diagnostics.</summary>
    public ILogger? Logger { get; set; }

    /// <summary>Optional HTTP client override (proxies, custom handlers).</summary>
    public HttpClient? HttpClient { get; set; }

    /// <summary>How long to wait for another process holding an install/environment lock. Default 10 minutes.</summary>
    public TimeSpan LockTimeout { get; set; } = TimeSpan.FromMinutes(10);

    /// <summary>
    /// Adds a local directory containing python-build-standalone <c>install_only</c> archives as the
    /// highest-priority installation source.
    /// </summary>
    public PythonOptions AddDirectorySource(string directory, string name = "directory")
    {
        Sources.Insert(0, new DirectorySource(name, directory));
        return this;
    }

    private static string DefaultRootDirectory()
    {
        string appName = Assembly.GetEntryAssembly()?.GetName().Name ?? "PythonEmbedded.Net";
        return Path.Combine(
            System.Environment.GetFolderPath(System.Environment.SpecialFolder.LocalApplicationData),
            appName,
            "python-embedded");
    }
}
