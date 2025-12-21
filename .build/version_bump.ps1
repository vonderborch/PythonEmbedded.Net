param(
    [Parameter(Position = 0)]
    [string] $Directory,
    [Parameter(Position = 1)]
    [ValidateSet("Release-Major", "Release-Minor", "Release-Patch", "Release-Revision")]
    [string] $Mode,
    [Parameter(Position = 2, Mandatory = $False)]
    [string[]] $ExcludedDirectories = @()
)

###################################################################################################################
################################################# Common Helpers ##################################################
###################################################################################################################

. "$PSScriptRoot/version_bump_common.ps1"

###################################################################################################################
##################################################### LOGIC #######################################################
###################################################################################################################

Write-Output "Params:";
Write-Output "  Directory: $Directory";
Write-Output "  Mode: $Mode";
Write-Output "  ExcludedDirectories: $ExcludedDirectories";

# Get the latest version...
$versionMajor, $versionMinor, $versionPatch, $versionRevision = Get-LatestVersion -Directory $Directory;

Write-Output "Previous version: $versionMajor.$versionMinor.$versionPatch.$versionRevision";

# Update the version...
if ($Mode -eq "Release-Major")
{
    $versionMajor++;
    $versionMinor = 0;
    $versionPatch = 0;
    $versionRevision = 0;
}
elseif ($Mode -eq "Release-Minor")
{
    $versionMinor++;
    $versionPatch = 0;
    $versionRevision = 0;
}
elseif ($Mode -eq "Release-Patch")
{
    $versionPatch++;
    $versionRevision = 0;
}
elseif ($Mode -eq "Release-Revision")
{
    $versionRevision++;
}

# Update the version in the project files...
Update-ProjectFiles -Directory $Directory -versionMajor $versionMajor -versionMinor $versionMinor -versionPatch $versionPatch -versionRevision $versionRevision -ExcludedDirectories $ExcludedDirectories;
