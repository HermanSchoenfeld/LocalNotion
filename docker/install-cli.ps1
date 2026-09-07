[CmdletBinding()]
param(
    [string]$Image = 'local-notion:latest',
    [string]$InstallDirectory = (Join-Path $env:LOCALAPPDATA 'Sphere10\LocalNotion\bin'),
    [switch]$BuildImage,
    [switch]$NoPath,
    [switch]$Uninstall
)

$ErrorActionPreference = 'Stop'
if ($env:OS -ne 'Windows_NT') { throw 'This command installer supports Windows. See docker/README.md for direct Docker commands on other systems.' }
$installRoot = [System.IO.Path]::GetFullPath($InstallDirectory)
$manifestPath = Join-Path $installRoot 'localnotion-docker-install.json'
$configPath = Join-Path $installRoot 'localnotion-docker.json'
$projectRoot = Split-Path -Parent $PSScriptRoot
$utf8 = [System.Text.UTF8Encoding]::new($false)
$managedFiles = @('localnotion.exe','localnotion-docker.ps1','localnotion-docker.json','localnotion-docker-install.json')

function Set-CommandPath {
    param([bool]$Remove)
    $userPath = [Environment]::GetEnvironmentVariable('Path', 'User')
    $entries = @($userPath -split ';' | Where-Object {
        -not [string]::IsNullOrWhiteSpace($_) -and
        [Environment]::ExpandEnvironmentVariables($_).TrimEnd('\','/') -ine $installRoot.TrimEnd('\','/')
    })
    if (-not $Remove) { $entries = @($installRoot) + $entries }
    [Environment]::SetEnvironmentVariable('Path', ($entries -join ';'), 'User')
    if ($Remove) {
        $env:Path = (($env:Path -split ';' | Where-Object { $_.TrimEnd('\','/') -ine $installRoot.TrimEnd('\','/') }) -join ';')
    } else {
        $env:Path = $installRoot + ';' + $env:Path
    }
    # Let Explorer propagate the changed user environment to newly opened terminals.
    if (-not ('LocalNotionEnvironmentNotification' -as [type])) {
        Add-Type @'
using System;
using System.Runtime.InteropServices;
public static class LocalNotionEnvironmentNotification {
    [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    public static extern IntPtr SendMessageTimeout(IntPtr window, uint message, UIntPtr wParam,
        string lParam, uint flags, uint timeout, out UIntPtr result);
}
'@
    }
    $notificationResult = [UIntPtr]::Zero
    [LocalNotionEnvironmentNotification]::SendMessageTimeout(
        [IntPtr]0xffff, 0x001a, [UIntPtr]::Zero, 'Environment', 2, 5000, [ref]$notificationResult) | Out-Null
}

if (Test-Path -LiteralPath $manifestPath) {
    $manifest = Get-Content -LiteralPath $manifestPath -Raw -Encoding UTF8 | ConvertFrom-Json
    if ($manifest.product -ne 'Local Notion Docker CLI' -or $manifest.installDirectory -ine $installRoot) {
        throw 'The installation manifest does not match this launcher directory.'
    }
} else {
    foreach ($file in $managedFiles) {
        if (Test-Path -LiteralPath (Join-Path $installRoot $file)) {
            throw "The target contains an unmanaged file. Choose another -InstallDirectory: $file"
        }
    }
}

if ($Uninstall) {
    if (-not (Test-Path -LiteralPath $manifestPath)) { throw 'There is no managed Local Notion Docker CLI installation in that directory.' }
    if (-not $NoPath) { Set-CommandPath -Remove $true }
    foreach ($file in $managedFiles) {
        $installedFile = Join-Path $installRoot $file
        if (Test-Path -LiteralPath $installedFile -PathType Leaf) { Remove-Item -LiteralPath $installedFile }
    }
    Write-Host 'Removed the command launcher. Docker images, repository data, and volumes are preserved.'
    exit 0
}

$docker = Get-Command docker.exe -CommandType Application -ErrorAction Stop | Select-Object -First 1
$compiler = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
if (-not (Test-Path -LiteralPath $compiler)) {
    $compiler = Join-Path $env:WINDIR 'Microsoft.NET\Framework\v4.0.30319\csc.exe'
}
if (-not (Test-Path -LiteralPath $compiler)) { throw 'The built-in Windows .NET Framework C# compiler was not found.' }

$config = if (Test-Path -LiteralPath $configPath) {
    Get-Content -LiteralPath $configPath -Raw -Encoding UTF8 | ConvertFrom-Json
} else {
    [pscustomobject]@{ image = $Image; stateVolume = 'local-notion-state'; repositories = @() }
}
if ($PSBoundParameters.ContainsKey('Image')) { $config.image = $Image }
$imageToInstall = $config.image
if ([string]::IsNullOrWhiteSpace($imageToInstall)) { throw 'An image name is required.' }

$imageExists = $false
try {
    & $docker.Source image inspect $imageToInstall *> $null
    $imageExists = $LASTEXITCODE -eq 0
} catch {
    # Windows PowerShell 5.1 can turn Docker's missing-image stderr into an exception.
    $imageExists = $false
}
if ($BuildImage -or -not $imageExists) {
    $revision = 'unknown'
    $gitCommand = Get-Command git.exe -CommandType Application -ErrorAction SilentlyContinue | Select-Object -First 1
    if ($gitCommand) {
        try {
            $detectedRevision = & $gitCommand.Source -C $projectRoot rev-parse HEAD 2>$null
            if ($LASTEXITCODE -eq 0) { $revision = $detectedRevision }
        } catch { $revision = 'unknown' }
    }
    & $docker.Source build --platform linux/amd64 --build-arg "VCS_REF=$revision" -t $imageToInstall $projectRoot
    if ($LASTEXITCODE -ne 0) { throw 'The local Docker image could not be built. Start Docker Desktop in Linux-container mode and retry.' }
}

$sourceScript = Join-Path $PSScriptRoot 'localnotion-docker.ps1'
$sourceLauncher = Join-Path $PSScriptRoot 'LocalNotionDockerLauncher.cs'
if (-not (Test-Path -LiteralPath $sourceScript) -or -not (Test-Path -LiteralPath $sourceLauncher)) {
    throw 'The command launcher source files are missing.'
}
$buildRoot = Join-Path $projectRoot '.docker/cli-build'
[System.IO.Directory]::CreateDirectory($buildRoot) | Out-Null
$compiledLauncher = Join-Path $buildRoot 'localnotion.exe'
& $compiler /nologo /target:exe /optimize+ /r:System.Runtime.Serialization.dll ("/out:" + $compiledLauncher) $sourceLauncher
if ($LASTEXITCODE -ne 0) { throw 'The command launcher could not be compiled.' }

# Register only this exact imported sample with its separate secret. Other folders
# use their own registry credential or an explicitly selected LOCALNOTION_TOKEN_FILE.
$sampleData = Join-Path $projectRoot '.docker/data'
$sampleToken = Join-Path $projectRoot '.docker/secrets/notion-token'
if ((Test-Path -LiteralPath (Join-Path $sampleData '.localnotion/registry.json')) -and (Test-Path -LiteralPath $sampleToken -PathType Leaf)) {
    $sampleData = [System.IO.Path]::GetFullPath($sampleData)
    $existingMappings = @($config.repositories | Where-Object { $_.hostPath -ine $sampleData })
    $config.repositories = $existingMappings + @([pscustomobject]@{hostPath = $sampleData; tokenFile = $sampleToken})
}

[System.IO.Directory]::CreateDirectory($installRoot) | Out-Null
Copy-Item -LiteralPath $compiledLauncher -Destination (Join-Path $installRoot 'localnotion.exe') -Force
Copy-Item -LiteralPath $sourceScript -Destination (Join-Path $installRoot 'localnotion-docker.ps1') -Force
[System.IO.File]::WriteAllText($configPath, ($config | ConvertTo-Json -Depth 10), $utf8)
$manifest = [pscustomobject]@{
    product = 'Local Notion Docker CLI'
    installDirectory = $installRoot
    installedAt = [DateTime]::UtcNow.ToString('o')
}
[System.IO.File]::WriteAllText($manifestPath, ($manifest | ConvertTo-Json), $utf8)
if (-not $NoPath) { Set-CommandPath -Remove $false }

Write-Host "Installed localnotion at $installRoot"
Write-Host "Docker image: $imageToInstall"
Write-Host 'Open a new terminal, change to a Notion data folder, and run localnotion --help.'
Write-Host 'The background Compose sync service is managed separately.'
