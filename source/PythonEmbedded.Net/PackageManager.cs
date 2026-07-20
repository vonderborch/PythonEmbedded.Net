namespace PythonEmbedded.Net;

/// <summary>Package operations for one environment, delegating to the configured <see cref="IPackageInstaller"/>.</summary>
public sealed class PackageManager
{
    private readonly PythonVirtualEnvironment _env;
    private readonly PythonHost _host;

    internal PackageManager(PythonVirtualEnvironment env, PythonHost host)
    {
        _env = env;
        _host = host;
    }

    /// <summary>Installs a package, e.g. <c>"requests"</c> or <c>"requests==2.31"</c>.</summary>
    public Task InstallAsync(string package, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(package);
        return InstallAsync(new PackageRequest { Packages = [package] }, ct);
    }

    /// <summary>Installs from a full request (multiple packages, requirements file, index URL, extra args).</summary>
    public Task InstallAsync(PackageRequest request, CancellationToken ct = default)
        => _host.Options.Installer.InstallAsync(_env, request, ct);

    /// <summary>Uninstalls a package.</summary>
    public Task UninstallAsync(string package, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(package);
        return _host.Options.Installer.UninstallAsync(_env, package, ct);
    }

    /// <summary>Lists installed packages.</summary>
    public Task<IReadOnlyList<InstalledPackage>> ListAsync(CancellationToken ct = default)
        => _host.Options.Installer.ListAsync(_env, ct);

    /// <inheritdoc cref="InstallAsync(string, CancellationToken)"/>
    public void Install(string package) => InstallAsync(package).GetAwaiter().GetResult();

    /// <inheritdoc cref="InstallAsync(PackageRequest, CancellationToken)"/>
    public void Install(PackageRequest request) => InstallAsync(request).GetAwaiter().GetResult();

    /// <inheritdoc cref="UninstallAsync"/>
    public void Uninstall(string package) => UninstallAsync(package).GetAwaiter().GetResult();

    /// <inheritdoc cref="ListAsync"/>
    public IReadOnlyList<InstalledPackage> List() => ListAsync().GetAwaiter().GetResult();
}
