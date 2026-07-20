namespace PythonEmbedded.Net;

/// <summary>
/// A version constraint parsed from user input: <c>"latest"</c>, <c>"3"</c>, <c>"3.13"</c>,
/// <c>"3.13.2"</c>, or a full pre-release like <c>"3.15.0b3"</c>.
/// </summary>
public sealed record PythonVersionRequest(int? Major, int? Minor, int? Patch, string? Suffix, string Raw)
{
    /// <summary>Parses a version constraint.</summary>
    /// <exception cref="FormatException">The string is not a recognizable version constraint.</exception>
    public static PythonVersionRequest Parse(string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);
        string trimmed = value.Trim();

        if (trimmed.Equals("latest", StringComparison.OrdinalIgnoreCase))
        {
            return new PythonVersionRequest(null, null, null, null, trimmed);
        }

        if (PythonVersion.TryParse(trimmed, out PythonVersion full))
        {
            return new PythonVersionRequest(full.Major, full.Minor, full.Patch, full.Suffix, trimmed);
        }

        string[] parts = trimmed.Split('.');
        if (parts.Length is 1 or 2 && parts.All(p => int.TryParse(p, out _)))
        {
            return new PythonVersionRequest(
                int.Parse(parts[0]),
                parts.Length > 1 ? int.Parse(parts[1]) : null,
                null,
                null,
                trimmed);
        }

        throw new FormatException($"'{value}' is not a valid Python version request (expected 'latest', '3', '3.13', '3.13.2', or '3.15.0b3').");
    }

    /// <summary>Whether the given concrete version satisfies this request.</summary>
    public bool Matches(PythonVersion version)
    {
        if (Major is not null && version.Major != Major)
        {
            return false;
        }

        if (Minor is not null && version.Minor != Minor)
        {
            return false;
        }

        if (Patch is not null)
        {
            // A fully-specified request pins the suffix too: "3.15.0" must not match 3.15.0b3.
            if (version.Patch != Patch || !string.Equals(version.Suffix, Suffix, StringComparison.Ordinal))
            {
                return false;
            }
        }

        return true;
    }

    /// <inheritdoc />
    public override string ToString() => Raw;
}
