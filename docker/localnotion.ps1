[CmdletBinding()]
param(
    [ValidateSet('Install','Import','Configure','Start','Stop','Restart','Logs','Status','Run','Help')]
    [string]$Action = 'Status',
    [string]$SourceRepository,
    [string[]]$CommandArgs = @('--help')
)

$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path -Parent $PSScriptRoot
$instanceRoot = Join-Path $projectRoot '.docker'
$dataRoot = Join-Path $instanceRoot 'data'
$secretRoot = Join-Path $instanceRoot 'secrets'
$tokenPath = Join-Path $secretRoot 'notion-token'
$utf8 = [System.Text.UTF8Encoding]::new($false)

function Invoke-Compose {
    param(
        [string[]]$DockerArguments,
        [int[]]$AllowedExitCodes = @(0)
    )
    & docker compose --project-directory $projectRoot -f (Join-Path $projectRoot 'compose.yaml') @DockerArguments
    if ($LASTEXITCODE -notin $AllowedExitCodes) { throw "Docker Compose failed (exit $LASTEXITCODE)." }
}

function Initialize-LocalFolders {
    [System.IO.Directory]::CreateDirectory($dataRoot) | Out-Null
    [System.IO.Directory]::CreateDirectory($secretRoot) | Out-Null
    # Restrict host access to local runtime credentials on Windows.
    if ($env:OS -eq 'Windows_NT') {
        $userSid = [System.Security.Principal.WindowsIdentity]::GetCurrent().User.Value
        $aclArguments = @(
            $secretRoot,
            '/inheritance:r',
            '/grant:r',
            "*$($userSid):(OI)(CI)F",
            '*S-1-5-18:(OI)(CI)F',
            '*S-1-5-32-544:(OI)(CI)F'
        )
        & icacls @aclArguments | Out-Null
        if ($LASTEXITCODE -ne 0) { throw "Could not restrict secret-directory permissions (exit $LASTEXITCODE)." }
    }
}

function ConvertTo-RepositoryRelativePath {
    param($Path, [string]$FieldName)
    if ($null -eq $Path) { return $null }
    if ($Path -isnot [string]) { throw "Import requires a string path: $FieldName" }
    $relativePath = $Path.Replace('\','/')
    if ($relativePath -match '^/|^[A-Za-z]:|(^|/)\.\.(/|$)') {
        throw "Import requires a path inside the repository: $FieldName"
    }
    return $relativePath
}

function Import-LocalRepository {
    if ([string]::IsNullOrWhiteSpace($SourceRepository)) {
        throw 'Import requires -SourceRepository with the existing Local Notion directory.'
    }
    $sourceRoot = (Resolve-Path -LiteralPath $SourceRepository).Path
    $sourceRegistry = Join-Path $sourceRoot '.localnotion/registry.json'
    if (-not (Test-Path -LiteralPath $sourceRegistry -PathType Leaf)) {
        throw 'The source does not contain .localnotion/registry.json.'
    }
    if (@(Get-ChildItem -LiteralPath $dataRoot -Force).Count -gt 0) {
        throw "Import requires an empty test data directory: $dataRoot"
    }
    if ($dataRoot.StartsWith($sourceRoot.TrimEnd('\','/') + [System.IO.Path]::DirectorySeparatorChar,
            [System.StringComparison]::OrdinalIgnoreCase) -or $sourceRoot -eq $dataRoot) {
        throw 'The import destination cannot be inside the source.'
    }
    $registry = Get-Content -LiteralPath $sourceRegistry -Raw | ConvertFrom-Json
    foreach ($property in $registry.paths.PSObject.Properties) {
        if ($property.Name -notmatch '_path$') { continue }
        if ($property.Name -eq 'repository_path') {
            $relativePath = ([string]$property.Value).Replace('\','/')
            if ($relativePath -notin @('..','../')) {
                throw 'Import requires repository_path to reference the parent of .localnotion.'
            }
            $property.Value = $relativePath
        } else {
            $property.Value = ConvertTo-RepositoryRelativePath -Path $property.Value -FieldName "paths.$($property.Name)"
        }
    }
    # Render entries retain their saved filenames when an object is unchanged.
    # Normalize and validate them before copying, alongside the path profile.
    foreach ($resource in @($registry.resources)) {
        if ($null -eq $resource -or $null -eq $resource.renders) { continue }
        foreach ($render in $resource.renders.PSObject.Properties) {
            if ($null -eq $render.Value) { continue }
            $pathProperty = $render.Value.PSObject.Properties['local_path']
            if ($null -ne $pathProperty) {
                $pathProperty.Value = ConvertTo-RepositoryRelativePath -Path $pathProperty.Value -FieldName "resources.renders.$($render.Name).local_path"
            }
        }
    }
    foreach ($cmsItem in @($registry.cms_items)) {
        if ($null -eq $cmsItem) { continue }
        $pathProperty = $cmsItem.PSObject.Properties['render_path']
        if ($null -ne $pathProperty) {
            $pathProperty.Value = ConvertTo-RepositoryRelativePath -Path $pathProperty.Value -FieldName 'cms_items.render_path'
        }
    }
    # Do not follow junctions/symlinks into unrelated directories.
    $links = @(Get-ChildItem -LiteralPath $sourceRoot -Recurse -Force -Attributes ReparsePoint)
    if ($links.Count -gt 0) { throw 'Import requires a source without junctions or symbolic links.' }

    foreach ($entry in Get-ChildItem -LiteralPath $sourceRoot -Force) {
        if ($entry.Name -eq '.git') { continue }
        Copy-Item -LiteralPath $entry.FullName -Destination $dataRoot -Recurse -Force
    }
    if (-not [string]::IsNullOrWhiteSpace($registry.notion_api_key)) {
        [System.IO.File]::WriteAllText($tokenPath, $registry.notion_api_key.Trim(), $utf8)
        $registry.notion_api_key = $null
    }
    foreach ($hook in @('git','nginx','apache')) {
        if ($null -ne $registry.$hook) { $registry.$hook.enabled = $false }
    }
    if ($null -ne $registry.git -and $null -ne $registry.git.PSObject.Properties['push']) {
        $registry.git.push = $false
    }
    # Sphere10's registry encoding detection requires a UTF-8 BOM. Keep secrets BOM-free.
    [System.IO.File]::WriteAllText(
        (Join-Path $dataRoot '.localnotion/registry.json'),
        ($registry | ConvertTo-Json -Depth 100), [System.Text.UTF8Encoding]::new($true))
    [System.IO.File]::WriteAllText(
        (Join-Path $instanceRoot 'source.json'),
        (@{ source = $sourceRoot; importedAt = [DateTime]::UtcNow.ToString('o') } | ConvertTo-Json), $utf8)
    Write-Host "Imported a test copy into $dataRoot"
    Write-Host 'The original repository is unchanged. Git and web-server hooks are disabled in the copy.'
}

if ($Action -eq 'Help') {
    Write-Host 'Official Local Notion Docker'
    Write-Host 'Install: build locally and start. Import -SourceRepository: copy an existing repository.'
    Write-Host 'Configure: enter your token privately. Start / Stop / Restart / Logs / Status.'
    Write-Host 'Run -CommandArgs @("--version"): execute CLI commands.'
    Write-Host 'Stop synchronization before running another command that accesses the same repository.'
    exit 0
}

Initialize-LocalFolders
switch ($Action) {
    'Import' { Import-LocalRepository }
    'Configure' {
        $secureToken = Read-Host 'Notion integration token (input hidden)' -AsSecureString
        $tokenPointer = [System.Runtime.InteropServices.Marshal]::SecureStringToBSTR($secureToken)
        try {
            $plainToken = [System.Runtime.InteropServices.Marshal]::PtrToStringBSTR($tokenPointer).Trim()
            if ([string]::IsNullOrWhiteSpace($plainToken)) { throw 'Token cannot be empty.' }
            [System.IO.File]::WriteAllText($tokenPath, $plainToken, $utf8)
        } finally {
            $plainToken = $null
            [System.Runtime.InteropServices.Marshal]::ZeroFreeBSTR($tokenPointer)
            $secureToken.Dispose()
        }
        Write-Host 'Token saved locally. The sync service reads it on its next poll.'
    }
    'Install' {
        if ($SourceRepository) { Import-LocalRepository }
        $revision = & git -C $projectRoot rev-parse HEAD
        if ($LASTEXITCODE -ne 0) { $revision = 'unknown' }
        Invoke-Compose -DockerArguments @('build','--build-arg',"VCS_REF=$revision")
        Invoke-Compose -DockerArguments @('up','-d','--no-build','--pull','never')
        Invoke-Compose -DockerArguments @('ps')
    }
    'Start' { Invoke-Compose -DockerArguments @('up','-d','--no-build','--pull','never') }
    'Stop' { Invoke-Compose -DockerArguments @('stop') }
    'Restart' { Invoke-Compose -DockerArguments @('restart') }
    'Logs' { Invoke-Compose -DockerArguments @('logs','--tail','100','-f','local-notion') }
    'Status' { Invoke-Compose -DockerArguments @('ps','--all') }
    'Run' {
        # CommandLineParser currently returns -2 (254 on Linux) for help.
        $allowedExitCodes = if ($CommandArgs -contains '--help') { @(0,254) } else { @(0) }
        Invoke-Compose -DockerArguments (@('run','--rm','--no-deps','local-notion') + $CommandArgs) -AllowedExitCodes $allowedExitCodes
        exit 0
    }
}
