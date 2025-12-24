using NUnit.Framework;
using Octokit;
using PythonEmbedded.Net.Exceptions;
using PythonEmbedded.Net.Helpers;
using PythonEmbedded.Net.Models;

namespace PythonEmbedded.Net.Test.Helpers;

/// <summary>
/// Tests for version matching logic in GitHubReleaseHelper, specifically partial version support.
/// </summary>
[TestFixture]
public class GitHubReleaseHelperVersionMatchingTests
{
    [Test]
    public void PartialVersionDetection_WithTwoPartVersion_IsDetectedAsPartial()
    {
        // Test that "3.10" is detected as a partial version
        var versionParts = "3.10".Split('.');
        var isPartialVersion = versionParts.Length < 3;
        
        Assert.That(isPartialVersion, Is.True);
    }

    [Test]
    public void PartialVersionDetection_WithThreePartVersion_IsNotPartial()
    {
        // Test that "3.10.19" is not detected as a partial version
        var versionParts = "3.10.19".Split('.');
        var isPartialVersion = versionParts.Length < 3;
        
        Assert.That(isPartialVersion, Is.False);
    }

    [Test]
    public void VersionParsing_WithPartialVersion_ExtractsMajorMinor()
    {
        // Test that partial version "3.10" correctly extracts major=3, minor=10
        var (major, minor, patch) = VersionParser.ParseVersion("3.10");
        
        Assert.That(major, Is.EqualTo(3));
        Assert.That(minor, Is.EqualTo(10));
        Assert.That(patch, Is.EqualTo(0)); // Patch defaults to 0 for partial versions
    }

    [Test]
    public void AssetVersionMatching_WithPartialVersion_MatchesAnyPatch()
    {
        // Test the logic: when isPartialVersion=true, major.minor match should work
        // This simulates the IsMatchingAsset logic for partial versions
        
        var pythonVersion = "3.10";
        var (major, minor, _) = VersionParser.ParseVersion(pythonVersion);
        var isPartialVersion = pythonVersion.Split('.').Length < 3;
        
        // Simulate asset versions that should match
        var matchingAssets = new[]
        {
            ("cpython-3.10.19-x86_64-pc-windows-msvc-install-only.tar.zst", 3, 10, 19),
            ("cpython-3.10.18-x86_64-pc-windows-msvc-install-only.tar.zst", 3, 10, 18),
            ("cpython-3.10.0-x86_64-pc-windows-msvc-install-only.tar.zst", 3, 10, 0)
        };

        foreach (var (assetName, assetMajor, assetMinor, assetPatch) in matchingAssets)
        {
            bool versionMatches = isPartialVersion
                ? assetMajor == major && assetMinor == minor
                : false; // Would check exact match for full versions
            
            Assert.That(versionMatches, Is.True, $"Asset {assetName} should match partial version {pythonVersion}");
        }

        // Simulate asset versions that should NOT match
        var nonMatchingAssets = new[]
        {
            ("cpython-3.11.0-x86_64-pc-windows-msvc-install-only.tar.zst", 3, 11, 0),
            ("cpython-2.10.0-x86_64-pc-windows-msvc-install-only.tar.zst", 2, 10, 0)
        };

        foreach (var (assetName, assetMajor, assetMinor, assetPatch) in nonMatchingAssets)
        {
            bool versionMatches = isPartialVersion
                ? assetMajor == major && assetMinor == minor
                : false;
            
            Assert.That(versionMatches, Is.False, $"Asset {assetName} should NOT match partial version {pythonVersion}");
        }
    }

    [Test]
    public void AssetVersionMatching_WithFullVersion_MatchesExactly()
    {
        // Test the logic: when isPartialVersion=false, exact match required
        var pythonVersion = "3.10.19";
        var (major, minor, patch) = VersionParser.ParseVersion(pythonVersion);
        var isPartialVersion = pythonVersion.Split('.').Length < 3;
        
        // Should match exact version
        var (assetMajor, assetMinor, assetPatch) = (3, 10, 19);
        bool versionMatches = isPartialVersion
            ? assetMajor == major && assetMinor == minor
            : assetMajor == major && assetMinor == minor && assetPatch == patch;
        
        Assert.That(versionMatches, Is.True);
        
        // Should NOT match different patch
        var (assetMajor2, assetMinor2, assetPatch2) = (3, 10, 18);
        bool versionMatches2 = isPartialVersion
            ? assetMajor2 == major && assetMinor2 == minor
            : assetMajor2 == major && assetMinor2 == minor && assetPatch2 == patch;
        
        Assert.That(versionMatches2, Is.False);
    }

    [Test]
    public void LatestPatchSelection_WithMultiplePatches_SelectsLatest()
    {
        // Test that when multiple patch versions exist, the latest is selected
        var versions = new[]
        {
            "3.10.15",
            "3.10.19",
            "3.10.12",
            "3.10.20"
        };

        var parsedVersions = versions.Select(v => VersionParser.ParseVersion(v)).ToList();
        var sortedVersions = parsedVersions
            .OrderByDescending(v => v.Major)
            .ThenByDescending(v => v.Minor)
            .ThenByDescending(v => v.Patch)
            .ToList();

        var latest = sortedVersions.First();
        
        Assert.That(latest.Major, Is.EqualTo(3));
        Assert.That(latest.Minor, Is.EqualTo(10));
        Assert.That(latest.Patch, Is.EqualTo(20)); // Latest patch
    }
}

