# PyPIPackageInfo Records

Records for representing PyPI package information and search results.

## Namespace

`PythonEmbedded.Net.Models`

## PyPIPackageInfo

Represents package metadata from PyPI.

```csharp
public record PyPIPackageInfo(
    string Name,
    string Version,
    string? Summary = null,
    string? Description = null,
    string? Author = null,
    string? AuthorEmail = null,
    string? HomePage = null,
    string? License = null,
    IReadOnlyList<string>? RequiresPython = null)
```

**Properties:**
- `Name` - Package name
- `Version` - Package version
- `Summary` - Optional package summary
- `Description` - Optional package description
- `Author` - Optional author name
- `AuthorEmail` - Optional author email
- `HomePage` - Optional homepage URL
- `License` - Optional license information
- `RequiresPython` - Optional Python version requirements

## PyPISearchResult

Represents search results from PyPI.

```csharp
public record PyPISearchResult(
    string Name,
    string Version,
    string? Summary = null)
```

**Properties:**
- `Name` - Package name
- `Version` - Package version
- `Summary` - Optional package summary

**Example:**

```csharp
var results = runtime.SearchPackages("requests");
foreach (var result in results)
{
    Console.WriteLine($"{result.Name} {result.Version}: {result.Summary}");
}
```

## Related Types

- [BasePythonRuntime](../Runtimes/BasePythonRuntime.md) - Uses PyPISearchResult for package search




