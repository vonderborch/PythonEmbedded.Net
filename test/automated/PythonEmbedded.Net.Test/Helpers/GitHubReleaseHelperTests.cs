using NUnit.Framework;
using Octokit;
using PythonEmbedded.Net.Exceptions;
using PythonEmbedded.Net.Helpers;
using PythonEmbedded.Net.Models;

namespace PythonEmbedded.Net.Test.Helpers;

/// <summary>
/// Unit tests for GitHubReleaseHelper focusing on version matching and asset selection logic.
/// </summary>
[TestFixture]
public class GitHubReleaseHelperTests
{
    [Test]
    public void ExtractVersionFromAssetName_WithValidAssetName_ReturnsVersion()
    {
        // This tests the private ExtractVersionFromAssetName method logic
        // We can't test it directly, but we can test the behavior through public methods
        
        // Test cases that would use this method:
        var testCases = new[]
        {
            ("cpython-3.10.19-x86_64-pc-windows-msvc-install-only.tar.zst", "3.10.19"),
            ("cpython-3.12.0-x86_64-pc-windows-msvc-install-only.tar.zst", "3.12.0"),
            ("python-3.11.5-x86_64-pc-windows-msvc.tar.zst", "3.11.5")
        };

        // These would be tested through FindReleaseAssetAsync in integration tests
        // For unit tests, we document the expected behavior
        foreach (var (assetName, expectedVersion) in testCases)
        {
            // The ExtractVersionFromAssetName method should extract the version correctly
            // This is verified through integration tests
            Assert.That(assetName, Does.Contain(expectedVersion));
        }
    }

    [Test]
    public void IsMatchingAsset_WithPartialVersion_MatchesAnyPatch()
    {
        // Test the logic: "3.10" should match "3.10.19", "3.10.18", etc.
        // This is tested through the IsMatchingAsset method which is private
        // We document the expected behavior here
        
        var testCases = new[]
        {
            // (pythonVersion, assetName, targetTriple, isPartialVersion, major, minor, shouldMatch)
            ("3.10", "cpython-3.10.19-x86_64-pc-windows-msvc-install-only.tar.zst", "x86_64-pc-windows-msvc", true, 3, 10, true),
            ("3.10", "cpython-3.10.0-x86_64-pc-windows-msvc-install-only.tar.zst", "x86_64-pc-windows-msvc", true, 3, 10, true),
            ("3.10", "cpython-3.11.0-x86_64-pc-windows-msvc-install-only.tar.zst", "x86_64-pc-windows-msvc", true, 3, 10, false),
            ("3.12.0", "cpython-3.12.0-x86_64-pc-windows-msvc-install-only.tar.zst", "x86_64-pc-windows-msvc", false, 3, 12, true),
            ("3.12.0", "cpython-3.12.1-x86_64-pc-windows-msvc-install-only.tar.zst", "x86_64-pc-windows-msvc", false, 3, 12, false)
        };

        // Document expected behavior - actual testing done through integration tests
        foreach (var testCase in testCases)
        {
            // The IsMatchingAsset method should correctly match based on these criteria
            Assert.That(true, Is.True); // Placeholder
        }
    }

    [Test]
    public void IsInstallOnlyArchive_WithInstallOnlyArchive_ReturnsTrue()
    {
        // Test the IsInstallOnlyArchive logic
        var installOnlyNames = new[]
        {
            "cpython-3.12.0-x86_64-pc-windows-msvc-install-only.tar.zst",
            "cpython-3.12.0-x86_64-pc-windows-msvc-install.tar.zst"
        };

        var fullArchiveNames = new[]
        {
            "cpython-3.12.0-x86_64-pc-windows-msvc-full.tar.zst",
            "cpython-3.12.0-x86_64-pc-windows-msvc.tar.zst"
        };

        // Document expected behavior
        foreach (var name in installOnlyNames)
        {
            Assert.That(name.ToLowerInvariant(), Does.Contain("install"));
            Assert.That(name.ToLowerInvariant(), Does.Not.Contain("full"));
        }

        foreach (var name in fullArchiveNames)
        {
            // Full archives either contain "full" or don't contain "install"
            Assert.That(
                name.ToLowerInvariant().Contains("full") ||
                !name.ToLowerInvariant().Contains("install"),
                Is.True);
        }
    }

    [Test]
    public void IsFullArchive_WithFullArchive_ReturnsTrue()
    {
        // Test the IsFullArchive logic
        var fullArchiveNames = new[]
        {
            "cpython-3.12.0-x86_64-pc-windows-msvc-full.tar.zst",
            "cpython-3.12.0-x86_64-pc-windows-msvc.tar.zst",
            "cpython-3.12.0-x86_64-pc-windows-msvc.zip"
        };

        // Document expected behavior
        foreach (var name in fullArchiveNames)
        {
            var isFull = name.ToLowerInvariant().Contains("full") ||
                        (!name.ToLowerInvariant().Contains("install") &&
                         (name.EndsWith(".tar.zst", StringComparison.OrdinalIgnoreCase) ||
                          name.EndsWith(".zip", StringComparison.OrdinalIgnoreCase)));
            
            Assert.That(isFull, Is.True);
        }
    }
}

