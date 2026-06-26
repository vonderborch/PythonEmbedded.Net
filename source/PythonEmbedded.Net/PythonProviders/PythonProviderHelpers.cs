namespace PythonEmbedded.Net.PythonProviders;

/// <summary>
/// Provides utilities for working with Python-related assets and managing version compatibility.
/// </summary>
public static class PythonProviderHelpers
{
    /// <summary>
    /// Determines whether the specified asset version matches the target version based on their component values.
    /// </summary>
    /// <param name="assetVersion">The version of the asset being evaluated.</param>
    /// <param name="targetVersion">The target version to compare against.</param>
    /// <returns>
    /// A boolean value indicating whether the asset version matches the target version.
    /// Returns true if the versions match based on major, minor, and optionally build components; otherwise, false.
    /// </returns>
    public static bool IsMatchingVersion(Version assetVersion, Version targetVersion)
    {
        // Major + minor + build
        if (targetVersion.Minor != -1 && targetVersion.Build != -1)
        {
            bool isMatch = assetVersion.Major == targetVersion.Major &&
                          assetVersion.Minor == targetVersion.Minor &&
                          assetVersion.Build == targetVersion.Build;
            return isMatch;
        }
        // Major + minor 
        else if (targetVersion.Minor != -1)
        {
            bool isMatch = assetVersion.Major == targetVersion.Major &&
                           assetVersion.Minor == targetVersion.Minor;
            return isMatch;
        }
        // Major only
        else
        {
            bool isMatch = assetVersion.Major == targetVersion.Major;
            return isMatch;
        }
    }

    /// <summary>
    /// Extracts the version information from the specified asset name if it matches the expected version pattern.
    /// </summary>
    /// <param name="assetName">The name of the asset containing the version information.</param>
    /// <returns>
    /// A <see cref="Version"/> object representing the extracted version if the asset name matches the expected pattern; otherwise, null.
    /// </returns>
    public static Version? GetVersionFromAssetName(string assetName)
    {
        string name = assetName.ToLowerInvariant();
        
        var versionMatch = System.Text.RegularExpressions.Regex.Match(
            name,
            @"(?:cpython|python)-(\d+)\.(\d+)\.(\d+)");
        if (!versionMatch.Success)
        {
            return null;
        }
        
        var assetMajor = int.Parse(versionMatch.Groups[1].Value);
        var assetMinor = int.Parse(versionMatch.Groups[2].Value);
        var assetPatch = int.Parse(versionMatch.Groups[3].Value);
        
        Version version = new(assetMajor, assetMinor, assetPatch);
        return version;
    }

    /// <summary>
    /// Determines whether a given asset name matches the specified platform target triple.
    /// </summary>
    /// <param name="assetName">The name of the asset to check.</param>
    /// <param name="targetTriple">The target platform triple to match against.</param>
    /// <returns>True if the asset name matches the target platform triple; otherwise, false.</returns>
    public static bool IsMatchingPlatform(string assetName, string targetTriple)
    {
        bool containsPlatformTarget = assetName.Contains(targetTriple);
        return containsPlatformTarget;
    }
}
