#requires -Version 7.4

<#
.SYNOPSIS
Checks release executable lookup when PATH exposes multiple copies of a command.
.DESCRIPTION
Evaluates the real lookup expressions from the release scripts against mocked
Get-Command results. No release script, executable, or external service is run.
#>
[CmdletBinding()]
param()

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
$repoRoot = Split-Path $PSScriptRoot -Parent
$cases = @(
    @{ Script = 'build/publish-github-release.ps1'; Variable = 'gh'; Command = 'gh' },
    @{ Script = 'build/publish-github-release.ps1'; Variable = 'docker'; Command = 'docker' },
    @{ Script = 'build/release.ps1'; Variable = 'gitExecutable'; Command = 'git' }
)

function Test-ResolutionExpression {
    param([string] $Expression, [string] $ExpectedCommand, [string[]] $Paths)

    # A local function shadows only this test invocation, reproducing the two
    # application results returned for /usr/bin and /bin on the Linux runner.
    function Get-Command {
        [CmdletBinding()]
        param([Parameter(Position = 0)][string] $Name, [string] $CommandType)
        if ($Name -cne $ExpectedCommand -or $CommandType -cne 'Application') {
            throw "Unexpected command lookup: $Name ($CommandType)."
        }
        foreach ($path in $Paths) { [pscustomobject]@{ Source = $path } }
    }

    $resolved = & ([scriptblock]::Create($Expression))
    if ($resolved -isnot [string] -or $resolved -cne $Paths[0]) {
        throw "$ExpectedCommand must resolve to one executable string: $($Paths[0])."
    }
}

foreach ($case in $cases) {
    $tokens = $null
    $errors = $null
    $ast = [Management.Automation.Language.Parser]::ParseFile(
        (Join-Path $repoRoot $case.Script), [ref]$tokens, [ref]$errors)
    if ($errors.Count) { throw "Cannot parse $($case.Script)." }
    $assignments = @($ast.FindAll({
        param($node)
        $node -is [Management.Automation.Language.AssignmentStatementAst] -and
        $node.Left -is [Management.Automation.Language.VariableExpressionAst] -and
        $node.Left.VariablePath.UserPath -ceq $case.Variable
    }, $true))
    if ($assignments.Count -ne 1) { throw "Expected one executable assignment for $($case.Variable)." }
    $expression = $assignments[0].Right.Extent.Text
    Test-ResolutionExpression -Expression $expression -ExpectedCommand $case.Command -Paths @(
        "/usr/bin/$($case.Command)", "/bin/$($case.Command)")
    Test-ResolutionExpression -Expression $expression -ExpectedCommand $case.Command -Paths @(
        "C:\Program Files\Tools\$($case.Command).exe", "C:\Other Tools\$($case.Command).exe")
    Write-Host "Passed $($case.Script): $($case.Variable), duplicate Linux paths and Windows paths with spaces."
}
