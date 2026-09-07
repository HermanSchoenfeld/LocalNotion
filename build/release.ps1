#requires -Version 7.0

<#
.SYNOPSIS
Publishes a committed Local Notion release through the official GitHub workflow.

.DESCRIPTION
Validates the release version, clean checkout, official origin, and master ancestry.
Updates and commits only Version.props when necessary, then creates an annotated tag
and atomically pushes the current HEAD to master together with that tag.

Use -WhatIf to preview the release. The preview may query origin and fetch master,
but does not edit source files, create commits or tags, or push anything.

.EXAMPLE
.\build\release.ps1 -Version 1.5.0 -WhatIf

.EXAMPLE
.\build\release.ps1 -Version 1.5.1

.EXAMPLE
.\build\release.ps1 -Version 1.6.0-rc.1
#>
[CmdletBinding(SupportsShouldProcess = $true, ConfirmImpact = 'Medium')]
param(
    [Parameter(Mandatory = $true)]
    [ValidateNotNullOrEmpty()]
    [string] $Version
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
$PSNativeCommandUseErrorActionPreference = $false

function ConvertTo-ReleaseVersion {
    param(
        [Parameter(Mandatory = $true)][string] $Value,
        [Parameter(Mandatory = $true)][string] $Name
    )

    $pattern = '\A(0|[1-9][0-9]{0,4})\.(0|[1-9][0-9]{0,4})\.(0|[1-9][0-9]{0,4})(?:-(?:0|[1-9][0-9]*|[0-9]*[A-Za-z-][0-9A-Za-z-]*)(?:\.(?:0|[1-9][0-9]*|[0-9]*[A-Za-z-][0-9A-Za-z-]*))*)?\z'
    $match = [regex]::Match($Value, $pattern)
    if (-not $match.Success) {
        throw "$Name must be a semantic version such as 1.5.0 or 1.6.0-rc.1, without build metadata."
    }
    foreach ($component in 1..3) {
        if ([int] $match.Groups[$component].Value -gt 65534) {
            throw "Each numeric component of $Name must be between 0 and 65534."
        }
    }

    return [System.Management.Automation.SemanticVersion]::Parse($Value)
}

$requestedVersion = ConvertTo-ReleaseVersion -Value $Version -Name 'Version'
$repositoryPath = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$gitExecutable = (Get-Command git -CommandType Application -ErrorAction Stop | Select-Object -First 1).Source

function Invoke-ReleaseGit {
    param(
        [Parameter(Mandatory = $true)][string[]] $GitArguments,
        [switch] $Network,
        [int[]] $AllowedExitCodes = @(0)
    )

    $commandArguments = @('-C', $repositoryPath)
    if ($Network -and $IsWindows) {
        # Use the Windows certificate store without weakening TLS verification.
        $commandArguments += @('-c', 'http.sslBackend=schannel')
    }
    $commandArguments += $GitArguments
    $output = @(& $gitExecutable @commandArguments 2>&1 | ForEach-Object { "$_" })
    $exitCode = $LASTEXITCODE
    if ($exitCode -notin $AllowedExitCodes) {
        throw ("git $($GitArguments -join ' ') failed (exit $exitCode):" + [Environment]::NewLine + ($output -join [Environment]::NewLine))
    }

    return [pscustomobject]@{ ExitCode = $exitCode; Output = $output }
}

$gitRoot = ((Invoke-ReleaseGit -GitArguments @('rev-parse', '--show-toplevel')).Output -join '').Trim()
if (-not [string]::Equals(
    [IO.Path]::GetFullPath($gitRoot),
    $repositoryPath,
    $(if ($IsWindows) { [StringComparison]::OrdinalIgnoreCase } else { [StringComparison]::Ordinal })
)) {
    throw 'The release helper must be located in the build directory of its Git checkout.'
}

$officialOrigins = @(
    'https://github.com/Sphere10/LocalNotion.git',
    'git@github.com:Sphere10/LocalNotion.git',
    'ssh://git@github.com/Sphere10/LocalNotion.git'
)
foreach ($direction in @('fetch', 'push')) {
    $urlArguments = @('remote', 'get-url', '--all')
    if ($direction -eq 'push') { $urlArguments += '--push' }
    $urlArguments += 'origin'
    $originUrls = (Invoke-ReleaseGit -GitArguments $urlArguments).Output
    if ($originUrls.Count -ne 1 -or $originUrls[0] -cnotin $officialOrigins) {
        throw "origin must have exactly one official Sphere10/LocalNotion $direction URL (HTTPS or SSH)."
    }
}

$status = (Invoke-ReleaseGit -GitArguments @('status', '--porcelain=v1', '--untracked-files=all')).Output
if ($status.Count -ne 0) {
    throw 'The checkout must be clean, including untracked files. Commit or remove pending changes before releasing.'
}

$versionPath = Join-Path $repositoryPath 'Version.props'
$null = Invoke-ReleaseGit -GitArguments @('ls-files', '--error-unmatch', '--', 'Version.props', '.github/workflows/release.yml')
if (-not (Test-Path -LiteralPath $versionPath -PathType Leaf)) {
    throw 'The checkout must contain a committed Version.props file.'
}
$versionBytes = [IO.File]::ReadAllBytes($versionPath)
$versionText = [IO.File]::ReadAllText($versionPath)
[xml] $versionDocument = $versionText
$versionNodes = $versionDocument.SelectNodes('/Project/PropertyGroup/ReleaseVersion')
$versionMatches = [regex]::Matches($versionText, '<ReleaseVersion\b[^>]*>(?<value>[^<]*)</ReleaseVersion>')
if ($versionNodes.Count -ne 1 -or $versionMatches.Count -ne 1) {
    throw 'Version.props must contain exactly one ReleaseVersion element.'
}
$currentVersionText = $versionNodes[0].InnerText.Trim()
$currentVersion = ConvertTo-ReleaseVersion -Value $currentVersionText -Name 'ReleaseVersion in Version.props'
if ($requestedVersion.CompareTo($currentVersion) -lt 0) {
    throw "Version $Version is older than the checked-in release version $currentVersionText. Releases cannot downgrade."
}

$tag = "v$Version"
$tagReference = "refs/tags/$tag"
$localTag = Invoke-ReleaseGit -GitArguments @('show-ref', '--verify', '--quiet', $tagReference) -AllowedExitCodes @(0, 1)
if ($localTag.ExitCode -eq 0) {
    throw "Local tag $tag already exists. Existing release tags are never moved or overwritten."
}
$remoteTag = Invoke-ReleaseGit -Network -GitArguments @('ls-remote', '--exit-code', '--tags', 'origin', $tagReference) -AllowedExitCodes @(0, 2)
if ($remoteTag.ExitCode -eq 0) {
    throw "Remote tag $tag already exists. Choose a new release version."
}

$null = Invoke-ReleaseGit -Network -GitArguments @('fetch', '--no-tags', 'origin', 'refs/heads/master:refs/remotes/origin/master')
$ancestry = Invoke-ReleaseGit -GitArguments @('merge-base', '--is-ancestor', 'refs/remotes/origin/master', 'HEAD') -AllowedExitCodes @(0, 1)
if ($ancestry.ExitCode -ne 0) {
    throw 'Current HEAD does not descend from origin/master. Integrate origin/master into this branch before releasing.'
}
$head = ((Invoke-ReleaseGit -GitArguments @('rev-parse', 'HEAD')).Output -join '').Trim()
$updateVersion = $currentVersionText -cne $Version
$workflowUrl = "https://github.com/Sphere10/LocalNotion/actions/workflows/release.yml?query=branch%3A$tag"

Write-Host "Release: $tag"
Write-Host "Current commit: $head"
if ($updateVersion) {
    Write-Host "Version.props: $currentVersionText -> $Version (commit only this file)"
} else {
    Write-Host "Version.props: $Version (no version commit needed)"
}
Write-Host "Create annotated tag: $tag"
Write-Host "Atomic push: HEAD:refs/heads/master $($tagReference):$tagReference"
Write-Host "Workflow: $workflowUrl"

$action = "Publish $tag by "
if ($updateVersion) { $action += 'updating and committing Version.props, ' }
$action += 'creating an annotated tag, and atomically pushing HEAD to master with that tag'
if (-not $PSCmdlet.ShouldProcess('Sphere10/LocalNotion on GitHub', $action)) {
    return
}

# Recheck local state immediately before changing anything.
$currentHead = ((Invoke-ReleaseGit -GitArguments @('rev-parse', 'HEAD')).Output -join '').Trim()
$currentStatus = (Invoke-ReleaseGit -GitArguments @('status', '--porcelain=v1', '--untracked-files=all')).Output
if ($currentHead -cne $head -or $currentStatus.Count -ne 0) {
    throw 'The checkout changed during release validation. Review the changes and run the helper again.'
}

if ($updateVersion) {
    $valueGroup = $versionMatches[0].Groups['value']
    $updatedVersionText = $versionText.Substring(0, $valueGroup.Index) + $Version + $versionText.Substring($valueGroup.Index + $valueGroup.Length)
    $hasUtf8Bom = $versionBytes.Length -ge 3 -and $versionBytes[0] -eq 0xEF -and $versionBytes[1] -eq 0xBB -and $versionBytes[2] -eq 0xBF
    [IO.File]::WriteAllText($versionPath, $updatedVersionText, [Text.UTF8Encoding]::new($hasUtf8Bom))
    $null = Invoke-ReleaseGit -GitArguments @('commit', '--only', '-m', "Release Local Notion $Version", '--', 'Version.props')
}

$null = Invoke-ReleaseGit -GitArguments @('tag', '--annotate', $tag, '--message', "Local Notion $Version")
try {
    $push = Invoke-ReleaseGit -Network -GitArguments @('push', '--atomic', '--no-follow-tags', 'origin', 'HEAD:refs/heads/master', "$($tagReference):$tagReference")
    $push.Output | ForEach-Object { Write-Host $_ }
} catch {
    throw "The atomic push did not complete successfully. Inspect the remote state before retrying; the local release commit and tag have been retained. $($_.Exception.Message)"
}

$releaseCommit = ((Invoke-ReleaseGit -GitArguments @('rev-parse', 'HEAD')).Output -join '').Trim()
Write-Host "Published $tag at $releaseCommit. Follow the release workflow:"
Write-Host $workflowUrl
[pscustomobject]@{
    Version = $Version
    Tag = $tag
    Commit = $releaseCommit
    WorkflowUrl = $workflowUrl
}
