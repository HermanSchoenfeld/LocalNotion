#requires -Version 7.4
<#
.SYNOPSIS
Publishes and archives one supported self-contained LocalNotion release.
.EXAMPLE
pwsh ./build/package.ps1 -Runtime win-x64 -BuildNumber 17
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory)]
    [ValidateSet('win-x64', 'win-x86', 'win-arm64', 'linux-x64', 'linux-arm64', 'linux-arm', 'osx-x64', 'osx-arm64')]
    [string] $Runtime,
    [string] $Version,
    [ValidateRange(0, 65534)]
    [int] $BuildNumber = 0,
    [string] $SourceRevisionId,
    [string] $OutputDirectory
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
$PSNativeCommandUseErrorActionPreference = $false
$repoRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))

if (-not $PSBoundParameters.ContainsKey('Version')) {
    [xml] $versionProperties = Get-Content -LiteralPath (Join-Path $repoRoot 'Version.props') -Raw
    $Version = $versionProperties.SelectSingleNode('/Project/PropertyGroup/ReleaseVersion').InnerText.Trim()
}
$versionPattern = '^(0|[1-9][0-9]{0,4})\.(0|[1-9][0-9]{0,4})\.(0|[1-9][0-9]{0,4})(-(0|[1-9][0-9]*|[0-9]*[A-Za-z-][0-9A-Za-z-]*)(\.(0|[1-9][0-9]*|[0-9]*[A-Za-z-][0-9A-Za-z-]*))*)?$'
if ($Version -cnotmatch $versionPattern) {
    throw 'Version must be a semantic version without build metadata, for example 1.5.0 or 1.5.0-rc.1.'
}
$numericVersion = [version] ($Version.Split('-')[0])
if ($numericVersion.Major -gt 65534 -or $numericVersion.Minor -gt 65534 -or $numericVersion.Build -gt 65534) {
    throw 'The major, minor, and patch version components must be between 0 and 65534.'
}
if (-not $PSBoundParameters.ContainsKey('SourceRevisionId')) {
    $SourceRevisionId = (& git -C $repoRoot rev-parse HEAD | Out-String).Trim()
    if ($LASTEXITCODE -ne 0) { throw 'Could not resolve git HEAD. Supply -SourceRevisionId explicitly.' }
}
if ($SourceRevisionId -cnotmatch '^[0-9a-fA-F]{7,64}$') {
    throw 'SourceRevisionId must be a hexadecimal Git commit ID (7 to 64 characters).'
}
$SourceRevisionId = $SourceRevisionId.ToLowerInvariant()
$platforms = Get-Content -LiteralPath (Join-Path $PSScriptRoot 'platforms.json') -Raw | ConvertFrom-Json
$platform = @($platforms.include | Where-Object rid -CEQ $Runtime)
if ($platform.Count -ne 1) { throw "Runtime '$Runtime' must have exactly one entry in build/platforms.json." }
$archiveExtension = if ($Runtime.StartsWith('win-')) { 'zip' } else { 'tar.gz' }
if ($platform[0].archiveExtension -cne $archiveExtension) { throw "Incorrect archive extension for $Runtime in build/platforms.json." }
if (-not $PSBoundParameters.ContainsKey('OutputDirectory')) {
    $OutputDirectory = Join-Path $repoRoot "publish/$Version/artifacts"
}
if ([string]::IsNullOrWhiteSpace($OutputDirectory)) { throw 'OutputDirectory must not be empty.' }
$outputRoot = [IO.Path]::GetFullPath($OutputDirectory, (Get-Location).ProviderPath)
$stageRoot = [IO.Path]::GetFullPath((Join-Path $repoRoot 'publish/.staging'))
$stageDirectory = Join-Path $stageRoot "$Runtime-$([guid]::NewGuid().ToString('N'))"
$payloadDirectory = Join-Path $stageDirectory 'payload'
$archiveName = "localnotion-$Runtime.$archiveExtension"
$archivePath = Join-Path $outputRoot $archiveName

function Remove-StagingDirectory {
    if (-not (Test-Path -LiteralPath $stageDirectory)) { return }
    $resolvedRoot = (Resolve-Path -LiteralPath $stageRoot).ProviderPath.TrimEnd([IO.Path]::DirectorySeparatorChar) + [IO.Path]::DirectorySeparatorChar
    $resolvedTarget = (Resolve-Path -LiteralPath $stageDirectory).ProviderPath
    $comparison = if ($IsWindows) { [StringComparison]::OrdinalIgnoreCase } else { [StringComparison]::Ordinal }
    if (-not $resolvedTarget.StartsWith($resolvedRoot, $comparison) -or
        ((Get-Item -LiteralPath $resolvedTarget).Attributes -band [IO.FileAttributes]::ReparsePoint)) {
        throw "Refusing to clean a staging path outside '$resolvedRoot': $resolvedTarget"
    }
    Remove-Item -LiteralPath $resolvedTarget -Recurse -Force
}

function New-TarGzipArchive([string] $SourceDirectory, [string] $DestinationPath) {
    $fileStream = [IO.File]::Create($DestinationPath)
    try {
        $gzip = [IO.Compression.GZipStream]::new($fileStream, [IO.Compression.CompressionLevel]::Optimal, $true)
        try {
            $writer = [System.Formats.Tar.TarWriter]::new($gzip, [System.Formats.Tar.TarEntryFormat]::Pax, $true)
            try {
                foreach ($item in (Get-ChildItem -LiteralPath $SourceDirectory -Force -Recurse | Sort-Object FullName)) {
                    $entryName = [IO.Path]::GetRelativePath($SourceDirectory, $item.FullName).Replace('\', '/')
                    $isLink = $null -ne $item.LinkTarget
                    $entryType = if ($isLink) { [System.Formats.Tar.TarEntryType]::SymbolicLink }
                        elseif ($item.PSIsContainer) { [System.Formats.Tar.TarEntryType]::Directory }
                        else { [System.Formats.Tar.TarEntryType]::RegularFile }
                    $entry = [System.Formats.Tar.PaxTarEntry]::new($entryType, $entryName)
                    $entry.ModificationTime = $item.LastWriteTimeUtc
                    $entry.Mode = if ($item.PSIsContainer -or $isLink -or $entryName -ceq 'localnotion' -or $entryName.EndsWith('.sh')) {
                        [IO.UnixFileMode] 493 # 0755, including Unix apphosts cross-published from Windows.
                    } else { [IO.UnixFileMode] 420 } # 0644
                    if (-not $IsWindows -and -not $isLink) {
                        $entry.Mode = [IO.File]::GetUnixFileMode($item.FullName)
                        if ($entryName -ceq 'localnotion' -or $entryName.EndsWith('.sh')) { $entry.Mode = $entry.Mode -bor [IO.UnixFileMode] 73 } # 0111
                    }
                    if ($isLink) {
                        $entry.LinkName = $item.LinkTarget.Replace('\', '/')
                        $writer.WriteEntry($entry)
                    } elseif ($item.PSIsContainer) {
                        $writer.WriteEntry($entry)
                    } else {
                        $contentStream = [IO.File]::OpenRead($item.FullName)
                        try {
                            $entry.DataStream = $contentStream
                            $writer.WriteEntry($entry)
                        } finally { $contentStream.Dispose() }
                    }
                }
            } finally { $writer.Dispose() }
        } finally { $gzip.Dispose() }
    } finally { $fileStream.Dispose() }
}

# All user-controlled identifiers have been validated before creating any output.
$null = New-Item -ItemType Directory -Path $outputRoot -Force
$null = New-Item -ItemType Directory -Path $payloadDirectory -Force
try {
    $publishArguments = @(
        'publish', (Join-Path $repoRoot 'LocalNotion.CLI/LocalNotion.CLI.csproj'),
        '--configuration', 'Release', '--framework', 'net8.0', '--runtime', $Runtime,
        '--self-contained', 'true', '--output', $payloadDirectory,
        '-p:PublishSingleFile=true', '-p:PublishTrimmed=false', '-p:PublishReadyToRun=false',
        '-p:DebugSymbols=false', '-p:DebugType=None',
        "-p:ReleaseVersion=$Version", "-p:BuildNumber=$BuildNumber", "-p:SourceRevisionId=$SourceRevisionId"
    )
    & dotnet @publishArguments
    if ($LASTEXITCODE -ne 0) { throw "dotnet publish failed for $Runtime (exit $LASTEXITCODE)." }

    $executableName = if ($Runtime.StartsWith('win-')) { 'localnotion.exe' } else { 'localnotion' }
    if (-not (Test-Path -LiteralPath (Join-Path $payloadDirectory $executableName) -PathType Leaf)) {
        throw "The published package is missing $executableName."
    }
    Copy-Item -LiteralPath (Join-Path $repoRoot 'LICENSE'), (Join-Path $repoRoot 'COPYRIGHT') -Destination $payloadDirectory
    $attachmentPlatform = if ($Runtime.StartsWith('win-')) { 'windows' } else { 'unix' }
    $attachmentDirectory = Join-Path $PSScriptRoot "attachments/$attachmentPlatform"
    if (-not (Test-Path -LiteralPath $attachmentDirectory -PathType Container)) {
        throw "Missing release attachments: $attachmentDirectory"
    }
    foreach ($attachment in (Get-ChildItem -LiteralPath $attachmentDirectory -Force)) {
        $destination = Join-Path $payloadDirectory $attachment.Name
        if ((Test-Path -LiteralPath $destination) -or $attachment.Name -in @('localnotion-release.json', 'VERSION.txt')) {
            throw "Release attachment conflicts with package content: $($attachment.Name)"
        }
        Copy-Item -LiteralPath $attachment.FullName -Destination $destination -Recurse
    }
    [IO.File]::WriteAllText((Join-Path $payloadDirectory 'VERSION.txt'), "$Version-build.$BuildNumber`n", [Text.UTF8Encoding]::new($false))
    [ordered] @{
        version = $Version
        buildNumber = $BuildNumber
        commit = $SourceRevisionId
        runtime = $Runtime
        informationalVersion = "$Version+build.$BuildNumber.sha.$SourceRevisionId"
    } | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $payloadDirectory 'localnotion-release.json') -Encoding utf8NoBOM

    $stagedArchive = Join-Path $stageDirectory $archiveName
    if ($archiveExtension -ceq 'zip') {
        [IO.Compression.ZipFile]::CreateFromDirectory($payloadDirectory, $stagedArchive, [IO.Compression.CompressionLevel]::Optimal, $false)
    } else {
        New-TarGzipArchive -SourceDirectory $payloadDirectory -DestinationPath $stagedArchive
    }
    Move-Item -LiteralPath $stagedArchive -Destination $archivePath -Force
    Write-Host "Created $archivePath"
} finally {
    Remove-StagingDirectory
}
