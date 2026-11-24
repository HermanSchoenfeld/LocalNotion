#requires -Version 7.4

<#
.SYNOPSIS
Checks release metadata against valid and invalid Framework package selections.
.DESCRIPTION
Runs the real metadata script in temporary fixtures with a mocked local Git HEAD.
No repository, package cache, application, or external service is changed.
#>
[CmdletBinding()]
param()

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
$repoRoot = Split-Path $PSScriptRoot -Parent
$testRoot = [IO.Path]::GetFullPath((Join-Path $repoRoot 'publish/.framework-package-tests'))
$testDirectory = Join-Path $testRoot ([guid]::NewGuid().ToString('N'))
$fixtureBuild = Join-Path $testDirectory 'build'
$head = '89abcdef0123456789abcdef0123456789abcdef'
$previousOutput = $env:GITHUB_OUTPUT

function Test-FrameworkPackage {
    param([string] $Scenario, [string] $Properties, [string] $Expected, [switch] $Untagged)

    $outputPath = Join-Path $testDirectory "$Scenario.outputs"
    $env:GITHUB_OUTPUT = $outputPath
    [IO.File]::WriteAllText((Join-Path $testDirectory 'Directory.Build.props'), "<Project><PropertyGroup>$Properties</PropertyGroup></Project>", [Text.UTF8Encoding]::new($false))
    $calls = [Collections.Generic.List[string]]::new()
    function git {
        $arguments = @($args)
        if ($arguments.Count -ne 4 -or $arguments[0] -cne '-C' -or $arguments[1] -cne $testDirectory -or
            $arguments[2] -cne 'rev-parse' -or $arguments[3] -cne 'HEAD') {
            throw 'The metadata fixture attempted an unexpected Git operation.'
        }
        $calls.Add('rev-parse HEAD')
        $global:LASTEXITCODE = 0
        return $head
    }

    $failure = $null
    $result = $null
    # A master or pull-request build passes no tag; a release build passes its v* tag.
    $release = if ($Untagged) { @{} } else { @{ Tag = 'v1.7.0' } }
    try { $result = & (Join-Path $fixtureBuild 'ci-metadata.ps1') -BuildNumber 17 @release }
    catch { $failure = $_.Exception.Message }
    if ($Expected) {
        if ($failure) { throw "Valid Framework package '$Scenario' was rejected: $failure" }
        if ($result.framework_version -cne $Expected -or $result.sha -cne $head -or $result.version -cne '1.7.0' -or
            $result.build_number -ne 17 -or $result.publish -cne 'false' -or $calls.Count -ne 1) {
            throw "Framework package or release identity was lost for '$Scenario'."
        }
        if ("framework_version=$Expected" -cnotin @(Get-Content -LiteralPath $outputPath) -or "sha=$head" -cnotin @(Get-Content -LiteralPath $outputPath)) {
            throw "The Framework package for '$Scenario' was not written to the workflow outputs."
        }
    } else {
        if ($null -eq $failure -or $failure -notmatch 'Framework|UseSphere10FrameworkSource' -or $calls.Count -ne 0 -or (Test-Path -LiteralPath $outputPath)) {
            throw "Invalid Framework package selection '$Scenario' was accepted or produced release metadata."
        }
    }
    Write-Host "Passed Framework package metadata: $Scenario."
}

function New-Properties {
    param([string] $Version, [string] $Source = 'false')
    "<UseSphere10FrameworkSource Condition=`"'`$(UseSphere10FrameworkSource)' == ''`">$Source</UseSphere10FrameworkSource><Sphere10FrameworkVersion Condition=`"'`$(Sphere10FrameworkVersion)' == ''`">$Version</Sphere10FrameworkVersion>"
}

try {
    $null = New-Item -ItemType Directory -Path $fixtureBuild -Force
    Copy-Item -LiteralPath (Join-Path $repoRoot 'build/ci-metadata.ps1'), (Join-Path $repoRoot 'build/platforms.json') -Destination $fixtureBuild
    [IO.File]::WriteAllText((Join-Path $testDirectory 'Version.props'), '<Project><PropertyGroup><ReleaseVersion>1.7.0</ReleaseVersion></PropertyGroup></Project>', [Text.UTF8Encoding]::new($false))
    Test-FrameworkPackage -Scenario 'stable' -Properties (New-Properties '3.2.0') -Expected '3.2.0'
    Test-FrameworkPackage -Scenario 'ci-prerelease-on-master' -Properties (New-Properties '3.2.0-ci.163.43664782937.1') -Expected '3.2.0-ci.163.43664782937.1' -Untagged
    Test-FrameworkPackage -Scenario 'ci-prerelease-release' -Properties (New-Properties '3.2.0-ci.163.43664782937.1') -Expected ''
    Test-FrameworkPackage -Scenario 'surrounding-whitespace' -Properties (New-Properties ' 3.2.0 ') -Expected '3.2.0'
    Test-FrameworkPackage -Scenario 'source-default' -Properties (New-Properties '3.2.0' 'true') -Expected ''
    Test-FrameworkPackage -Scenario 'missing-version' -Properties '<UseSphere10FrameworkSource>false</UseSphere10FrameworkSource>' -Expected ''
    Test-FrameworkPackage -Scenario 'duplicate-version' -Properties ((New-Properties '3.2.0') + '<Sphere10FrameworkVersion>3.2.1</Sphere10FrameworkVersion>') -Expected ''
    Test-FrameworkPackage -Scenario 'empty-version' -Properties (New-Properties '') -Expected ''
    Test-FrameworkPackage -Scenario 'two-components' -Properties (New-Properties '3.2') -Expected ''
    Test-FrameworkPackage -Scenario 'leading-zero' -Properties (New-Properties '3.02.0') -Expected ''
    Test-FrameworkPackage -Scenario 'version-range' -Properties (New-Properties '[3.2.0]') -Expected ''
    Test-FrameworkPackage -Scenario 'floating' -Properties (New-Properties '3.2.*') -Expected ''
} finally {
    $env:GITHUB_OUTPUT = $previousOutput
    if (Test-Path -LiteralPath $testDirectory) {
        $resolvedRoot = (Resolve-Path -LiteralPath $testRoot).ProviderPath.TrimEnd([IO.Path]::DirectorySeparatorChar) + [IO.Path]::DirectorySeparatorChar
        $resolvedTarget = (Resolve-Path -LiteralPath $testDirectory).ProviderPath
        $comparison = if ($IsWindows) { [StringComparison]::OrdinalIgnoreCase } else { [StringComparison]::Ordinal }
        if (-not $resolvedTarget.StartsWith($resolvedRoot, $comparison) -or
            ((Get-Item -LiteralPath $resolvedTarget).Attributes -band [IO.FileAttributes]::ReparsePoint)) {
            throw "Refusing to clean a test directory outside '$resolvedRoot': $resolvedTarget"
        }
        Remove-Item -LiteralPath $resolvedTarget -Recurse -Force
    }
}
