param(
    [string]$SolutionDir
)

try {
    # Clear out any existing nuget packages
    $release_dir = [System.IO.Path]::Combine($SolutionDir, "release")
    Remove-Item -Path $release_dir -Include *.nupkg -Recurse -Force

    # Execute the Version Bump script
    $version_bump_script = [System.IO.Path]::Combine($SolutionDir, ".build", "version_bump.py")
    python3 $version_bump_script --directory $SolutionDir --exclude *Templates* --mode Dialog
    
    exit 0;
}
catch {
    Write-Error $_
    exit 1
}
