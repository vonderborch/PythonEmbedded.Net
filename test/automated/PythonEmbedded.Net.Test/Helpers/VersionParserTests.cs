using NUnit.Framework;
using PythonEmbedded.Net.Helpers;

namespace PythonEmbedded.Net.Test.Helpers;

/// <summary>
/// Tests for VersionParser utility class.
/// </summary>
[TestFixture]
public class VersionParserTests
{
    [Test]
    public void ParseVersion_WithFullVersion_ReturnsCorrectComponents()
    {
        var (major, minor, patch) = VersionParser.ParseVersion("3.12.5");
        
        Assert.That(major, Is.EqualTo(3));
        Assert.That(minor, Is.EqualTo(12));
        Assert.That(patch, Is.EqualTo(5));
    }

    [Test]
    public void ParseVersion_WithPartialVersion_ReturnsZeroPatch()
    {
        var (major, minor, patch) = VersionParser.ParseVersion("3.12");
        
        Assert.That(major, Is.EqualTo(3));
        Assert.That(minor, Is.EqualTo(12));
        Assert.That(patch, Is.EqualTo(0));
    }

    [Test]
    public void ParseVersion_WithSingleNumber_ThrowsException()
    {
        Assert.Throws<PythonEmbedded.Net.Exceptions.InvalidPythonVersionException>(() =>
        {
            VersionParser.ParseVersion("3");
        });
    }

    [Test]
    public void NormalizeVersion_WithFullVersion_ReturnsSame()
    {
        var normalized = VersionParser.NormalizeVersion("3.12.5");
        
        Assert.That(normalized, Is.EqualTo("3.12.5"));
    }

    [Test]
    public void NormalizeVersion_WithPartialVersion_AddsPatch()
    {
        var normalized = VersionParser.NormalizeVersion("3.12");
        
        Assert.That(normalized, Is.EqualTo("3.12.0"));
    }

    [Test]
    public void CompareVersions_WithDifferentVersions_ReturnsCorrectComparison()
    {
        var result1 = VersionParser.CompareVersions("3.12.0", "3.11.0");
        Assert.That(result1, Is.GreaterThan(0));
        
        var result2 = VersionParser.CompareVersions("3.11.0", "3.12.0");
        Assert.That(result2, Is.LessThan(0));
        
        var result3 = VersionParser.CompareVersions("3.12.0", "3.12.0");
        Assert.That(result3, Is.EqualTo(0));
    }

    [Test]
    public void MatchesPartialVersion_WithMatchingVersions_ReturnsTrue()
    {
        var result = VersionParser.MatchesPartialVersion("3.12.5", "3.12");
        
        Assert.That(result, Is.True);
    }

    [Test]
    public void MatchesPartialVersion_WithNonMatchingVersions_ReturnsFalse()
    {
        var result = VersionParser.MatchesPartialVersion("3.11.5", "3.12");
        
        Assert.That(result, Is.False);
    }
}

