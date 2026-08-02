namespace PythonEmbedded.Net.Models;

/// <summary>A package installed in an environment for which a newer version is available.</summary>
/// <param name="Name">Package name.</param>
/// <param name="CurrentVersion">Currently installed version.</param>
/// <param name="LatestVersion">Latest available version, when the installer could resolve it.</param>
public sealed record OutdatedPackage(string Name, string CurrentVersion, string? LatestVersion);
