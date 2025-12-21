namespace PythonEmbedded.Net.Models;

/// <summary>
/// Represents information about an installed Python package.
/// </summary>
public record PackageInfo(string Name, string Version, string? Location = null, string? Summary = null);

/// <summary>
/// Represents information about an outdated package (available update).
/// </summary>
public record OutdatedPackageInfo(string Name, string InstalledVersion, string LatestVersion);

/// <summary>
/// Represents pip configuration information.
/// </summary>
public record PipConfiguration(string? IndexUrl = null, string? TrustedHost = null, string? Proxy = null);

