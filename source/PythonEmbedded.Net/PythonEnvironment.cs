using PythonEmbedded.Net.Extensibility;
using PythonEmbedded.Net.Internals;

namespace PythonEmbedded.Net;

/// <summary>
/// The entry point. The happy path is one line:
/// <code>
/// var env = await PythonEnvironment.GetEnvironmentAsync("3.13", "myapp");
/// await env.Packages.InstallAsync("requests");
/// var result = await env.RunAsync("script.py");
/// </code>
/// Environment names are always explicit — there is no default — so the same Python version can
/// back multiple independent virtual environments. Optional configuration happens once at startup
/// via <see cref="Configure"/>.
/// </summary>
public static class PythonEnvironment
{
    private static readonly Lock SyncRoot = new();
    private static PythonOptions _options = new();
    private static PythonHost? _host;

    /// <summary>
    /// Configures the library's <i>defaults</i> — <see cref="PythonOptions.Installer"/> and
    /// <see cref="PythonOptions.Runner"/> apply to every environment unless overridden per-environment via
    /// the <c>installer</c>/<c>runner</c> parameters on <see cref="GetEnvironmentAsync"/> and
    /// <see cref="PythonInstallation.GetEnvironmentAsync"/>. An overridden installer is recorded and fixed
    /// for that environment's lifetime (it materially affects how the environment was built on disk); an
    /// overridden runner is not recorded — it's a pure execution-time concern and can differ on every fetch.
    /// Must be called before any other member; throws <see cref="InvalidOperationException"/> once an
    /// installation or environment has been requested.
    /// </summary>
    public static void Configure(Action<PythonOptions> configure)
    {
        ArgumentNullException.ThrowIfNull(configure);
        lock (SyncRoot)
        {
            if (_host is not null)
            {
                throw new InvalidOperationException(
                    "PythonEnvironment.Configure must be called before the first use of the library.");
            }

            configure(_options);
        }
    }

    /// <summary>
    /// Gets (installing and creating on first use) a named virtual environment for the requested
    /// version. <paramref name="version"/> accepts <c>"latest"</c>, <c>"3"</c>, <c>"3.13"</c>, or
    /// <c>"3.13.2"</c>. <paramref name="name"/> is required — the same version can back multiple
    /// independent environments, each identified by its own name. <paramref name="installer"/> and
    /// <paramref name="runner"/> override the <see cref="Configure"/>d defaults for this environment;
    /// see <see cref="Configure"/> for how each is (or isn't) recorded.
    /// </summary>
    public static Task<PythonVirtualEnvironment> GetEnvironmentAsync(
        string version, string name, CancellationToken ct = default,
        IPackageInstaller? installer = null, IPythonRunner? runner = null)
        => Host.GetEnvironmentAsync(version, name, ct, installer, runner);

    /// <summary>Gets (installing on first use) a Python installation for the requested version.</summary>
    public static Task<PythonInstallation> GetInstallationAsync(string version, CancellationToken ct = default)
        => Host.GetInstallationAsync(version, ct);

    /// <summary>Lists all installations under the configured root directory.</summary>
    public static Task<IReadOnlyList<PythonInstallation>> ListInstallationsAsync(CancellationToken ct = default)
        => Host.ListInstallationsAsync(ct);

    /// <summary>Removes an installation and all of its environments from disk.</summary>
    public static Task RemoveAsync(PythonInstallation install, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(install);
        return Host.RemoveAsync(install, ct);
    }

    /// <inheritdoc cref="GetEnvironmentAsync"/>
    public static PythonVirtualEnvironment GetEnvironment(
        string version, string name, IPackageInstaller? installer = null, IPythonRunner? runner = null)
        => GetEnvironmentAsync(version, name, default, installer, runner).GetAwaiter().GetResult();

    /// <inheritdoc cref="GetInstallationAsync"/>
    public static PythonInstallation GetInstallation(string version)
        => GetInstallationAsync(version).GetAwaiter().GetResult();

    /// <inheritdoc cref="ListInstallationsAsync"/>
    public static IReadOnlyList<PythonInstallation> ListInstallations()
        => ListInstallationsAsync().GetAwaiter().GetResult();

    /// <inheritdoc cref="RemoveAsync"/>
    public static void Remove(PythonInstallation install)
        => RemoveAsync(install).GetAwaiter().GetResult();

    private static PythonHost Host
    {
        get
        {
            if (_host is { } host)
            {
                return host;
            }

            lock (SyncRoot)
            {
                return _host ??= new PythonHost(_options);
            }
        }
    }

    /// <summary>Resets all state, including configuration. Tests only.</summary>
    internal static void Reset()
    {
        lock (SyncRoot)
        {
            _host = null;
            _options = new PythonOptions();
        }
    }
}
