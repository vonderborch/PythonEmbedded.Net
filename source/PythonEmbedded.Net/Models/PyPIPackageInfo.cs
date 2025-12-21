namespace PythonEmbedded.Net.Models;

/// <summary>
/// Represents package metadata from PyPI.
/// </summary>
public record PyPIPackageInfo(
    string Name,
    string Version,
    string? Summary = null,
    string? Description = null,
    string? Author = null,
    string? AuthorEmail = null,
    string? HomePage = null,
    string? License = null,
    IReadOnlyList<string>? RequiresPython = null);

/// <summary>
/// Represents search results from PyPI.
/// </summary>
public record PyPISearchResult(
    string Name,
    string Version,
    string? Summary = null);

