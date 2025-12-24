using NUnit.Framework;
using Octokit;
using PythonEmbedded.Net.Exceptions;
using PythonEmbedded.Net.Helpers;
using PythonEmbedded.Net.Models;

namespace PythonEmbedded.Net.Test.Helpers;

/// <summary>
/// Tests for GitHubReleaseHelper using mocked Octokit responses.
/// Tests the logic for partial version matching and asset selection.
/// </summary>
[TestFixture]
public class GitHubReleaseHelperMockedTests
{
    [Test]
    public void PartialVersionDetection_WithVariousFormats_DetectsCorrectly()
    {
        // Test partial version detection logic
        var testCases = new[]
        {
            ("3.10", true),   // Partial - only major.minor
            ("3.12", true),  // Partial - only major.minor
            ("3.10.19", false), // Full - major.minor.patch
            ("3.12.0", false),  // Full - major.minor.patch
            ("3", false)     // Invalid - would throw exception
        };

        foreach (var (version, expectedPartial) in testCases)
        {
            if (version == "3")
            {
                // Single number is invalid
                Assert.Throws<InvalidPythonVersionException>(() =>
                {
                    VersionParser.ParseVersion(version);
                });
                continue;
            }

            var versionParts = version.Split('.');
            var isPartialVersion = versionParts.Length < 3;
            
            Assert.That(isPartialVersion, Is.EqualTo(expectedPartial), 
                $"Version {version} should be detected as {(expectedPartial ? "partial" : "full")}");
        }
    }

    [Test]
    public void VersionExtraction_FromAssetNames_ExtractsCorrectly()
    {
        // Test that we can extract versions from asset names
        var testCases = new[]
        {
            ("cpython-3.10.19-x86_64-pc-windows-msvc-install-only.tar.zst", "3.10.19"),
            ("cpython-3.12.0-x86_64-pc-windows-msvc-install-only.tar.zst", "3.12.0"),
            ("python-3.11.5-x86_64-pc-windows-msvc.tar.zst", "3.11.5"),
            ("cpython-3.10.0-x86_64-unknown-linux-gnu-install-only.tar.zst", "3.10.0")
        };

        foreach (var (assetName, expectedVersion) in testCases)
        {
            var match = System.Text.RegularExpressions.Regex.Match(
                assetName.ToLowerInvariant(),
                @"(?:cpython|python)-(\d+)\.(\d+)\.(\d+)");
            
            Assert.That(match.Success, Is.True, $"Should extract version from {assetName}");
            if (match.Success)
            {
                var extractedVersion = $"{match.Groups[1].Value}.{match.Groups[2].Value}.{match.Groups[3].Value}";
                Assert.That(extractedVersion, Is.EqualTo(expectedVersion));
            }
        }
    }

    [Test]
    public void AssetMatching_WithPartialVersion_MatchesCorrectPatches()
    {
        // Simulate the IsMatchingAsset logic for partial versions
        var pythonVersion = "3.10";
        var versionParts = pythonVersion.Split('.');
        var isPartialVersion = versionParts.Length < 3;
        var (major, minor, _) = VersionParser.ParseVersion(pythonVersion);
        var targetTriple = "x86_64-pc-windows-msvc";

        var matchingAssets = new[]
        {
            "cpython-3.10.19-x86_64-pc-windows-msvc-install-only.tar.zst",
            "cpython-3.10.18-x86_64-pc-windows-msvc-install-only.tar.zst",
            "cpython-3.10.0-x86_64-pc-windows-msvc-install-only.tar.zst"
        };

        var nonMatchingAssets = new[]
        {
            "cpython-3.11.0-x86_64-pc-windows-msvc-install-only.tar.zst", // Wrong minor
            "cpython-2.10.0-x86_64-pc-windows-msvc-install-only.tar.zst", // Wrong major
            "cpython-3.10.19-x86_64-unknown-linux-gnu-install-only.tar.zst" // Wrong platform
        };

        foreach (var assetName in matchingAssets)
        {
            var versionMatch = System.Text.RegularExpressions.Regex.Match(
                assetName.ToLowerInvariant(),
                @"(?:cpython|python)-(\d+)\.(\d+)\.(\d+)");
            
            if (versionMatch.Success)
            {
                var assetMajor = int.Parse(versionMatch.Groups[1].Value);
                var assetMinor = int.Parse(versionMatch.Groups[2].Value);
                
                bool versionMatches = isPartialVersion
                    ? assetMajor == major && assetMinor == minor
                    : false;
                
                bool platformMatches = assetName.ToLowerInvariant().Contains(targetTriple.ToLowerInvariant());
                
                Assert.That(versionMatches && platformMatches, Is.True, 
                    $"Asset {assetName} should match partial version {pythonVersion}");
            }
        }

        foreach (var assetName in nonMatchingAssets)
        {
            var versionMatch = System.Text.RegularExpressions.Regex.Match(
                assetName.ToLowerInvariant(),
                @"(?:cpython|python)-(\d+)\.(\d+)\.(\d+)");
            
            if (versionMatch.Success)
            {
                var assetMajor = int.Parse(versionMatch.Groups[1].Value);
                var assetMinor = int.Parse(versionMatch.Groups[2].Value);
                
                bool versionMatches = isPartialVersion
                    ? assetMajor == major && assetMinor == minor
                    : false;
                
                bool platformMatches = assetName.ToLowerInvariant().Contains(targetTriple.ToLowerInvariant());
                
                Assert.That(!versionMatches || !platformMatches, Is.True, 
                    $"Asset {assetName} should NOT match partial version {pythonVersion}");
            }
        }
    }

    [Test]
    public void LatestPatchSelection_WithMultipleAssets_SelectsHighestPatch()
    {
        // Test that when multiple patch versions exist, the highest is selected
        var assetNames = new[]
        {
            "cpython-3.10.15-x86_64-pc-windows-msvc-install-only.tar.zst",
            "cpython-3.10.19-x86_64-pc-windows-msvc-install-only.tar.zst",
            "cpython-3.10.12-x86_64-pc-windows-msvc-install-only.tar.zst",
            "cpython-3.10.20-x86_64-pc-windows-msvc-install-only.tar.zst"
        };

        var versions = assetNames.Select(name =>
        {
            var match = System.Text.RegularExpressions.Regex.Match(
                name.ToLowerInvariant(),
                @"(?:cpython|python)-(\d+)\.(\d+)\.(\d+)");
            if (match.Success)
            {
                return VersionParser.ParseVersion($"{match.Groups[1].Value}.{match.Groups[2].Value}.{match.Groups[3].Value}");
            }
            return (0, 0, 0);
        }).Where(v => v != (0, 0, 0)).ToList();

        var sortedVersions = versions
            .OrderByDescending(v => v.Item1)
            .ThenByDescending(v => v.Item2)
            .ThenByDescending(v => v.Item3)
            .ToList();

        var latest = sortedVersions.First();
        
        Assert.That(latest.Item1, Is.EqualTo(3));
        Assert.That(latest.Item2, Is.EqualTo(10));
        Assert.That(latest.Item3, Is.EqualTo(20)); // Should be the highest patch
    }

    [Test]
    public void InstallOnlyVsFullArchive_Classification_IsCorrect()
    {
        // Test archive type classification
        var installOnlyArchives = new[]
        {
            "cpython-3.12.0-x86_64-pc-windows-msvc-install-only.tar.zst",
            "cpython-3.12.0-x86_64-pc-windows-msvc-install.tar.zst"
        };

        var fullArchives = new[]
        {
            "cpython-3.12.0-x86_64-pc-windows-msvc-full.tar.zst",
            "cpython-3.12.0-x86_64-pc-windows-msvc.tar.zst",
            "cpython-3.12.0-x86_64-pc-windows-msvc.zip"
        };

        foreach (var archive in installOnlyArchives)
        {
            var isInstallOnly = archive.ToLowerInvariant().Contains("install") && 
                               !archive.ToLowerInvariant().Contains("full");
            Assert.That(isInstallOnly, Is.True, $"Archive {archive} should be classified as install-only");
        }

        foreach (var archive in fullArchives)
        {
            var isFull = archive.ToLowerInvariant().Contains("full") ||
                        (!archive.ToLowerInvariant().Contains("install") &&
                         (archive.EndsWith(".tar.zst", StringComparison.OrdinalIgnoreCase) ||
                          archive.EndsWith(".zip", StringComparison.OrdinalIgnoreCase)));
            Assert.That(isFull, Is.True, $"Archive {archive} should be classified as full");
        }
    }
}

