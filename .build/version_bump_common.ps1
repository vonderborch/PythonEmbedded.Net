function Save-LatestVersion
{
    param(
        [Parameter(Position = 0)]
        [string] $Directory,
        [Parameter(Position = 1)]
        [int] $Major,
        [Parameter(Position = 2)]
        [int] $Minor,
        [Parameter(Position = 3)]
        [int] $Patch,
        [Parameter(Position = 4)]
        [int] $Revision
    )

    $cacheFile = [IO.Path]::Combine($Directory, ".version.json");
    $version = [ordered]@{
        major = $Major;
        minor = $Minor;
        patch = $Patch;
        revision = $Revision;
    } | ConvertTo-Json | Set-Content $cacheFile;

    return $version
}

function Get-LatestVersion
{
    param(
        [Parameter(Position = 0)]
        [string] $Directory
    )
    # Get the last version from the cache file
    $cacheFile = [IO.Path]::Combine($Directory, ".version.json");
    $lastVersionMajor = 1;
    $lastVersionMinor = 0;
    $lastVersionPatch = 0;
    $lastVersionRevision = 0;

    if (Test-Path $cacheFile)
    {
        $lastVersion = Get-Content $cacheFile | ConvertFrom-Json;
        $lastVersionMajor = $lastVersion.major;
        $lastVersionMinor = $lastVersion.minor;
        $lastVersionPatch = $lastVersion.patch;
        $lastVersionRevision = $lastVersion.revision;
    }
    else
    {
        Save-LatestVersion -Directory $Directory -Major 0 -Minor 0 -Patch 0 -Revision 0;
    }

    return $lastVersionMajor, $lastVersionMinor, $lastVersionPatch, $lastVersionRevision;
}

function Get-ProjectFiles
{
    param(
        [Parameter(Position = 0)]
        [string] $Directory,
        [Parameter(Position = 1)]
        [string[]]$ExcludedDirectories
    )

    $files = Get-ChildItem -Path $Directory -Filter "*.csproj" -Recurse -Exclude $ExcludedDirectories | %{
        $allowed = $true
        foreach ($exclude in $ExcludedDirectories)
        {
            if ((Split-Path $_.FullName -Parent) -ilike $exclude)
            {
                $allowed = $false
                break
            }
        }
        if ($allowed)
        {
            $_
        }
    }

    return $files
}

function Update-ProjectFiles
{
    param(
        [Parameter(Position = 0)]
        [string] $Directory,
        [Parameter(Position = 1)]
        [int] $versionMajor,
        [Parameter(Position = 2)]
        [int] $versionMinor,
        [Parameter(Position = 3)]
        [int] $versionPatch,
        [Parameter(Position = 4)]
        [int] $versionRevision,
        [Parameter(Position = 5)]
        [string[]] $ExcludedDirectories
    )

    Write-Output "Excluded Directories:"
    foreach ($dir in $ExcludedDirectories)
    {
        Write-Output "  $dir"
    }

    $version = "$versionMajor.$versionMinor.$versionPatch.$versionRevision"
    Write-Output "New version: $version"

    # Update all csproj files to match...
    $files = Get-ProjectFiles -Directory $Directory -ExcludedDirectories $ExcludedDirectories
    $regex = '(?<=Version>)(\d+\.\d+\.\d+\.\d+)*(?=<\/)'

    Write-Output "Found $( $files.Count ) files to update..."
    Write-Output "  Files: $files"

    foreach ($file in $files)
    {
        Write-Output "  Updating $( $file.FullName )..."
        $file_path = $file.FullName
        (Get-Content $file_path) -replace $regex, $version | Set-Content $file_path -Encoding UTF8
    }

    # Save the new version...
    Save-LatestVersion -Directory $Directory -Major $versionMajor -Minor $versionMinor -Patch $versionPatch -Revision $versionRevision
}
