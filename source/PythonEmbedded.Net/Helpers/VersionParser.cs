using System.Text.RegularExpressions;
using PythonEmbedded.Net.Exceptions;

namespace PythonEmbedded.Net.Helpers;

/// <summary>
/// Utility class for parsing and comparing Python version strings.
/// </summary>
internal static class VersionParser
{
    private static readonly Regex VersionRegex = new(
        @"^(\d+)\.(\d+)(?:\.(\d+))?(?:([a-zA-Z]+)(\d+)?)?$",
        RegexOptions.Compiled);

    /// <summary>
    /// Parses a Python version string into major, minor, and patch components.
    /// </summary>
    /// <param name="versionString">The version string to parse (e.g., "3.12.0", "3.12", "3.12.0rc1").</param>
    /// <returns>A tuple containing (major, minor, patch) version numbers.</returns>
    /// <exception cref="InvalidPythonVersionException">Thrown when the version string is invalid.</exception>
    public static (int Major, int Minor, int Patch) ParseVersion(string versionString)
    {
        if (string.IsNullOrWhiteSpace(versionString))
        {
            throw new InvalidPythonVersionException(
                "Version string cannot be null or empty.")
            {
                InvalidVersion = versionString
            };
        }

        var match = VersionRegex.Match(versionString.Trim());
        if (!match.Success)
        {
            throw new InvalidPythonVersionException(
                $"Invalid version format: {versionString}. Expected format: major.minor.patch (e.g., 3.12.0)")
            {
                InvalidVersion = versionString
            };
        }

        var major = int.Parse(match.Groups[1].Value);
        var minor = int.Parse(match.Groups[2].Value);
        var patch = match.Groups[3].Success ? int.Parse(match.Groups[3].Value) : 0;

        return (major, minor, patch);
    }

    /// <summary>
    /// Compares two version strings.
    /// </summary>
    /// <param name="version1">The first version string.</param>
    /// <param name="version2">The second version string.</param>
    /// <returns>
    /// A value less than zero if version1 is less than version2,
    /// zero if version1 equals version2,
    /// or a value greater than zero if version1 is greater than version2.
    /// </returns>
    public static int CompareVersions(string version1, string version2)
    {
        var v1 = ParseVersion(version1);
        var v2 = ParseVersion(version2);

        var majorComparison = v1.Major.CompareTo(v2.Major);
        if (majorComparison != 0)
            return majorComparison;

        var minorComparison = v1.Minor.CompareTo(v2.Minor);
        if (minorComparison != 0)
            return minorComparison;

        return v1.Patch.CompareTo(v2.Patch);
    }

    /// <summary>
    /// Checks if version1 is greater than version2.
    /// </summary>
    public static bool IsGreaterThan(string version1, string version2)
    {
        return CompareVersions(version1, version2) > 0;
    }

    /// <summary>
    /// Checks if version1 is less than version2.
    /// </summary>
    public static bool IsLessThan(string version1, string version2)
    {
        return CompareVersions(version1, version2) < 0;
    }

    /// <summary>
    /// Checks if version1 equals version2.
    /// </summary>
    public static bool IsEqual(string version1, string version2)
    {
        return CompareVersions(version1, version2) == 0;
    }

    /// <summary>
    /// Checks if a version string matches a partial version (e.g., "3.12" matches "3.12.0", "3.12.1", etc.).
    /// </summary>
    /// <param name="fullVersion">The full version string (e.g., "3.12.0").</param>
    /// <param name="partialVersion">The partial version string (e.g., "3.12").</param>
    /// <returns>True if the full version matches the partial version.</returns>
    public static bool MatchesPartialVersion(string fullVersion, string partialVersion)
    {
        var full = ParseVersion(fullVersion);
        var partial = ParseVersion(partialVersion);

        // For partial versions, patch is 0, so we only compare major and minor
        return full.Major == partial.Major && full.Minor == partial.Minor;
    }

    /// <summary>
    /// Normalizes a version string to the format "major.minor.patch".
    /// </summary>
    /// <param name="versionString">The version string to normalize.</param>
    /// <returns>A normalized version string (e.g., "3.12" -> "3.12.0").</returns>
    public static string NormalizeVersion(string versionString)
    {
        var (major, minor, patch) = ParseVersion(versionString);
        return $"{major}.{minor}.{patch}";
    }
}
