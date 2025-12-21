param(
    [string]$ProjectDir,
    [string]$SolutionDir
)

try {
    # Clear out the release dirs in the .build dir
    $build_debug_dir = [System.IO.Path]::Combine($ProjectDir, "debug")
    $build_release_dir = [System.IO.Path]::Combine($ProjectDir, "release")
    Remove-Item -Path $build_debug_dir -Recurse -Force -ErrorAction SilentlyContinue
    Remove-Item -Path $build_release_dir -Recurse -Force -ErrorAction SilentlyContinue

    # Find all nupkg and xml documentation items in the first level of folders under the release dir
    $release_dir = [System.IO.Path]::Combine($SolutionDir, "release")
    $folders = Get-ChildItem -Path $release_dir -Directory
    $nupkg_files = $folders | Get-ChildItem -Filter *.nupkg
    $xml_files = $folders | Get-ChildItem -Filter *.xml
    
    if ($nupkg_files.Count -gt 0) {
        # Copy the nupkg files to the release dir
        $nupkg_files | Copy-Item -Destination $release_dir -Force
    }
    if ($xml_files.Count -gt 0) {
        # Copy the xml files to the release dir
        $xml_files | Copy-Item -Destination $release_dir -Force
    }
    
    
    exit 0;
}
catch {
    Write-Error $_
    exit 1
}
