namespace PythonEmbedded.Net.OLD.Models;

/// <summary>
/// Represents information about an installed Python package.
/// </summary>
public record PackageInfo(string Name, string Version, string? Location = null, string? Summary = null);

/// <summary>
/// Represents information about an outdated package (available update).
/// </summary>
public record OutdatedPackageInfo(string Name, string InstalledVersion, string LatestVersion);

/// <summary>
/// Represents the status of a package requirement.
/// </summary>
/// <param name="PackageSpecification">The original requirement string.</param>
/// <param name="IsInstalled">Whether the package is installed at all.</param>
/// <param name="MeetsRequirement">Whether the installed version meets the requirement.</param>
/// <param name="InstalledVersion">The currently installed version, if any.</param>
/// <param name="RequiredVersion">The version or range required.</param>
public record RequirementStatus(
    string PackageSpecification,
    bool IsInstalled,
    bool MeetsRequirement,
    string? InstalledVersion = null,
    string? RequiredVersion = null);

/// <summary>
/// Represents pip configuration information.
/// </summary>
public record PipConfiguration(string? IndexUrl = null, string? TrustedHost = null, string? Proxy = null);

