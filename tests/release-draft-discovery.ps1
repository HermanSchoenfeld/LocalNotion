#requires -Version 7.4

<#
.SYNOPSIS
Checks release discovery when the tag endpoint cannot return an authenticated draft.
#>
[CmdletBinding()]
param()
Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
$repoRoot = Split-Path $PSScriptRoot -Parent
$tokens = $null
$errors = $null
$ast = [Management.Automation.Language.Parser]::ParseFile(
    (Join-Path $repoRoot 'build/publish-github-release.ps1'), [ref]$tokens, [ref]$errors)
if ($errors.Count) { throw 'Cannot parse the release publisher.' }
$lookup = $ast.Find({
    param($node)
    $node -is [Management.Automation.Language.FunctionDefinitionAst] -and
    $node.Name -ceq 'Get-ReleaseForTag'
}, $true)
if ($null -eq $lookup) { throw 'Cannot find the release lookup helper.' }

function Test-ReleaseLookup {
    param([string] $Scenario, [string] $Definition)
    $repository = 'Sphere10/LocalNotion'
    $tag = 'v1.5.0'
    $calls = [Collections.Generic.List[string]]::new()
    function Get-GitHubJson {
        param([string] $Endpoint, [switch] $AllowNotFound)
        $calls.Add($Endpoint)
        if ($Endpoint.EndsWith("/tags/$tag")) {
            if (-not $AllowNotFound) { throw 'The published-tag lookup must tolerate a confirmed 404.' }
            if ($Scenario -eq 'published') { return [pscustomobject]@{ id = 1; tag_name = $tag; draft = $false } }
            return $null # A real 404 from the published-tag endpoint.
        }
        if ($AllowNotFound) { throw 'List errors must never be treated as an absent draft.' }
        if ($Scenario -eq 'list-failure') { throw 'GitHub list request failed: HTTP 403.' }
        if ($Scenario -eq 'missing') { return }
        if ($Scenario -eq 'paginated-draft' -and $Endpoint.EndsWith('page=1')) {
            foreach ($index in 1..100) { [pscustomobject]@{ id = 1000 + $index; tag_name = "v0.0.$index"; draft = $false } }
            return
        }
        [pscustomobject]@{ id = 2; tag_name = $tag; draft = $true }
        if ($Scenario -eq 'duplicates') { [pscustomobject]@{ id = 3; tag_name = $tag; draft = $true } }
    }
    . ([scriptblock]::Create($Definition))
    $failure = $null
    $result = $null
    try { $result = Get-ReleaseForTag -Require:($Scenario -eq 'missing') }
    catch { $failure = $_.Exception.Message }
    switch ($Scenario) {
        'published' {
            if ($failure -or $result.id -ne 1 -or $calls.Count -ne 1) { throw 'Published release lookup failed.' }
        }
        'draft' {
            if ($failure -or $result.id -ne 2 -or -not $result.draft) { throw 'Existing draft was not found after the tag endpoint returned 404.' }
        }
        'paginated-draft' {
            if ($failure -or $result.id -ne 2 -or $calls.Count -ne 3) { throw 'Draft on the second page was not found.' }
        }
        'duplicates' {
            if ($failure -notlike 'Multiple releases or drafts*') { throw 'Duplicate drafts were not rejected.' }
        }
        'missing' {
            if ($failure -notlike 'Could not find*') { throw 'Required missing draft was not rejected.' }
        }
        'list-failure' {
            if ($failure -notlike '*HTTP 403*') { throw 'A failed list request was mistaken for a missing draft.' }
        }
    }
    Write-Host "Passed release discovery: $Scenario."
}

foreach ($scenario in @('published', 'draft', 'paginated-draft', 'duplicates', 'missing', 'list-failure')) {
    Test-ReleaseLookup -Scenario $scenario -Definition $lookup.Extent.Text
}
