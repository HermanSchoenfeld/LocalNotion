# Official Local Notion Docker: Windows CLI launcher.
# No param block: preserve Local Notion's own option names in raw $args.
$ErrorActionPreference = 'Stop'
$cliArguments = @($args | ForEach-Object { [string]$_ })
$argumentEnvelope = $env:LOCALNOTION_DOCKER_ARGS
$env:LOCALNOTION_DOCKER_ARGS = $null

function ConvertTo-WindowsArgument {
    param([AllowEmptyString()][string]$Value)
    # CommandLineToArgvW/MS C runtime quoting, including embedded quotes and trailing slashes.
    $escaped = [regex]::Replace($Value, '(\\*)"', '$1$1\"')
    $escaped = [regex]::Replace($escaped, '(\\+)$', '$1$1')
    return '"' + $escaped + '"'
}

function New-DockerProcess {
    param([string[]]$Arguments, [switch]$Capture)
    $startInfo = [System.Diagnostics.ProcessStartInfo]::new()
    $startInfo.FileName = $script:dockerExecutable
    $startInfo.Arguments = ($Arguments | ForEach-Object { ConvertTo-WindowsArgument $_ }) -join ' '
    $startInfo.UseShellExecute = $false
    $startInfo.RedirectStandardOutput = [bool]$Capture
    $startInfo.RedirectStandardError = [bool]$Capture
    $startInfo.CreateNoWindow = [bool]$Capture
    return [System.Diagnostics.Process]::Start($startInfo)
}

function Invoke-DockerCapture {
    param([string[]]$Arguments)
    $process = New-DockerProcess -Arguments $Arguments -Capture
    try {
        $stdout = $process.StandardOutput.ReadToEndAsync()
        $stderr = $process.StandardError.ReadToEndAsync()
        $process.WaitForExit()
        return [pscustomobject]@{
            ExitCode = $process.ExitCode
            Output = $stdout.Result
            Error = $stderr.Result
        }
    } finally { $process.Dispose() }
}

function Resolve-HostPath {
    param([string]$Value, [string]$BaseDirectory, [ValidateSet('Directory','File')][string]$Kind)
    if ([string]::IsNullOrWhiteSpace($Value)) { throw "The $Kind path cannot be empty." }
    $candidate = if ([System.IO.Path]::IsPathRooted($Value)) { $Value } else { Join-Path $BaseDirectory $Value }
    try { $resolved = Resolve-Path -LiteralPath $candidate -ErrorAction Stop } catch {
        throw "$Kind path does not exist: $candidate"
    }
    if ($resolved.Provider.Name -ne 'FileSystem') { throw 'Only Windows filesystem paths can be mounted into Docker.' }
    $path = $resolved.ProviderPath
    $valid = if ($Kind -eq 'Directory') { [System.IO.Directory]::Exists($path) } else { [System.IO.File]::Exists($path) }
    if (-not $valid) { throw "Expected a $Kind path: $path" }
    return $path
}

function ConvertTo-ComparableHostPath {
    param([string]$Path)
    $normalized = $Path.Replace('\','/')
    if ($normalized -match '^/(?:run/desktop/mnt/host/|host_mnt/|mnt/)([a-zA-Z])(?:/|$)(.*)$') {
        $normalized = $matches[1] + ':/' + $matches[2]
    }
    return $normalized.TrimEnd('/')
}

function Assert-ContainerCompatibleRegistry {
    param([string]$RepositoryPath)
    $registryFile = Join-Path $RepositoryPath '.localnotion/registry.json'
    if (-not [System.IO.File]::Exists($registryFile)) { return $false }
    try { $registry = Get-Content -LiteralPath $registryFile -Raw -Encoding UTF8 | ConvertFrom-Json } catch {
        throw 'Could not read the repository registry.json. Check that it is a valid Local Notion repository.'
    }
    function Assert-RelativeRegistryPath {
        param($Value, [switch]$RepositoryRoot)
        if ($null -eq $Value) { return $false }
        $invalid = $Value -isnot [string]
        if (-not $invalid) {
            # Windows separators in relative paths are interpreted by the application.
            # Validate the portable form without rewriting the host registry here.
            $portablePath = $Value.Replace('\','/')
            $invalid = if ($RepositoryRoot) { $portablePath -notin @('..','../') } else {
                $portablePath -match '^/|^[A-Za-z][A-Za-z0-9+.-]*:|(^|/)\.\.(/|$)'
            }
        }
        if ($invalid) {
            throw 'This repository contains an absolute path or a storage/render path outside its folder. Command mode mounts only the selected repository. Move that storage inside the repository or use a Docker configuration with explicit mounts.'
        }
        return $Value.Contains('\')
    }
    $needsPortablePaths = $false
    foreach ($property in $registry.paths.PSObject.Properties) {
        if ($property.Name -match '_path$') {
            if (Assert-RelativeRegistryPath -Value $property.Value -RepositoryRoot:($property.Name -eq 'repository_path')) { $needsPortablePaths = $true }
        }
    }
    foreach ($resource in @($registry.resources)) {
        if ($null -eq $resource -or $null -eq $resource.renders) { continue }
        foreach ($render in $resource.renders.PSObject.Properties) {
            if ($null -ne $render.Value -and (Assert-RelativeRegistryPath -Value $render.Value.local_path)) { $needsPortablePaths = $true }
        }
    }
    foreach ($cmsItem in @($registry.cms_items)) {
        if ($null -ne $cmsItem -and (Assert-RelativeRegistryPath -Value $cmsItem.render_path)) { $needsPortablePaths = $true }
    }
    return $needsPortablePaths
}
function New-BindMount {
    param([string]$Source, [string]$Target, [switch]$ReadOnly)
    # Docker --mount uses CSV; quoting the whole source field also preserves commas.
    $mount = 'type=bind,"source=' + $Source.Replace('"','""') + '",target=' + $Target
    if ($ReadOnly) { $mount += ',readonly' }
    return $mount
}

$runProcess = $null
$containerName = $null
$exitCode = 1
try {
    if ($argumentEnvelope) {
        try {
            $decoded = [System.Text.Encoding]::UTF8.GetString([Convert]::FromBase64String($argumentEnvelope)) | ConvertFrom-Json
            $cliArguments = @($decoded | ForEach-Object { [string]$_ })
        } catch { throw 'The localnotion launcher received an invalid argument envelope.' }
        $argumentEnvelope = $null
    }
    $location = Get-Location
    if ($location.Provider.Name -ne 'FileSystem') { throw 'Run localnotion from a Windows filesystem directory.' }
    $workingDirectory = $location.ProviderPath
    $metadataOnly = $cliArguments.Count -eq 0 -or $cliArguments -contains '--help' -or $cliArguments -contains '--version' -or $cliArguments[0] -in @('help','version')
    $helpRequested = $cliArguments.Count -eq 0 -or $cliArguments -contains '--help' -or $cliArguments[0] -eq 'help'

    $configuration = $null
    $configFile = Join-Path $PSScriptRoot 'localnotion-docker.json'
    if (Test-Path -LiteralPath $configFile -PathType Leaf) {
        try { $configuration = Get-Content -LiteralPath $configFile -Raw -Encoding UTF8 | ConvertFrom-Json } catch {
            throw 'The localnotion-docker.json launcher configuration is invalid.'
        }
    }
    $image = if ($env:LOCALNOTION_IMAGE) { $env:LOCALNOTION_IMAGE } elseif ($configuration.image) { [string]$configuration.image } else { 'local-notion:latest' }
    $stateVolume = if ($configuration.stateVolume) { [string]$configuration.stateVolume } else { 'local-notion-state' }
    if ($image -notmatch '^[A-Za-z0-9][A-Za-z0-9._:/@-]*$') { throw 'LOCALNOTION_IMAGE or the configured image name is invalid.' }
    if ($stateVolume -notmatch '^[A-Za-z0-9][A-Za-z0-9_.-]*$') { throw 'The configured Docker state volume name is invalid.' }

    $repositoryPath = $workingDirectory
    $rewrittenArguments = [System.Collections.Generic.List[string]]::new()
    $pathSeen = $false
    for ($index = 0; $index -lt $cliArguments.Count; $index++) {
        $argument = $cliArguments[$index]
        if ($argument -match '^--(?:override-[a-z-]+-path|cancel-trigger)(?:=|$)' -and -not $metadataOnly) {
            throw 'This launcher does not support additional filesystem path options. Use a Docker deployment with explicit mounts for those paths.'
        }
        $shortPath = $argument -eq '-p' -or ($cliArguments[0] -eq 'prune' -and $argument -eq '-r')
        if ($argument -eq '--path' -or $shortPath -or $argument.StartsWith('--path=')) {
            if ($pathSeen) { throw 'Specify the repository path only once.' }
            $pathSeen = $true
            if ($argument.StartsWith('--path=')) { $selectedPath = $argument.Substring(7) } else {
                $index++
                if ($index -ge $cliArguments.Count) { throw 'The --path/-p option requires a Windows repository directory.' }
                $selectedPath = $cliArguments[$index]
            }
            $repositoryPath = Resolve-HostPath -Value $selectedPath -BaseDirectory $workingDirectory -Kind Directory
            $rewrittenArguments.Add('--path')
            $rewrittenArguments.Add('/repo')
        } else { $rewrittenArguments.Add($argument) }
    }

    $dockerCommand = Get-Command docker.exe -CommandType Application -ErrorAction SilentlyContinue | Select-Object -First 1
    if (-not $dockerCommand) { throw 'Docker CLI was not found. Install Docker Desktop and ensure docker.exe is on PATH.' }
    $script:dockerExecutable = $dockerCommand.Source
    if ($env:DOCKER_HOST -and $env:DOCKER_HOST.Replace('\','/') -notmatch '^(?:npipe:/+\./pipe/|unix:///)') {
        throw 'This launcher requires local Docker Desktop. A remote DOCKER_HOST cannot mount this computer''s repository paths.'
    }
    $endpoint = Invoke-DockerCapture -Arguments @('context','inspect','--format','{{.Endpoints.docker.Host}}')
    if ($endpoint.ExitCode -ne 0 -or $endpoint.Output.Trim().Replace('\','/') -notmatch '^(?:npipe:/+\./pipe/|unix:///)') {
        throw 'Select a local Docker Desktop context before using localnotion; remote Docker contexts are not supported.'
    }
    $engine = Invoke-DockerCapture -Arguments @('info','--format','{{.OSType}}')
    if ($engine.ExitCode -ne 0) { throw 'Docker Desktop is unavailable. Start Docker Desktop, wait until its engine is ready, then retry.' }
    if ($engine.Output.Trim() -ne 'linux') { throw 'Switch Docker Desktop to Linux containers before running localnotion.' }
    $localImage = Invoke-DockerCapture -Arguments @('image','inspect','--format','{{index .Config.Labels "com.sphere10.localnotion.portable-paths"}}',$image)
    if ($localImage.ExitCode -ne 0) { throw "The local Docker image '$image' is missing. Build it with docker/install-cli.ps1 -BuildImage in the source checkout." }

    $tokenPath = $null
    if (-not $metadataOnly) {
        $targetPath = ConvertTo-ComparableHostPath $repositoryPath
        $running = Invoke-DockerCapture -Arguments @('ps','--quiet')
        if ($running.ExitCode -ne 0) { throw 'Could not check whether another Docker container is using the repository.' }
        $containerIds = @($running.Output -split '\r?\n' | Where-Object { $_ })
        if ($containerIds.Count) {
            $mounts = Invoke-DockerCapture -Arguments (@('inspect','--format','{{.Name}}|{{json .Mounts}}') + $containerIds)
            if ($mounts.ExitCode -ne 0) { throw 'Could not inspect running containers. Retry after Docker finishes starting or stopping them.' }
            foreach ($line in @($mounts.Output -split '\r?\n' | Where-Object { $_ })) {
                $parts = $line -split '\|',2
                # PS5.1 keeps ConvertFrom-Json arrays as a single pipeline object.
                # Assign first, then enumerate the actual mount array.
                $parsedMounts = ConvertFrom-Json -InputObject $parts[1]
                foreach ($mount in $parsedMounts) {
                    if ($mount.Type -eq 'bind' -and $mount.Destination -eq '/repo' -and
                        (ConvertTo-ComparableHostPath $mount.Source).Equals($targetPath,[StringComparison]::OrdinalIgnoreCase)) {
                        $busyName = $parts[0].TrimStart('/')
                        throw "This repository is already in use by Docker container '$busyName'. Stop it with: docker stop $busyName"
                    }
                }
            }
        }
        $needsPortablePaths = Assert-ContainerCompatibleRegistry -RepositoryPath $repositoryPath
        if ($needsPortablePaths -and $localImage.Output.Trim() -ne '1') {
            throw 'This Docker image needs an update to open Windows-style repository paths. From the source checkout, run: .\docker\install-cli.ps1 -BuildImage'
        }
        if ($env:LOCALNOTION_TOKEN_FILE) {
            $tokenPath = Resolve-HostPath -Value $env:LOCALNOTION_TOKEN_FILE -BaseDirectory $workingDirectory -Kind File
        } else {
            foreach ($mapping in @($configuration.repositories)) {
                if (-not $mapping.hostPath) { continue }
                $mappedPath = [string]$mapping.hostPath
                if (-not [System.IO.Path]::IsPathRooted($mappedPath)) { $mappedPath = Join-Path $PSScriptRoot $mappedPath }
                $mappedPath = [System.IO.Path]::GetFullPath($mappedPath)
                if ((ConvertTo-ComparableHostPath $mappedPath).Equals($targetPath,[StringComparison]::OrdinalIgnoreCase)) {
                    if ($mapping.tokenFile) {
                        $tokenPath = Resolve-HostPath -Value ([string]$mapping.tokenFile) -BaseDirectory $PSScriptRoot -Kind File
                    }
                    break
                }
            }
        }
    }

    $containerName = 'local-notion-cli-' + [Guid]::NewGuid().ToString('N')
    $dockerArguments = [System.Collections.Generic.List[string]]::new()
    foreach ($value in @('run','--rm','--init','--pull','never','--name',$containerName,'--stop-signal','SIGINT','--stop-timeout','60','--workdir','/repo','--mount',"type=volume,source=$stateVolume,target=/var/lib/localnotion")) { $dockerArguments.Add($value) }
    if (-not $metadataOnly) {
        $dockerArguments.Add('--mount')
        $dockerArguments.Add((New-BindMount -Source $repositoryPath -Target '/repo'))
        if ($tokenPath) {
            $dockerArguments.Add('--mount')
            $dockerArguments.Add((New-BindMount -Source $tokenPath -Target '/run/secrets/notion-token' -ReadOnly))
            $dockerArguments.Add('--env')
            $dockerArguments.Add('NOTION_API_KEY_FILE=/run/secrets/notion-token')
        }
    }
    $dockerArguments.Add($image)
    $dockerArguments.AddRange($rewrittenArguments)
    $runProcess = New-DockerProcess -Arguments $dockerArguments.ToArray()
    $runProcess.WaitForExit()
    $exitCode = $runProcess.ExitCode
    if ($helpRequested -and $exitCode -eq 254) { $exitCode = 0 }
} catch {
    [Console]::Error.WriteLine('localnotion: ' + $_.Exception.Message)
    $exitCode = 1
} finally {
    if ($null -ne $runProcess) {
        try {
            # The Docker client can exit before its container. Check the container itself,
            # including after Ctrl+C, and only clean up this invocation's unique name.
            $remaining = Invoke-DockerCapture -Arguments @('inspect','--format','{{.State.Running}}',$containerName)
            if ($remaining.ExitCode -eq 0) {
                if ($remaining.Output.Trim() -eq 'true') {
                    $stopped = Invoke-DockerCapture -Arguments @('stop','--signal','SIGINT','--time','60',$containerName)
                    if ($stopped.ExitCode -ne 0) { throw "Could not stop the command container. Run: docker stop $containerName" }
                }
                $removed = Invoke-DockerCapture -Arguments @('rm',$containerName)
            }
            if (-not $runProcess.HasExited) { $runProcess.WaitForExit(5000) | Out-Null }
        } catch {
            [Console]::Error.WriteLine('localnotion: ' + $_.Exception.Message)
            $exitCode = 1
        } finally { $runProcess.Dispose() }
    }
}
exit $exitCode