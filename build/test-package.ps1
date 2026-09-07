#requires -Version 7.4
<#
.SYNOPSIS
Extracts a release archive, checks its identity, and smoke-tests the packaged executable.
.DESCRIPTION
Default extraction uses a unique ignored publish/.smoke directory and is cleaned afterwards.
-ExtractDirectory requires a new directory and keeps it for external checks such as ARM32 QEMU.
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory)]
    [string] $ArchivePath,
    [Parameter(Mandatory)]
    [ValidatePattern('^[0-9]+\.[0-9]+\.[0-9]+(-[0-9A-Za-z.-]+)?$')]
    [string] $Version,
    [Parameter(Mandatory)]
    [ValidateRange(0, 65534)]
    [int] $BuildNumber,
    [Parameter(Mandatory)]
    [ValidatePattern('^[0-9a-fA-F]{7,64}$')]
    [string] $SourceRevisionId,
    [Parameter(Mandatory)]
    [ValidateSet('win-x64', 'win-x86', 'win-arm64', 'linux-x64', 'linux-arm64', 'linux-arm', 'osx-x64', 'osx-arm64')]
    [string] $Runtime,
    [switch] $SkipExecution,
    [string] $ExtractDirectory
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
$repoRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$archive = (Resolve-Path -LiteralPath $ArchivePath).ProviderPath
$isWindowsPackage = $Runtime.StartsWith('win-')
$extension = if ($isWindowsPackage) { 'zip' } else { 'tar.gz' }
if ([IO.Path]::GetFileName($archive) -cne "localnotion-$Runtime.$extension") {
    throw "Expected an archive named localnotion-$Runtime.$extension."
}
$keepExtraction = $PSBoundParameters.ContainsKey('ExtractDirectory')
$smokeRoot = [IO.Path]::GetFullPath((Join-Path $repoRoot 'publish/.smoke'))
if ($keepExtraction) {
    if ([string]::IsNullOrWhiteSpace($ExtractDirectory)) { throw 'ExtractDirectory must not be empty.' }
    $extractionDirectory = [IO.Path]::GetFullPath($ExtractDirectory, (Get-Location).ProviderPath)
} else {
    $extractionDirectory = Join-Path $smokeRoot ([guid]::NewGuid().ToString('N'))
}
if (Test-Path -LiteralPath $extractionDirectory) {
    throw "Extraction requires a new isolated directory: $extractionDirectory"
}
$null = New-Item -ItemType Directory -Path $extractionDirectory

function Invoke-SmokeCommand([string] $Executable, [string] $Argument, [string] $WorkingDirectory) {
    $startInfo = [Diagnostics.ProcessStartInfo]::new($Executable)
    $startInfo.ArgumentList.Add($Argument)
    $startInfo.WorkingDirectory = $WorkingDirectory
    $startInfo.UseShellExecute = $false
    $startInfo.CreateNoWindow = $true
    $startInfo.RedirectStandardOutput = $true
    $startInfo.RedirectStandardError = $true
    $process = [Diagnostics.Process]::new()
    $process.StartInfo = $startInfo
    try {
        if (-not $process.Start()) { throw "Could not start $Executable." }
        $stdout = $process.StandardOutput.ReadToEndAsync()
        $stderr = $process.StandardError.ReadToEndAsync()
        if (-not $process.WaitForExit(60000)) {
            $process.Kill($true)
            $process.WaitForExit()
            throw "Timed out running $Argument."
        }
        $output = $stdout.GetAwaiter().GetResult() + $stderr.GetAwaiter().GetResult()
        Write-Host $output.TrimEnd()
        return [pscustomobject] @{ ExitCode = $process.ExitCode; Output = $output }
    } finally { $process.Dispose() }
}

try {
    $executableName = if ($isWindowsPackage) { 'localnotion.exe' } else { 'localnotion' }
    if ($isWindowsPackage) {
        [IO.Compression.ZipFile]::ExtractToDirectory($archive, $extractionDirectory)
    } else {
        $fileStream = [IO.File]::OpenRead($archive)
        try {
            $gzip = [IO.Compression.GZipStream]::new($fileStream, [IO.Compression.CompressionMode]::Decompress, $true)
            try {
                $reader = [System.Formats.Tar.TarReader]::new($gzip, $true)
                $foundExecutable = $false
                try {
                    while ($null -ne ($entry = $reader.GetNextEntry())) {
                        if ($entry.Name.TrimStart('.', '/') -ceq 'localnotion') {
                            if (($entry.Mode -band [IO.UnixFileMode]::UserExecute) -eq 0) {
                                throw 'The tar archive does not preserve the localnotion executable bit.'
                            }
                            $foundExecutable = $true
                        }
                    }
                } finally { $reader.Dispose() }
                if (-not $foundExecutable) { throw 'The tar archive does not contain localnotion.' }
            } finally { $gzip.Dispose() }
            $fileStream.Position = 0
            $gzip = [IO.Compression.GZipStream]::new($fileStream, [IO.Compression.CompressionMode]::Decompress, $true)
            try {
                [System.Formats.Tar.TarFile]::ExtractToDirectory($gzip, $extractionDirectory, $false)
            } finally { $gzip.Dispose() }
        } finally { $fileStream.Dispose() }
    }
    foreach ($requiredFile in @($executableName, 'LICENSE', 'COPYRIGHT', 'localnotion-release.json', 'VERSION.txt')) {
        if (-not (Test-Path -LiteralPath (Join-Path $extractionDirectory $requiredFile) -PathType Leaf)) {
            throw "The archive is missing $requiredFile."
        }
    }
    $expectedInformationalVersion = "$Version+build.$BuildNumber.sha.$($SourceRevisionId.ToLowerInvariant())"
    $metadata = Get-Content -LiteralPath (Join-Path $extractionDirectory 'localnotion-release.json') -Raw | ConvertFrom-Json
    if ($metadata.version -cne $Version -or $metadata.buildNumber -ne $BuildNumber -or
        $metadata.commit -cne $SourceRevisionId.ToLowerInvariant() -or $metadata.runtime -cne $Runtime -or
        $metadata.informationalVersion -cne $expectedInformationalVersion) {
        throw 'The packaged release metadata does not match the requested version, build, commit, and runtime.'
    }
    $installationVersion = (Get-Content -LiteralPath (Join-Path $extractionDirectory 'VERSION.txt') -Raw).Trim()
    if ($installationVersion -cne "$Version-build.$BuildNumber") { throw 'VERSION.txt does not match the requested release.' }

    if (-not $SkipExecution) {
        $executable = Join-Path $extractionDirectory $executableName
        $workingDirectory = Join-Path $extractionDirectory '.smoke-work'
        $null = New-Item -ItemType Directory -Path $workingDirectory
        $versionResult = Invoke-SmokeCommand -Executable $executable -Argument '--version' -WorkingDirectory $workingDirectory
        if ($versionResult.ExitCode -ne 0 -or -not $versionResult.Output.Contains($expectedInformationalVersion)) {
            throw "The packaged --version failed or did not report $expectedInformationalVersion (exit $($versionResult.ExitCode))."
        }
        $helpResult = Invoke-SmokeCommand -Executable $executable -Argument '--help' -WorkingDirectory $workingDirectory
        # CommandLineParser reports help using the existing -2 application code (254 on Unix).
        if ($helpResult.ExitCode -notin @(0, -2, 254) -or $helpResult.Output -notmatch '\b(init|pull|status)\b') {
            throw "The packaged --help failed or did not contain CLI commands (exit $($helpResult.ExitCode))."
        }
    }
    Write-Host "Validated $Runtime $Version build $BuildNumber from $archive"
    if ($keepExtraction) { Write-Host "Extracted to $extractionDirectory" }
} finally {
    if (-not $keepExtraction -and (Test-Path -LiteralPath $extractionDirectory)) {
        $resolvedRoot = (Resolve-Path -LiteralPath $smokeRoot).ProviderPath.TrimEnd([IO.Path]::DirectorySeparatorChar) + [IO.Path]::DirectorySeparatorChar
        $resolvedTarget = (Resolve-Path -LiteralPath $extractionDirectory).ProviderPath
        $comparison = if ($IsWindows) { [StringComparison]::OrdinalIgnoreCase } else { [StringComparison]::Ordinal }
        if (-not $resolvedTarget.StartsWith($resolvedRoot, $comparison) -or
            ((Get-Item -LiteralPath $resolvedTarget).Attributes -band [IO.FileAttributes]::ReparsePoint)) {
            throw "Refusing to clean an extraction path outside '$resolvedRoot': $resolvedTarget"
        }
        Remove-Item -LiteralPath $resolvedTarget -Recurse -Force
    }
}
