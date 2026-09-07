# Run after: dotnet build LocalNotion.CLI -c Release
# Uses native Windows Git and Local Notion, synthetic data, and a local bare remote.
# No Docker, Notion credentials, network remotes, or ownership/ACL changes are used.
[CmdletBinding()]
param(
    [string]$CliPath,
    [switch]$KeepFixture
)

$ErrorActionPreference = 'Stop'
if ($env:OS -ne 'Windows_NT') { throw 'Run this regression on Windows with Git for Windows installed.' }
if ([string]::IsNullOrWhiteSpace($CliPath)) {
    $CliPath = Join-Path $PSScriptRoot '..\LocalNotion.CLI\bin\Release\net8.0\localnotion.dll'
}
$CliPath = [IO.Path]::GetFullPath($CliPath)
if (-not (Test-Path -LiteralPath $CliPath -PathType Leaf)) {
    throw "Build LocalNotion.CLI in Release first, or pass -CliPath: $CliPath"
}
$git = (Get-Command git.exe -CommandType Application | Select-Object -First 1).Source
if ([IO.Path]::GetExtension($CliPath) -eq '.dll') {
    $cliExecutable = (Get-Command dotnet.exe -CommandType Application | Select-Object -First 1).Source
    $cliPrefix = @($CliPath)
} else {
    $cliExecutable = $CliPath
    $cliPrefix = @()
}

$tempRoot = [IO.Path]::GetFullPath([IO.Path]::GetTempPath()).TrimEnd('\')
$fixtureRoot = Join-Path $tempRoot ('localnotion-git-ownership-' + [Guid]::NewGuid().ToString('N'))
# Build the Unicode name without depending on the script file's encoding in Windows PowerShell 5.1.
$repo = Join-Path $fixtureRoot ('repository with spaces ' + [char]0x00e9)
$remote = Join-Path $fixtureRoot ('local remote ' + [char]0x00e9 + '.git')
$unselectedRepo = Join-Path $fixtureRoot 'unselected repository'
$privateHome = Join-Path $fixtureRoot 'home'
$globalConfig = Join-Path $privateHome 'gitconfig'
$systemConfig = Join-Path $privateHome 'system-gitconfig'

function ConvertTo-NativeArgument([string]$Value) {
    # Windows CommandLineToArgvW/CRT quoting, also supported on Windows PowerShell 5.1.
    $result = New-Object Text.StringBuilder
    [void]$result.Append('"')
    $slashes = 0
    foreach ($character in $Value.ToCharArray()) {
        if ($character -eq '\') { $slashes++; continue }
        if ($character -eq '"') {
            [void]$result.Append('\', 2 * $slashes + 1)
            [void]$result.Append('"')
        } else {
            [void]$result.Append('\', $slashes)
            [void]$result.Append($character)
        }
        $slashes = 0
    }
    [void]$result.Append('\', 2 * $slashes)
    [void]$result.Append('"')
    $result.ToString()
}

function Invoke-FixtureProcess {
    param(
        [string]$Executable,
        [string[]]$Arguments,
        [switch]$DifferentOwner
    )
    $start = New-Object Diagnostics.ProcessStartInfo
    $start.FileName = $Executable
    $start.Arguments = (($Arguments | ForEach-Object { ConvertTo-NativeArgument $_ }) -join ' ')
    $start.WorkingDirectory = $fixtureRoot
    $start.UseShellExecute = $false
    $start.CreateNoWindow = $true
    $start.RedirectStandardOutput = $true
    $start.RedirectStandardError = $true
    # Do not inherit credentials, repository redirections, or safe.directory overrides.
    foreach ($key in @($start.EnvironmentVariables.Keys)) {
        if ($key -match '^(GIT_|NOTION_|LOCALNOTION_)') {
            $start.EnvironmentVariables.Remove($key)
        }
    }
    $start.EnvironmentVariables['HOME'] = $privateHome
    $start.EnvironmentVariables['USERPROFILE'] = $privateHome
    $start.EnvironmentVariables['APPDATA'] = (Join-Path $privateHome 'AppData\Roaming')
    $start.EnvironmentVariables['LOCALAPPDATA'] = (Join-Path $privateHome 'AppData\Local')
    $start.EnvironmentVariables['XDG_CONFIG_HOME'] = (Join-Path $privateHome '.config')
    $start.EnvironmentVariables['GIT_CONFIG_GLOBAL'] = $globalConfig
    $start.EnvironmentVariables['GIT_CONFIG_SYSTEM'] = $systemConfig
    $start.EnvironmentVariables['GIT_CONFIG_NOSYSTEM'] = '1'
    $start.EnvironmentVariables['GIT_TERMINAL_PROMPT'] = '0'
    $start.EnvironmentVariables['GIT_ALLOW_PROTOCOL'] = 'file'
    if ($DifferentOwner) {
        # Git's own test switch forces the owner check to fail without changing any file ACLs.
        $start.EnvironmentVariables['GIT_TEST_ASSUME_DIFFERENT_OWNER'] = '1'
    }
    $process = New-Object Diagnostics.Process
    $process.StartInfo = $start
    try {
        [void]$process.Start()
        $stdout = $process.StandardOutput.ReadToEndAsync()
        $stderr = $process.StandardError.ReadToEndAsync()
        if (-not $process.WaitForExit(120000)) {
            $process.Kill()
            throw 'Fixture command exceeded two minutes.'
        }
        [pscustomobject]@{
            ExitCode = $process.ExitCode
            Output = $stdout.GetAwaiter().GetResult() + $stderr.GetAwaiter().GetResult()
        }
    } finally {
        $process.Dispose()
    }
}

function Assert-Command {
    param($Result, [string]$Description)
    # Local Notion currently logs some Git failures but returns exit code zero.
    if ($Result.ExitCode -ne 0 -or $Result.Output -match '\[Error\]|fatal:|dubious ownership') {
        throw "$Description failed (exit $($Result.ExitCode)):$([Environment]::NewLine)$($Result.Output)"
    }
}

function Invoke-FixtureGit([string[]]$Arguments) {
    $result = Invoke-FixtureProcess -Executable $git -Arguments $Arguments
    Assert-Command $result ('git ' + ($Arguments -join ' '))
    $result.Output.Trim()
}

function Assert-OwnershipRejected([string]$Repository) {
    $result = Invoke-FixtureProcess -Executable $git -Arguments @('-C', $Repository, 'status', '--porcelain') -DifferentOwner
    if ($result.ExitCode -eq 0 -or $result.Output -notmatch 'detected dubious ownership') {
        throw "Native Git must reject the synthetic owner mismatch. Check GIT_TEST_ASSUME_DIFFERENT_OWNER support:$([Environment]::NewLine)$($result.Output)"
    }
}

try {
    foreach ($directory in @($repo, $privateHome, (Join-Path $privateHome 'AppData\Roaming'),
        (Join-Path $privateHome 'AppData\Local'), (Join-Path $privateHome '.config'),
        (Join-Path $privateHome 'empty-hooks'), (Join-Path $privateHome 'empty-template'))) {
        [void](New-Item -ItemType Directory -Path $directory -Force)
    }
    $utf8 = New-Object Text.UTF8Encoding($false)
    $hooks = (Join-Path $privateHome 'empty-hooks').Replace('\', '/')
    $template = (Join-Path $privateHome 'empty-template').Replace('\', '/')
    [IO.File]::WriteAllText($globalConfig, @"
[user]
    name = Local Notion Regression
    email = regression@example.invalid
[init]
    defaultBranch = main
    templateDir = "$template"
[core]
    hooksPath = "$hooks"
    autocrlf = false
[commit]
    gpgSign = false
"@, $utf8)
    [IO.File]::WriteAllText($systemConfig, "# Private unused system config fixture\n", $utf8)
    $globalBefore = (Get-FileHash -LiteralPath $globalConfig -Algorithm SHA256).Hash
    $systemBefore = (Get-FileHash -LiteralPath $systemConfig -Algorithm SHA256).Hash

    [void](Invoke-FixtureGit @('init', '--bare', $remote))
    [void](Invoke-FixtureGit @('init', $repo))
    [void](Invoke-FixtureGit @('init', $unselectedRepo))
    [void](Invoke-FixtureGit @('-C', $repo, 'remote', 'add', 'origin', $remote))
    [void](Invoke-FixtureGit @('-C', $repo, 'config', 'branch.main.remote', 'origin'))
    [void](Invoke-FixtureGit @('-C', $repo, 'config', 'branch.main.merge', 'refs/heads/main'))
    # A real remote server does not inherit the client's ownership-test environment.
    # Clear only that test flag for this fixture's local receive-pack process.
    [void](Invoke-FixtureGit @('-C', $repo, 'config', 'remote.origin.receivepack',
        'env -u GIT_TEST_ASSUME_DIFFERENT_OWNER git-receive-pack'))

    Assert-OwnershipRejected $repo
    Write-Host 'PASS: Native Git reproduces dubious ownership before running Local Notion.'

    $init = Invoke-FixtureProcess -Executable $cliExecutable -Arguments ($cliPrefix + @(
        'init', '--path', ($repo + '\'), '--git', '--git-push')) -DifferentOwner
    Assert-Command $init 'Local Notion init with Git commit and push'
    $headAfterInit = Invoke-FixtureGit @('-C', $repo, 'rev-parse', 'HEAD')
    $remoteAfterInit = Invoke-FixtureGit @('--git-dir', $remote, 'rev-parse', 'refs/heads/main')
    if ($headAfterInit -ne $remoteAfterInit) { throw 'The initialization commit was not pushed to the local remote.' }
    [void](Invoke-FixtureGit @('--git-dir', $remote, 'cat-file', '-e', 'main:.localnotion/registry.json'))
    Write-Host 'PASS: Native Local Notion initializes, stages, commits, and pushes with the owner mismatch.'

    [IO.File]::WriteAllText((Join-Path $repo 'ownership-regression.txt'), 'change committed by clean', $utf8)
    $clean = Invoke-FixtureProcess -Executable $cliExecutable -Arguments ($cliPrefix + @(
        'clean', '--path', $repo)) -DifferentOwner
    Assert-Command $clean 'Local Notion clean with Git commit and push'
    $headAfterClean = Invoke-FixtureGit @('-C', $repo, 'rev-parse', 'HEAD')
    $remoteAfterClean = Invoke-FixtureGit @('--git-dir', $remote, 'rev-parse', 'refs/heads/main')
    if ($headAfterClean -eq $headAfterInit -or $headAfterClean -ne $remoteAfterClean) {
        throw 'The subsequent change was not committed and pushed.'
    }
    $remoteText = Invoke-FixtureGit @('--git-dir', $remote, 'show', 'main:ownership-regression.txt')
    if ($remoteText -ne 'change committed by clean') { throw 'The remote does not contain the expected new content.' }
    Write-Host 'PASS: A subsequent native command commits and pushes the actual changed content.'

    Assert-OwnershipRejected $repo
    Assert-OwnershipRejected $unselectedRepo
    if ((Get-FileHash -LiteralPath $globalConfig -Algorithm SHA256).Hash -ne $globalBefore -or
        (Get-FileHash -LiteralPath $systemConfig -Algorithm SHA256).Hash -ne $systemBefore) {
        throw 'The private global/system Git configuration was modified.'
    }
    $localSafe = Invoke-FixtureProcess -Executable $git -Arguments @(
        '-C', $repo, 'config', '--local', '--get-all', 'safe.directory')
    if ($localSafe.ExitCode -ne 1 -or $localSafe.Output.Trim()) {
        throw 'A persistent repository safe.directory setting was written.'
    }
    Write-Host 'PASS: Trust is command-scoped; ordinary Git still rejects both repositories and Git config is unchanged.'
} finally {
    if ($KeepFixture) {
        Write-Host "Fixture retained at: $fixtureRoot"
    } elseif (Test-Path -LiteralPath $fixtureRoot) {
        $resolvedFixture = (Resolve-Path -LiteralPath $fixtureRoot).ProviderPath
        $allowedPrefix = $tempRoot + '\localnotion-git-ownership-'
        if (-not $resolvedFixture.StartsWith($allowedPrefix, [StringComparison]::OrdinalIgnoreCase) -or
            $resolvedFixture -ne [IO.Path]::GetFullPath($fixtureRoot)) {
            throw "Refusing cleanup outside the unique test fixture: $resolvedFixture"
        }
        Remove-Item -LiteralPath $resolvedFixture -Recurse -Force
    }
}
