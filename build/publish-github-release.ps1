#requires -Version 7.4

<#
.SYNOPSIS
Validates and publishes the tested Local Notion release artifacts.
.DESCRIPTION
Requires GH_TOKEN, authenticated Docker access to GHCR, and GITHUB_REPOSITORY set
to Sphere10/LocalNotion. Run publications serially using the workflow's shared
publication concurrency group. Matching drafts can resume; published releases and
mismatched assets, tags, or immutable image references are never overwritten.
ValidateOnly checks all nine archives and the Docker save archive without calling
GitHub or a Docker daemon.
.EXAMPLE
./build/publish-github-release.ps1 -Version 1.5.0 -BuildNumber 17 -SourceRevisionId <commit> -ArtifactsDirectory ./publish/artifacts -DockerArchive ./publish/docker-image.tar -ValidateOnly
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory)][ValidateLength(1,128)][string] $Version,
    [Parameter(Mandatory)][ValidateRange(0,65534)][int] $BuildNumber,
    [Parameter(Mandatory)][ValidatePattern('\A[0-9a-fA-F]{40}\z')][string] $SourceRevisionId,
    [Parameter(Mandatory)][string] $ArtifactsDirectory,
    [Parameter(Mandatory)][string] $DockerArchive,
    [switch] $ValidateOnly
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
$PSNativeCommandUseErrorActionPreference = $false
$repoRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$repository = 'Sphere10/LocalNotion'
$imageRepository = 'ghcr.io/sphere10/local-notion'
$SourceRevisionId = $SourceRevisionId.ToLowerInvariant()
$tag = "v$Version"
$imageTag = "$($imageRepository):$Version"
$commitTag = "$($imageRepository):sha-$SourceRevisionId"
$informationalVersion = "$Version+build.$BuildNumber.sha.$SourceRevisionId"
$versionPattern = '\A(0|[1-9][0-9]{0,4})\.(0|[1-9][0-9]{0,4})\.(0|[1-9][0-9]{0,4})(-(0|[1-9][0-9]*|[0-9]*[A-Za-z-][0-9A-Za-z-]*)(\.(0|[1-9][0-9]*|[0-9]*[A-Za-z-][0-9A-Za-z-]*))*)?\z'
if ($Version -cnotmatch $versionPattern) { throw 'Version must be SemVer without build metadata.' }
$semanticVersion = [System.Management.Automation.SemanticVersion]::Parse($Version)
if ($semanticVersion.Major -gt 65534 -or $semanticVersion.Minor -gt 65534 -or $semanticVersion.Patch -gt 65534) {
    throw 'Version components must not exceed 65534.'
}
$prerelease = $Version.Contains('-')
$artifactRoot = (Resolve-Path -LiteralPath $ArtifactsDirectory).ProviderPath
$dockerArchivePath = (Resolve-Path -LiteralPath $DockerArchive).ProviderPath
if (-not (Test-Path -LiteralPath $artifactRoot -PathType Container) -or
    -not (Test-Path -LiteralPath $dockerArchivePath -PathType Leaf)) { throw 'Supply an artifact directory and a Docker save archive.' }

function Invoke-Tool {
    param([string] $Tool, [string[]] $Arguments, [switch] $AllowFailure)
    $lines = @(& $Tool @Arguments 2>&1 | ForEach-Object { "$_" })
    $code = $LASTEXITCODE
    $result = [pscustomobject]@{ ExitCode = $code; Text = $lines -join [Environment]::NewLine }
    if ($code -ne 0 -and -not $AllowFailure) { throw "$Tool $($Arguments -join ' ') failed (exit $code): $($result.Text)" }
    return $result
}

function Get-ZipJson {
    param([string] $Path, [string] $Name)
    $zip = [IO.Compression.ZipFile]::OpenRead($Path)
    try {
        $entries = @($zip.Entries | Where-Object FullName -CEQ $Name)
        if ($entries.Count -ne 1 -or $entries[0].Length -gt 4194304) { throw "Missing, duplicate, or oversized $Name in $Path." }
        $reader = [IO.StreamReader]::new($entries[0].Open())
        try { return $reader.ReadToEnd() | ConvertFrom-Json } finally { $reader.Dispose() }
    } finally { $zip.Dispose() }
}

function Get-TarFileBytes {
    param([string] $Path, [string] $Name)
    $file = [IO.File]::OpenRead($Path)
    try {
        $reader = [System.Formats.Tar.TarReader]::new($file, $true)
        try {
            while ($null -ne ($entry = $reader.GetNextEntry())) {
                if ($entry.Name -ceq $Name -or $entry.Name -ceq "./$Name") {
                    if ($entry.Length -gt 4194304 -or $null -eq $entry.DataStream) { throw "Invalid or oversized Docker metadata: $Name." }
                    $buffer = [IO.MemoryStream]::new()
                    try { $entry.DataStream.CopyTo($buffer); return ,$buffer.ToArray() } finally { $buffer.Dispose() }
                }
            }
        } finally { $reader.Dispose() }
    } finally { $file.Dispose() }
    throw "Docker archive is missing $Name."
}

function Assert-ImageLabels {
    param($Labels)
    if ($null -eq $Labels -or
        $Labels.'org.opencontainers.image.version' -cne $Version -or
        $Labels.'org.opencontainers.image.revision' -cne $SourceRevisionId -or
        $Labels.'com.sphere10.localnotion.build-number' -cne [string]$BuildNumber -or
        $Labels.'org.opencontainers.image.source' -cne "https://github.com/$repository") {
        throw 'Docker image labels do not match the official release version, build, and commit.'
    }
}

# Validate the complete input set before any GitHub or registry access.
$platforms = Get-Content -LiteralPath (Join-Path $PSScriptRoot 'platforms.json') -Raw | ConvertFrom-Json
if (@($platforms.include).Count -ne 8 -or @($platforms.include.rid | Select-Object -Unique).Count -ne 8) {
    throw 'The release matrix must contain exactly eight unique native runtimes.'
}
$allowedInputs = @($platforms.include | ForEach-Object { "localnotion-$($_.rid).$($_.archiveExtension)" }) + @('localnotion-docker-windows.zip', 'release.json', 'SHA256SUMS.txt')
$unexpectedInputs = @(Get-ChildItem -LiteralPath $artifactRoot -Force | Where-Object { $_.PSIsContainer -or $_.Name -cnotin $allowedInputs })
if ($unexpectedInputs.Count) { throw "Unexpected release inputs: $($unexpectedInputs.Name -join ', ')." }
$archivePaths = [Collections.Generic.List[string]]::new()
foreach ($platform in $platforms.include) {
    $path = Join-Path $artifactRoot "localnotion-$($platform.rid).$($platform.archiveExtension)"
    if (-not (Test-Path -LiteralPath $path -PathType Leaf)) { throw "Required release archive is missing: $path" }
    & (Join-Path $PSScriptRoot 'test-package.ps1') -ArchivePath $path -Runtime $platform.rid -Version $Version -BuildNumber $BuildNumber -SourceRevisionId $SourceRevisionId -SkipExecution
    $archivePaths.Add($path)
}
$launcherPath = Join-Path $artifactRoot 'localnotion-docker-windows.zip'
$launcher = Get-ZipJson -Path $launcherPath -Name 'release.json'
if ($launcher.version -cne $Version -or $launcher.buildNumber -ne $BuildNumber -or
    $launcher.commit -cne $SourceRevisionId -or $launcher.image -cne $imageTag) {
    throw 'Docker launcher metadata does not match this release.'
}
$launcherFiles = @('release.json', 'install.ps1', 'install.bat', 'README.txt', 'LICENSE', 'COPYRIGHT', 'docker/install-cli.ps1', 'docker/localnotion-docker.ps1', 'docker/LocalNotionDockerLauncher.cs')
$launcherZip = [IO.Compression.ZipFile]::OpenRead($launcherPath)
try {
    $entries = @($launcherZip.Entries | Where-Object { -not $_.FullName.EndsWith('/') } | ForEach-Object FullName)
    if ($entries.Count -ne $launcherFiles.Count -or @($entries | Where-Object { $_ -cnotin $launcherFiles }).Count -or
        @($launcherFiles | Where-Object { $_ -cnotin $entries }).Count) { throw 'Docker launcher ZIP has missing, duplicate, or unexpected payload files.' }
} finally { $launcherZip.Dispose() }
$archivePaths.Add($launcherPath)
$archiveRecords = @($archivePaths | Sort-Object | ForEach-Object {
    [ordered]@{ name = [IO.Path]::GetFileName($_); size = (Get-Item -LiteralPath $_).Length; sha256 = (Get-FileHash -LiteralPath $_ -Algorithm SHA256).Hash.ToLowerInvariant() }
})

$dockerManifest = @([Text.Encoding]::UTF8.GetString((Get-TarFileBytes -Path $dockerArchivePath -Name 'manifest.json')) | ConvertFrom-Json)
if ($dockerManifest.Count -ne 1 -or @($dockerManifest[0].RepoTags).Count -ne 1 -or $dockerManifest[0].RepoTags[0] -cne $imageTag) {
    throw 'Docker save archive must contain only the tested release image with its exact version tag.'
}
$configBytes = Get-TarFileBytes -Path $dockerArchivePath -Name $dockerManifest[0].Config
$config = [Text.Encoding]::UTF8.GetString($configBytes) | ConvertFrom-Json
Assert-ImageLabels $config.config.Labels
if ($config.os -cne 'linux' -or $config.architecture -cne 'amd64') { throw 'The tested Docker image must target linux/amd64.' }
$imageId = 'sha256:' + [Convert]::ToHexString([Security.Cryptography.SHA256]::HashData($configBytes)).ToLowerInvariant()
if ($ValidateOnly) {
    [pscustomobject]@{ Validated = $true; Version = $Version; BuildNumber = $BuildNumber; Commit = $SourceRevisionId; ImageId = $imageId; Archives = $archiveRecords.Count }
    return
}

if ($env:GITHUB_REPOSITORY -cne $repository) { throw "Publication is restricted to $repository." }
if ([string]::IsNullOrWhiteSpace($env:GH_TOKEN)) { throw 'GH_TOKEN is required for publication.' }
$gh = (Get-Command gh -CommandType Application -ErrorAction Stop | Select-Object -First 1).Source
$docker = (Get-Command docker -CommandType Application -ErrorAction Stop | Select-Object -First 1).Source

function Get-GitHubJson {
    param([string] $Endpoint, [switch] $AllowNotFound)
    $response = Invoke-Tool $gh @('api', '--hostname', 'github.com', $Endpoint, '-H', 'Accept: application/vnd.github+json') -AllowFailure
    if ($response.ExitCode -eq 0) { return $response.Text | ConvertFrom-Json }
    # gh emits the HTTP status for API errors; authentication, transport, and rate
    # failures must never be interpreted as an absent release or tag.
    if ($AllowNotFound -and $response.Text -match '\(HTTP 404\)') { return $null }
    throw "GitHub query $Endpoint failed: $($response.Text)"
}

function Get-ReleaseForTag {
    param([switch] $Require)
    # The tag endpoint serves published releases. Authenticated list responses
    # also include drafts, which may not have a discoverable tag endpoint yet.
    $published = Get-GitHubJson "repos/$repository/releases/tags/$tag" -AllowNotFound
    if ($null -ne $published) { return $published }
    $matchingReleases = [Collections.Generic.List[object]]::new()
    for ($page = 1; ; $page++) {
        $releases = @(Get-GitHubJson "repos/$repository/releases?per_page=100&page=$page")
        foreach ($candidateRelease in $releases) {
            if ($candidateRelease.tag_name -ceq $tag) { $matchingReleases.Add($candidateRelease) }
        }
        if ($releases.Count -lt 100) { break }
    }
    if ($matchingReleases.Count -gt 1) { throw "Multiple releases or drafts match $tag. Resolve the duplicate drafts before publishing." }
    if ($matchingReleases.Count -eq 1) { return $matchingReleases[0] }
    if ($Require) { throw "Could not find the release or draft for $tag." }
    return $null
}
function Assert-RemoteTag {
    param([switch] $Require, [switch] $PassThru)
    $reference = Get-GitHubJson "repos/$repository/git/ref/tags/$tag" -AllowNotFound
    if ($null -eq $reference) {
        if ($Require) { throw "Release tag $tag was not created." }
        if ($PassThru) { return $false }
        return
    }
    $target = $reference.object
    for ($depth = 0; $target.type -ceq 'tag' -and $depth -lt 8; $depth++) {
        $target = (Get-GitHubJson "repos/$repository/git/tags/$($target.sha)").object
    }
    if ($target.type -cne 'commit' -or $target.sha -cne $SourceRevisionId) { throw "Tag $tag does not point to $SourceRevisionId; tags are never moved." }
    if ($PassThru) { return $true }
}

function Get-LocalImage {
    param([string] $Reference)
    $response = Invoke-Tool $docker @('image', 'inspect', '--format', '{{json .}}', $Reference)
    return $response.Text | ConvertFrom-Json
}

function Get-RegistryImage {
    param([string] $Reference)
    $probe = Invoke-Tool $docker @('manifest', 'inspect', $Reference) -AllowFailure
    if ($probe.ExitCode -ne 0) {
        if ($probe.Text -match '(?i)(manifest unknown|manifest_unknown|no such manifest|name unknown|name_unknown)' -and
            $probe.Text -notmatch '(?i)(unauthorized|denied|timeout|connection|TLS|certificate)') { return $null }
        throw "Registry lookup failed for $Reference; this is not a confirmed missing manifest: $($probe.Text)"
    }
    $null = Invoke-Tool $docker @('pull', $Reference)
    return Get-LocalImage $Reference
}

function Get-LatestDecision {
    if ($prerelease) { return $false }
    $latestRelease = Get-GitHubJson "repos/$repository/releases/latest" -AllowNotFound
    if ($null -ne $latestRelease) {
        $latestVersion = [System.Management.Automation.SemanticVersion]::Parse($latestRelease.tag_name.TrimStart('v'))
        if ($semanticVersion.CompareTo($latestVersion) -le 0) { return $false }
    }
    $latestImage = Get-RegistryImage "$($imageRepository):latest"
    if ($null -ne $latestImage) {
        try { $latestVersion = [System.Management.Automation.SemanticVersion]::Parse($latestImage.Config.Labels.'org.opencontainers.image.version') }
        catch { throw 'Cannot safely promote latest: the current Docker latest image has no semantic version label.' }
        $order = $semanticVersion.CompareTo($latestVersion)
        if ($order -lt 0) { return $false }
        # Equality is allowed only to finish a matching draft whose image was
        # promoted immediately before a previous release-edit failure.
        if ($order -eq 0 -and $latestImage.Id -cne $imageId) { return $false }
    }
    return $true
}

$repo = Get-GitHubJson "repos/$repository"
if ($repo.full_name -cne $repository) { throw 'GitHub returned an unexpected repository.' }
$tagExists = Assert-RemoteTag -PassThru
$release = Get-ReleaseForTag
$identity = "<!-- localnotion-release version=$Version build=$BuildNumber commit=$SourceRevisionId image=$imageId -->"
if ($null -ne $release -and (-not $release.draft -or -not ([string]$release.body).Contains($identity))) {
    throw "Release $tag already exists and is not a matching resumable draft. Published releases are never overwritten."
}
# Check existing native/launcher uploads before creating or pushing anything.
# The generated manifest/checksum files are compared once the image digest is known.
$preflightDirectory = Join-Path $repoRoot "publish/.release/$([guid]::NewGuid().ToString('N'))"
if ($null -ne $release) {
    foreach ($asset in $release.assets) {
        if ($asset.name -cnotin $allowedInputs) { throw "Draft contains an unexpected asset: $($asset.name)." }
        $record = @($archiveRecords | Where-Object { $_.name -ceq $asset.name })
        if ($record.Count -eq 1) {
            $remoteHash = if ($asset.PSObject.Properties.Name -contains 'digest') { [string]$asset.digest } else { '' }
            if (-not $remoteHash) {
                $downloadDirectory = Join-Path $preflightDirectory ([guid]::NewGuid().ToString('N'))
                $null = New-Item -ItemType Directory -Path $downloadDirectory -Force
                $null = Invoke-Tool $gh @('release', 'download', $tag, '--repo', $repository, '--pattern', $asset.name, '--dir', $downloadDirectory)
                $remoteHash = 'sha256:' + (Get-FileHash -LiteralPath (Join-Path $downloadDirectory $asset.name) -Algorithm SHA256).Hash.ToLowerInvariant()
            }
            if ($asset.size -ne $record[0].size -or $remoteHash -cne "sha256:$($record[0].sha256)") {
                throw "Draft asset $($asset.name) differs from this build. Existing assets are never overwritten."
            }
        }
    }
}
$null = Invoke-Tool $docker @('load', '--input', $dockerArchivePath)
$candidate = Get-LocalImage $imageId
Assert-ImageLabels $candidate.Config.Labels
if ($candidate.Id -cne $imageId -or $candidate.Os -cne 'linux' -or $candidate.Architecture -cne 'amd64') { throw 'Loaded Docker image does not match the tested archive.' }
foreach ($reference in @($imageTag, $commitTag)) {
    $existing = Get-RegistryImage $reference
    if ($null -ne $existing -and $existing.Id -cne $imageId) { throw "Immutable image reference $reference already contains a different image." }
}
$promoteLatest = Get-LatestDecision

$workDirectory = Join-Path $repoRoot "publish/.release/$([guid]::NewGuid().ToString('N'))"
$null = New-Item -ItemType Directory -Path $workDirectory -Force
$notesPath = Join-Path $workDirectory 'notes.md'
$notes = @(
    "Local Notion $Version"
    ''
    "Build: $BuildNumber"
    "Commit: $SourceRevisionId"
    "Version: $informationalVersion"
    ''
    'Download and extract a native archive for your platform, or use the Windows Docker launcher installer.'
    'Verify downloads against SHA256SUMS.txt. release.json records the image digest and archive hashes.'
    ''
    "Docker image: $imageTag"
    ''
    $identity
) -join [char]10
[IO.File]::WriteAllText($notesPath, $notes + [char]10, [Text.UTF8Encoding]::new($false))
if ($null -eq $release) {
    $arguments = @('release', 'create', $tag, '--repo', $repository, '--title', "Local Notion $Version", '--draft', '--latest=false', '--notes-file', $notesPath)
    if ($tagExists) {
        # The verified tag already pins the exact source commit. Leaving the
        # unused target unset lets draft metadata follow the default branch.
        $arguments += '--verify-tag'
    } else {
        $arguments += @('--target', $SourceRevisionId)
    }
    if ($prerelease) { $arguments += '--prerelease' }
    $null = Invoke-Tool $gh $arguments
    $release = Get-ReleaseForTag -Require
    if (-not $release.draft -or -not ([string]$release.body).Contains($identity)) { throw "Created release $tag is not the expected matching draft." }
}

# Retag from the captured image ID, since remote existence checks may have
# replaced local tag pointers. Every push contains the exact saved candidate.
foreach ($reference in @($imageTag, $commitTag)) {
    $null = Invoke-Tool $docker @('tag', $imageId, $reference)
    $null = Invoke-Tool $docker @('push', $reference)
}
$pushedImage = Get-LocalImage $imageId
$repoDigests = @($pushedImage.RepoDigests | Where-Object { $_.StartsWith("$imageRepository@sha256:") } | Select-Object -Unique)
if ($repoDigests.Count -ne 1) { throw 'Could not determine the unique pushed image digest.' }
$digest = $repoDigests[0].Substring($imageRepository.Length + 1)
if ($digest -cnotmatch '\Asha256:[0-9a-f]{64}\z') { throw 'Registry returned an invalid image digest.' }

$releaseManifest = [ordered]@{
    schemaVersion = 1; version = $Version; buildNumber = $BuildNumber; commit = $SourceRevisionId
    informationalVersion = $informationalVersion; tag = $tag
    image = [ordered]@{ repository = $imageRepository; tag = $imageTag; commitTag = $commitTag; digest = $digest; id = $imageId }
    assets = $archiveRecords
}
$manifestPath = Join-Path $artifactRoot 'release.json'
[IO.File]::WriteAllText($manifestPath, ($releaseManifest | ConvertTo-Json -Depth 6) + [char]10, [Text.UTF8Encoding]::new($false))
$uploadPaths = $archivePaths.ToArray() + @($manifestPath)
$checksumPath = Join-Path $artifactRoot 'SHA256SUMS.txt'
$checksums = @($uploadPaths | Sort-Object | ForEach-Object { (Get-FileHash -LiteralPath $_ -Algorithm SHA256).Hash.ToLowerInvariant() + '  ' + [IO.Path]::GetFileName($_) })
[IO.File]::WriteAllText($checksumPath, ($checksums -join [char]10) + [char]10, [Text.UTF8Encoding]::new($false))
$uploadPaths += $checksumPath
$expectedNames = @($uploadPaths | ForEach-Object { [IO.Path]::GetFileName($_) })
foreach ($asset in $release.assets) {
    if ($asset.name -cnotin $expectedNames) { throw "Draft contains an unexpected asset: $($asset.name)." }
}
foreach ($path in $uploadPaths) {
    $name = [IO.Path]::GetFileName($path)
    $hash = (Get-FileHash -LiteralPath $path -Algorithm SHA256).Hash.ToLowerInvariant()
    $existingAssets = @($release.assets | Where-Object name -CEQ $name)
    if ($existingAssets.Count -gt 1) { throw "Draft contains duplicate assets named $name." }
    if ($existingAssets.Count -eq 1) {
        $asset = $existingAssets[0]
        $remoteHash = if ($asset.PSObject.Properties.Name -contains 'digest') { [string]$asset.digest } else { '' }
        if (-not $remoteHash) {
            $downloadDirectory = Join-Path $workDirectory ([guid]::NewGuid().ToString('N'))
            $null = New-Item -ItemType Directory -Path $downloadDirectory
            $null = Invoke-Tool $gh @('release', 'download', $tag, '--repo', $repository, '--pattern', $name, '--dir', $downloadDirectory)
            $remoteHash = 'sha256:' + (Get-FileHash -LiteralPath (Join-Path $downloadDirectory $name) -Algorithm SHA256).Hash.ToLowerInvariant()
        }
        if ($asset.size -ne (Get-Item -LiteralPath $path).Length -or $remoteHash -cne "sha256:$hash") {
            throw "Draft asset $name differs from this build. Existing assets are never overwritten."
        }
    } else {
        $null = Invoke-Tool $gh @('release', 'upload', $tag, $path, '--repo', $repository)
    }
}

# A separate Docker configuration prevents the runner's GHCR credentials from
# making a private package appear public. A nonempty auths map disables automatic
# credential-store discovery, while its empty GHCR entry contains no credentials.
$anonymousConfig = Join-Path $workDirectory 'anonymous-docker'
$null = New-Item -ItemType Directory -Path $anonymousConfig
[IO.File]::WriteAllText((Join-Path $anonymousConfig 'config.json'), '{"auths":{"ghcr.io":{}}}', [Text.UTF8Encoding]::new($false))
$anonymousPull = Invoke-Tool $docker @('--config', $anonymousConfig, 'pull', "$imageRepository@$digest") -AllowFailure
if ($anonymousPull.ExitCode -ne 0) {
    throw "The image is not confirmed publicly pullable. The release remains a draft. Verify GHCR package visibility and registry/network access: $($anonymousPull.Text)"
}
$publicImage = Get-LocalImage "$imageRepository@$digest"
if ($publicImage.Id -cne $imageId) { throw 'Anonymous pull returned a different image.' }

Assert-RemoteTag
$promoteLatest = Get-LatestDecision
if ($promoteLatest) {
    $null = Invoke-Tool $docker @('tag', $imageId, "$($imageRepository):latest")
    $null = Invoke-Tool $docker @('push', "$($imageRepository):latest")
}
$arguments = @('release', 'edit', $tag, '--repo', $repository, '--draft=false', "--latest=$($promoteLatest.ToString().ToLowerInvariant())")
$null = Invoke-Tool $gh $arguments
Assert-RemoteTag -Require
$published = Get-GitHubJson "repos/$repository/releases/tags/$tag"
if ($published.draft) { throw 'GitHub still reports the release as a draft.' }
Write-Host "Published $tag, build $($BuildNumber): $($published.html_url)"
Write-Host "Public image: $imageRepository@$digest"
if ($env:GITHUB_STEP_SUMMARY) {
    $summary = @(
        "### Local Notion $Version"
        ''
        "- Release: $($published.html_url)"
        "- Build: $BuildNumber"
        "- Commit: $SourceRevisionId"
        "- Image: $imageTag"
        "- Digest: $digest"
        "- Latest: $promoteLatest"
    ) -join [char]10
    [IO.File]::AppendAllText($env:GITHUB_STEP_SUMMARY, $summary + [char]10, [Text.UTF8Encoding]::new($false))
}
[pscustomobject]@{ Version = $Version; BuildNumber = $BuildNumber; Commit = $SourceRevisionId; ReleaseUrl = $published.html_url; Image = "$imageRepository@$digest"; Latest = $promoteLatest }
