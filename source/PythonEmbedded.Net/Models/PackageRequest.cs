namespace PythonEmbedded.Net;

/// <summary>A package installation request beyond a simple package name.</summary>
public sealed record PackageRequest
{
    /// <summary>Package specifiers, e.g. <c>"requests"</c> or <c>"requests==2.31"</c>.</summary>
    public string[] Packages { get; init; } = [];

    /// <summary>Path to a requirements file to install from.</summary>
    public string? RequirementsFile { get; init; }

    /// <summary>Alternative package index URL.</summary>
    public string? IndexUrl { get; init; }

    /// <summary>Extra arguments passed through to the underlying tool.</summary>
    public string[] ExtraArgs { get; init; } = [];

    /// <summary>
    /// A project directory whose declared dependencies (pyproject.toml) should be installed.
    /// Honored by project-aware installers (poetry, uv); ignored by plain pip.
    /// </summary>
    public string? ProjectDirectory { get; init; }
}
