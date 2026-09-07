#Requires -Version 5.1
[CmdletBinding()]
param(
    [string]$InstallRoot,
    [switch]$NoPath
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$metadataPath = Join-Path $PSScriptRoot 'localnotion-release.json'
if (!(Test-Path -LiteralPath $metadataPath -PathType Leaf) -or
    !(Test-Path -LiteralPath (Join-Path $PSScriptRoot 'localnotion.exe') -PathType Leaf)) {
    throw 'Extract the complete Windows release archive before running this installer.'
}

$metadata = Get-Content -LiteralPath $metadataPath -Raw | ConvertFrom-Json
$version = [string]$metadata.version
$buildNumber = [string]$metadata.buildNumber
if ($version -notmatch '^[A-Za-z0-9][A-Za-z0-9._+-]*$' -or $buildNumber -notmatch '^[0-9]+$') {
    throw 'Release metadata must contain a valid version and numeric buildNumber.'
}

if ([string]::IsNullOrWhiteSpace($InstallRoot)) {
    if ([string]::IsNullOrWhiteSpace($env:LOCALAPPDATA)) {
        throw 'LOCALAPPDATA is unavailable. Supply -InstallRoot with a directory for your user.'
    }
    $InstallRoot = Join-Path $env:LOCALAPPDATA 'Programs\LocalNotion'
}
$InstallRoot = [IO.Path]::GetFullPath([Environment]::ExpandEnvironmentVariables($InstallRoot))
if (!$NoPath -and $InstallRoot.Contains(';')) {
    throw 'InstallRoot cannot contain a semicolon when updating PATH. Choose another root or use -NoPath.'
}
$installationPath = Join-Path $InstallRoot "$version-build.$buildNumber"
$sourcePrefix = $PSScriptRoot.TrimEnd('\') + '\'
if ($installationPath.StartsWith($sourcePrefix, [StringComparison]::OrdinalIgnoreCase)) {
    throw 'Choose an installation root outside the extracted archive directory.'
}
if (Test-Path -LiteralPath $installationPath) {
    throw "The version directory already exists: $installationPath. Existing installations are preserved."
}

New-Item -ItemType Directory -Path $InstallRoot -Force | Out-Null
New-Item -ItemType Directory -Path $installationPath | Out-Null
# Native libraries and supporting files must stay beside the executable.
Get-ChildItem -LiteralPath $PSScriptRoot -Force |
    Copy-Item -Destination $installationPath -Recurse -Force

if (!$NoPath) {
    $userPath = [Environment]::GetEnvironmentVariable('Path', 'User')
    $otherEntries = @()
    if (![string]::IsNullOrEmpty($userPath)) {
        $otherEntries = @($userPath.Split(';') | Where-Object {
            ![string]::Equals($_.TrimEnd('\'), $installationPath.TrimEnd('\'), [StringComparison]::OrdinalIgnoreCase)
        })
    }
    $newUserPath = (@($installationPath) + $otherEntries) -join ';'
    [Environment]::SetEnvironmentVariable('Path', $newUserPath, 'User')
    # Notify Explorer so new terminal windows receive the updated user PATH.
    if (-not ('LocalNotionNativeEnvironmentNotification' -as [type])) {
        Add-Type @'
using System;
using System.Runtime.InteropServices;
public static class LocalNotionNativeEnvironmentNotification {
    [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    public static extern IntPtr SendMessageTimeout(IntPtr window, uint message, UIntPtr wParam,
        string lParam, uint flags, uint timeout, out UIntPtr result);
}
'@
    }
    $notificationResult = [UIntPtr]::Zero
    [LocalNotionNativeEnvironmentNotification]::SendMessageTimeout(
        [IntPtr]0xffff, 0x001a, [UIntPtr]::Zero, 'Environment', 2, 5000, [ref]$notificationResult) | Out-Null
}

Write-Output "Installed Local Notion $version (build $buildNumber) in $installationPath"
if ($NoPath) {
    Write-Output 'PATH was not changed. Run localnotion.exe from the installation directory.'
} else {
    Write-Output 'Added the installation directory to your user PATH. Open a new terminal to use localnotion.'
}
