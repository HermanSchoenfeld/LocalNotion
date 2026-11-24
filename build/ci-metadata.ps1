#requires -Version 7.4
[CmdletBinding()]
param(
    [string]$Version,
    [ValidateRange(0, 65534)][int]$BuildNumber = 0,
    [string]$Tag,
    [switch]$Publish
)
$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
# Releases restore the exact public Framework package that Directory.Build.props selects.
[xml]$buildProperties = Get-Content -LiteralPath (Join-Path $root 'Directory.Build.props') -Raw
$frameworkNodes = @($buildProperties.SelectNodes('/Project/PropertyGroup/Sphere10FrameworkVersion'))
$sourceNodes = @($buildProperties.SelectNodes('/Project/PropertyGroup/UseSphere10FrameworkSource'))
if ($frameworkNodes.Count -ne 1 -or $sourceNodes.Count -ne 1) { throw 'Directory.Build.props must declare one Sphere10FrameworkVersion and one UseSphere10FrameworkSource default.' }
if ($sourceNodes[0].InnerText.Trim() -cne 'false') { throw 'Releases build against the public Framework packages; keep UseSphere10FrameworkSource false by default.' }
$frameworkVersion = $frameworkNodes[0].InnerText.Trim()
if ($frameworkVersion -cnotmatch '\A(0|[1-9][0-9]*)\.(0|[1-9][0-9]*)\.(0|[1-9][0-9]*)(-[0-9A-Za-z.-]+)?\z') { throw 'Sphere10FrameworkVersion must be one exact Framework package version.' }

[xml]$configuration = Get-Content -LiteralPath (Join-Path $root 'Version.props') -Raw
$configuredVersion = $configuration.SelectSingleNode('/Project/PropertyGroup/ReleaseVersion').InnerText.Trim()
if ($Tag) {
    if ($Tag -notmatch '^v(.+)$') { throw 'Release tags must start with v.' }
    $Version = $Matches[1]
    if ($Version -cne $configuredVersion) { throw 'The release tag must match Version.props. Use build/release.ps1 to publish a new version.' }
}
if (-not $Version) { $Version = $configuredVersion }
# Master and pull-request builds may use a Framework CI prerelease; a released Local Notion must build on a released Framework package.
if (($Tag -or $Publish) -and $frameworkVersion.Contains('-')) { throw "Releases build on a released Framework package, not the prerelease $frameworkVersion. Tag Framework, then set Sphere10FrameworkVersion in Directory.Build.props to that release." }
if ($Version -notmatch '^(0|[1-9][0-9]{0,4})\.(0|[1-9][0-9]{0,4})\.(0|[1-9][0-9]{0,4})(-(0|[1-9][0-9]*|[0-9]*[A-Za-z-][0-9A-Za-z-]*)(\.(0|[1-9][0-9]*|[0-9]*[A-Za-z-][0-9A-Za-z-]*))*)?$') { throw 'Use a semantic version such as 1.5.0 or 1.5.0-rc.1, without build metadata.' }
if ($Version.Length -gt 128) { throw 'The release version exceeds the Docker tag limit.' }
$numeric = [version]($Version.Split('-')[0])
if ($numeric.Major -gt 65534 -or $numeric.Minor -gt 65534 -or $numeric.Build -gt 65534) { throw 'Version components must not exceed 65534.' }
$sha = (& git -C $root rev-parse HEAD).Trim()
if ($LASTEXITCODE -ne 0 -or $sha -notmatch '^[0-9a-f]{40}$') { throw 'Cannot determine the release commit.' }
if ($Publish) {
    if ($env:GITHUB_REPOSITORY -cne 'Sphere10/LocalNotion') { throw 'Official releases run only in Sphere10/LocalNotion.' }
    $defaultBranch = if ($env:DEFAULT_BRANCH) { $env:DEFAULT_BRANCH } else { 'master' }
    & git -C $root merge-base --is-ancestor HEAD "origin/$defaultBranch"
    if ($LASTEXITCODE -ne 0) { throw 'The release commit must belong to the company default branch.' }
}
$matrix = Get-Content -LiteralPath (Join-Path $PSScriptRoot 'platforms.json') -Raw | ConvertFrom-Json
$result = [ordered]@{
    version = $Version
    tag = "v$Version"
    build_number = $BuildNumber
    sha = $sha
    framework_version = $frameworkVersion
    prerelease = $Version.Contains('-').ToString().ToLowerInvariant()
    publish = $Publish.IsPresent.ToString().ToLowerInvariant()
    matrix = ($matrix | ConvertTo-Json -Depth 5 -Compress)
}
if ($env:GITHUB_OUTPUT) {
    foreach ($entry in $result.GetEnumerator()) { "$($entry.Key)=$($entry.Value)" | Out-File -LiteralPath $env:GITHUB_OUTPUT -Append -Encoding utf8 }
}
[pscustomobject]$result
