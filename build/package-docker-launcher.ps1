#requires -Version 7.4
<#
.SYNOPSIS
Packages the Windows Docker command launcher and a version-pinned installer.
.EXAMPLE
pwsh ./build/package-docker-launcher.ps1 -Version 1.5.0 -BuildNumber 17 -SourceRevisionId <full-git-commit>
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory)]
    [ValidateLength(1, 128)]
    [string] $Version,
    [ValidateRange(0, 65534)]
    [int] $BuildNumber = 0,
    [Parameter(Mandatory)]
    [ValidatePattern('^([0-9a-fA-F]{40}|[0-9a-fA-F]{64})$')]
    [string] $SourceRevisionId,
    [string] $OutputDirectory
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
$repoRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$versionPattern = '^(0|[1-9][0-9]{0,4})\.(0|[1-9][0-9]{0,4})\.(0|[1-9][0-9]{0,4})(-(0|[1-9][0-9]*|[0-9]*[A-Za-z-][0-9A-Za-z-]*)(\.(0|[1-9][0-9]*|[0-9]*[A-Za-z-][0-9A-Za-z-]*))*)?$'
if ($Version -cnotmatch $versionPattern) {
    throw 'Version must be a semantic version without build metadata, for example 1.5.0 or 1.5.0-rc.1.'
}
$numericVersion = [version] ($Version.Split('-')[0])
if ($numericVersion.Major -gt 65534 -or $numericVersion.Minor -gt 65534 -or $numericVersion.Build -gt 65534) {
    throw 'The major, minor, and patch version components must be between 0 and 65534.'
}
$SourceRevisionId = $SourceRevisionId.ToLowerInvariant()
$image = "ghcr.io/sphere10/local-notion:$Version"
if (-not $PSBoundParameters.ContainsKey('OutputDirectory')) {
    $OutputDirectory = Join-Path $repoRoot "publish/$Version/artifacts"
}
if ([string]::IsNullOrWhiteSpace($OutputDirectory)) { throw 'OutputDirectory must not be empty.' }
$outputRoot = [IO.Path]::GetFullPath($OutputDirectory, (Get-Location).ProviderPath)
$stageRoot = [IO.Path]::GetFullPath((Join-Path $repoRoot 'publish/.staging'))
$stageDirectory = Join-Path $stageRoot "docker-launcher-$([guid]::NewGuid().ToString('N'))"
$payloadDirectory = Join-Path $stageDirectory 'payload'
$archiveName = 'localnotion-docker-windows.zip'
$archivePath = Join-Path $outputRoot $archiveName
$utf8 = [Text.UTF8Encoding]::new($false)

# Keep the archive independent of the rest of the checkout. In particular, never
# enumerate or copy the private .docker directory, application sources, or images.
$sourceFiles = @(
    'docker/install-cli.ps1',
    'docker/localnotion-docker.ps1',
    'docker/LocalNotionDockerLauncher.cs',
    'LICENSE',
    'COPYRIGHT'
)
foreach ($relativePath in $sourceFiles) {
    if (-not (Test-Path -LiteralPath (Join-Path $repoRoot $relativePath) -PathType Leaf)) {
        throw "A required Docker launcher source file is missing: $relativePath"
    }
}

$installer = @'
#requires -Version 5.1
[CmdletBinding()]
param(
    [string] $InstallDirectory,
    [switch] $NoPath
)

$ErrorActionPreference = 'Stop'
if ($env:OS -ne 'Windows_NT') {
    throw 'This installer supports Windows. Start Docker Desktop in Linux-container mode before installing.'
}
$image = '__BUNDLED_IMAGE__'
$docker = Get-Command docker.exe -CommandType Application -ErrorAction Stop | Select-Object -First 1
Write-Host "Downloading Local Notion image $image"
& $docker.Source pull $image
if ($LASTEXITCODE -ne 0) {
    throw "Could not pull $image. Check Docker Desktop, internet access, and registry access, then retry."
}

$imageAvailable = $false
try {
    & $docker.Source image inspect $image *> $null
    $imageAvailable = $LASTEXITCODE -eq 0
} catch {
    # Windows PowerShell 5.1 can report native stderr as an exception.
    $imageAvailable = $false
}
if (-not $imageAvailable) {
    throw "Docker cannot inspect the downloaded image $image. Check Docker Desktop and retry."
}

# Pull first so the existing installer uses the released image. This bundle does
# not contain the application source tree or a Dockerfile for a fallback build.
$installerArguments = @{ Image = $image }
if ($PSBoundParameters.ContainsKey('InstallDirectory')) {
    if ([string]::IsNullOrWhiteSpace($InstallDirectory)) { throw 'InstallDirectory must not be empty.' }
    $installerArguments.InstallDirectory = $InstallDirectory
}
if ($PSBoundParameters.ContainsKey('NoPath')) {
    $installerArguments.NoPath = $NoPath
}
& (Join-Path $PSScriptRoot 'docker\install-cli.ps1') @installerArguments
'@
$installer = $installer.Replace('__BUNDLED_IMAGE__', $image)

$batchInstaller = @'
@echo off
setlocal
powershell.exe -NoLogo -NoProfile -ExecutionPolicy Bypass -File "%~dp0install.ps1" %*
exit /b %ERRORLEVEL%
'@

$readme = @'
LOCAL NOTION - WINDOWS DOCKER COMMAND LAUNCHER

Release: __VERSION__
Build: __BUILD_NUMBER__
Image: __BUNDLED_IMAGE__
Commit: __COMMIT__

Requirements
- Windows with Windows PowerShell 5.1 and the built-in .NET Framework compiler.
- Docker Desktop installed, running, and set to Linux containers.
- Internet access to download the released image from GitHub Container Registry.

Install
1. Extract this entire ZIP into a writable folder. Keep its docker subfolder.
2. Run install.bat, or open PowerShell in that folder and run:

   powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\install.ps1

The installer downloads the exact image shown above, compiles the small Windows
command launcher, and installs it for the current user. The complete Local Notion
repository, application sources, .NET SDK, and image export are not required.

Optional arguments:
   powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\install.ps1 -InstallDirectory "C:\Tools\LocalNotion" -NoPath

The default installation directory is:
   %LOCALAPPDATA%\Sphere10\LocalNotion\bin

Without -NoPath, the installer adds this directory to your user PATH. Open a new
terminal after installing, then change to your Notion data folder and run:
   localnotion --help

The launcher uses Docker to execute Local Notion. Docker Desktop must remain
running when you use the command. The installer stores launcher configuration
in localnotion-docker.json beside the installed command. Updating this managed
installation selects this release image and preserves its existing state volume
and repository mappings. Background Compose sync services are managed separately.

If you already loaded or pulled the exact image and need to skip the download:
   powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\docker\install-cli.ps1 -Image "__BUNDLED_IMAGE__"

Use that command only after the image is available locally; the bundle does not
contain a Dockerfile for building a missing image.

Uninstall the managed command:
   powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\docker\install-cli.ps1 -Uninstall

Pass the same -InstallDirectory when using a custom location. Docker images,
repository data, and volumes are preserved by uninstalling the command launcher.

LICENSE and COPYRIGHT contain the accompanying licensing notices.
'@
$readme = $readme.Replace('__VERSION__', $Version).
    Replace('__BUILD_NUMBER__', [string] $BuildNumber).
    Replace('__BUNDLED_IMAGE__', $image).
    Replace('__COMMIT__', $SourceRevisionId)

# Identifiers and the exact source whitelist have been checked before creating output.
$null = New-Item -ItemType Directory -Path $outputRoot -Force
$null = New-Item -ItemType Directory -Path (Join-Path $payloadDirectory 'docker') -Force
try {
    foreach ($relativePath in $sourceFiles) {
        Copy-Item -LiteralPath (Join-Path $repoRoot $relativePath) -Destination (Join-Path $payloadDirectory $relativePath)
    }
    [IO.File]::WriteAllText((Join-Path $payloadDirectory 'install.ps1'), $installer.TrimEnd() + [char] 10, $utf8)
    [IO.File]::WriteAllText((Join-Path $payloadDirectory 'install.bat'), ($batchInstaller.TrimEnd() -replace '\r?\n', ([string] [char] 13 + [char] 10)) + [char] 13 + [char] 10, $utf8)
    [IO.File]::WriteAllText((Join-Path $payloadDirectory 'README.txt'), $readme.TrimEnd() + [char] 10, $utf8)
    [ordered] @{
        version = $Version
        buildNumber = $BuildNumber
        commit = $SourceRevisionId
        image = $image
    } | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $payloadDirectory 'release.json') -Encoding utf8NoBOM

    $stagedArchive = Join-Path $stageDirectory $archiveName
    [IO.Compression.ZipFile]::CreateFromDirectory($payloadDirectory, $stagedArchive, [IO.Compression.CompressionLevel]::Optimal, $false)
    Move-Item -LiteralPath $stagedArchive -Destination $archivePath -Force
    Write-Host "Created $archivePath"
} finally {
    if (Test-Path -LiteralPath $stageDirectory) {
        $resolvedRoot = (Resolve-Path -LiteralPath $stageRoot).ProviderPath.TrimEnd([IO.Path]::DirectorySeparatorChar) + [IO.Path]::DirectorySeparatorChar
        $resolvedTarget = (Resolve-Path -LiteralPath $stageDirectory).ProviderPath
        $comparison = if ($IsWindows) { [StringComparison]::OrdinalIgnoreCase } else { [StringComparison]::Ordinal }
        if (-not $resolvedTarget.StartsWith($resolvedRoot, $comparison) -or
            ((Get-Item -LiteralPath $resolvedTarget).Attributes -band [IO.FileAttributes]::ReparsePoint)) {
            throw "Refusing to clean a staging path outside '$resolvedRoot': $resolvedTarget"
        }
        Remove-Item -LiteralPath $resolvedTarget -Recurse -Force
    }
}
