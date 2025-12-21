param(
    [Parameter(Position = 0)]
    [string]$Directory,
    [Parameter(Position = 1, Mandatory = $False)]
    [string[]]$ExcludedDirectories = @()
);

###################################################################################################################
################################################# Common Helpers ##################################################
###################################################################################################################

. "$PSScriptRoot/version_bump_common.ps1"

###################################################################################################################
################################################ Specific Helpers #################################################
###################################################################################################################


function Show-DialogBox
{
    [CmdletBinding(PositionalBinding = $false)]
    param(
        [Parameter(Position = 0)]
        [string] $Title,
        [Parameter(Position = 1)]
        [string] $Message,
        [Parameter(Mandatory, Position = 2)]
        [string] $Version
    )

    # So that the $IsLinux and $IsMacOS PS Core-only variables can safely be accessed in WinPS.
    Set-StrictMode -Off

    # Linux
    if ($IsLinux)
    {
        Throw "Not supported on Linux."
    }
    # Mac
    elseif ($IsMacOS)
    {
        # Simplified version of: https://github.com/johncwelch/Get-PSDisplayDialog/blob/main/Get-DisplayDialog/Get-DisplayDialog.psm1#L298
        $displayDialogCommand = "display dialog `"$Message`" default answer `"$Version`" buttons {`"OK`", `"Cancel`"} default button 1 with title `"$Title`"";

        $dialogReplyString = $displayDialogCommand | /usr/bin/osascript -so;

        if ( $dialogReplyString.Contains("execution error: User canceled. `(-128`)"))
        {
            throw "User Cancelled"
        }

        [System.Collections.ArrayList]$dialogReplyArray = @()
        [System.Collections.ArrayList]$dialogReplyArrayList = @()
        $dialogReply = [ordered]@{ }

        $dialogReplyArray = $dialogReplyString.Split(",")

        foreach ($item in $dialogReplyArray)
        {
            $dialogReplyArrayList.Add($item.trim()) |Out-Null #so we don't see 0/1/etc.
        }

        foreach ($item in $dialogReplyArrayList)
        {
            $hashEntry = $item.Split(":")
            $dialogReply.Add($hashEntry[0], $hashEntry[1])
        }

        if ( $dialogReply.Contains("text returned"))
        {
            return $dialogReply["text returned"].ToString()
        }
        else
        {
            return $Version
        }
    }
    # Windows
    else
    {
        Add-Type -Assembly Microsoft.VisualBasic
        $result = [Microsoft.VisualBasic.Interaction]::InputBox($Message, $Title, $Version)

        if ($result -eq "")
        {
            throw "User Cancelled"
        }
        return $result
    }
}

###################################################################################################################
##################################################### LOGIC #######################################################
###################################################################################################################

Write-Output "Params:";
Write-Output "  Directory: $Directory";
Write-Output "  ExcludedDirectories: $ExcludedDirectories";

# Get the latest version...
$latestVersionMajor, $latestVersionMinor, $latestVersionPatch, $latestVersionRevision = Get-LatestVersion -Directory $Directory;

Write-Output "Previous version: $latestVersionMajor.$latestVersionMinor.$latestVersionPatch.$latestVersionRevision";

# Bump the revision...
$latestVersionRevision++;

# Ask what the user wants to set the version as...
$version = Show-DialogBox -Title "Bump Version" -Message "Please enter the new version" -Version "$latestVersionMajor.$latestVersionMinor.$latestVersionPatch.$latestVersionRevision";

$versionParts = $version -split "\.";
$versionMajor = [int]$versionParts[0];
$versionMinor = [int]$versionParts[1];
$versionPatch = [int]$versionParts[2];
$versionRevision = [int]$versionParts[3];

# Update the version in the project files...
Update-ProjectFiles -Directory $Directory -versionMajor $versionMajor -versionMinor $versionMinor -versionPatch $versionPatch -versionRevision $versionRevision -ExcludedDirectories $ExcludedDirectories;
