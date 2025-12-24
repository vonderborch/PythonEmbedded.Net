# PackageInfo Records

Records for representing Python package information.

## Namespace

`PythonEmbedded.Net.Models`

## PackageInfo

Represents information about an installed Python package.

```csharp
public record PackageInfo(
    string Name,
    string Version,
    string? Location = null,
    string? Summary = null)
```

**Properties:**
- `Name` - Package name
- `Version` - Installed version
- `Location` - Optional installation location
- `Summary` - Optional package summary

**Example:**

```csharp
var packages = runtime.ListInstalledPackages();
foreach (var package in packages)
{
    Console.WriteLine($"{package.Name} {package.Version}");
}
```

## OutdatedPackageInfo

Represents information about an outdated package (available update).

```csharp
public record OutdatedPackageInfo(
    string Name,
    string InstalledVersion,
    string LatestVersion)
```

**Properties:**
- `Name` - Package name
- `InstalledVersion` - Currently installed version
- `LatestVersion` - Latest available version

**Example:**

```csharp
var outdated = runtime.ListOutdatedPackages();
foreach (var package in outdated)
{
    Console.WriteLine($"{package.Name}: {package.InstalledVersion} -> {package.LatestVersion}");
}
```

## Related Types

- [BasePythonRuntime](../Runtimes/BasePythonRuntime.md) - Uses PackageInfo for package management



