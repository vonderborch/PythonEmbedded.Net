using System.Text.RegularExpressions;

namespace PythonEmbedded.Net.Models;

/// <summary>A concrete Python version, e.g. <c>3.13.14</c> or <c>3.15.0b3</c>.</summary>
public readonly partial record struct PythonVersion(int Major, int Minor, int Patch, string? Suffix = null)
    : IComparable<PythonVersion>
{
    [GeneratedRegex(@"^(?<major>\d+)\.(?<minor>\d+)\.(?<patch>\d+)(?<suffix>[a-z].*)?$")]
    private static partial Regex Pattern();

    /// <summary>Parses a full version string such as <c>3.13.14</c> or <c>3.15.0b3</c>.</summary>
    /// <exception cref="FormatException">The string is not a full <c>major.minor.patch[suffix]</c> version.</exception>
    public static PythonVersion Parse(string value)
    {
        if (!TryParse(value, out PythonVersion version))
        {
            throw new FormatException($"'{value}' is not a valid Python version (expected e.g. '3.13.14' or '3.15.0b3').");
        }

        return version;
    }

    /// <summary>Attempts to parse a full version string such as <c>3.13.14</c> or <c>3.15.0b3</c>.</summary>
    public static bool TryParse(string? value, out PythonVersion version)
    {
        version = default;
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        Match match = Pattern().Match(value.Trim());
        if (!match.Success)
        {
            return false;
        }

        version = new PythonVersion(
            int.Parse(match.Groups["major"].Value),
            int.Parse(match.Groups["minor"].Value),
            int.Parse(match.Groups["patch"].Value),
            match.Groups["suffix"].Success ? match.Groups["suffix"].Value : null);
        return true;
    }

    /// <summary>Orders versions numerically; a pre-release suffix sorts before the final release of the same patch.</summary>
    public int CompareTo(PythonVersion other)
    {
        int result = Major.CompareTo(other.Major);
        if (result != 0)
        {
            return result;
        }

        result = Minor.CompareTo(other.Minor);
        if (result != 0)
        {
            return result;
        }

        result = Patch.CompareTo(other.Patch);
        if (result != 0)
        {
            return result;
        }

        // Final release (no suffix) is newer than any pre-release of the same patch.
        return (Suffix, other.Suffix) switch
        {
            (null, null) => 0,
            (null, _) => 1,
            (_, null) => -1,
            _ => string.CompareOrdinal(Suffix, other.Suffix),
        };
    }

    /// <inheritdoc />
    public override string ToString() => $"{Major}.{Minor}.{Patch}{Suffix}";
}
