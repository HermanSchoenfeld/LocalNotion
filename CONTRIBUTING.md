# Contributing to Local Notion

Thank you for contributing to Local Notion. This guide covers development,
testing, local deployment, Docker validation, and the maintainer-only release
process. For normal usage, see the [main README](README.md) and
[Docker guide](docker/README.md). Full CI/CD and recovery details are in the
[maintainer release guide](build/README.md).

## Requirements and pull requests

Install Git, the .NET SDK selected by [global.json](global.json), and PowerShell
7.4 or later. Docker work also requires Docker Desktop in Linux containers mode
or Docker Engine. Use a clean Sphere10 Framework checkout containing
`Sphere10.Framework.Web.VisualRenderer`, or a matching verified NuGet package set.
See [renderer build modes](docs/renderer.md#building). The renderer and Framework
are MIT-licensed; LocalNotion retains its GPL license and historical linking exception.

Create a branch from `master`, follow [.editorconfig](.editorconfig), keep the
change focused, and update NUnit tests when behavior changes. Before opening a
pull request, run:

```powershell
dotnet build LocalNotion.sln -c Release
dotnet test LocalNotion.sln -c Release
```

Include the operating systems and deployment paths you tested. Use concise
imperative commit messages. Do not commit Notion tokens, SSH keys, private
repositories, `.docker/`, build output, or machine-specific paths. New
dependencies must be necessary, appropriately licensed, and documented.

## Deploy the native Windows command locally

From the repository root:

```powershell
.\deploy-locally.ps1
```

This publishes a self-contained Windows x64 Release build, requests elevation,
and installs it at `C:\Program Files\Local Notion\localnotion.exe`. It verifies
the installed file and runs `localnotion --version`. Details are written to
`publish/deploy-locally.log`. This operation does not build or publish Docker.

Confirm which command the shell resolves:

```powershell
Get-Command localnotion -All
localnotion --version
```

To retain the installed command bootstrap but select the native executable:

```powershell
.\docker\install-cli.ps1 -NativeExecutable 'C:\Program Files\Local Notion\localnotion.exe' -NoPath
```

Set `LOCALNOTION_BACKEND=docker` temporarily when that bootstrap should use its
Docker backend for comparison. See [native bootstrap setup](docker/README.md#use-the-existing-command-bootstrap-with-a-native-windows-executable).

## Build and deploy the Docker command locally

The normal Windows developer command builds the checkout as
`local-notion:latest`, compiles the launcher, installs it under
`%LOCALAPPDATA%\Sphere10\LocalNotion\bin`, and adds that folder to user PATH:

```powershell
.\docker\install-cli.ps1 -Image local-notion:latest -BuildImage
```

Open a new terminal after the first PATH change, then run:

```powershell
localnotion --version
localnotion --help
```

Rerun with `-BuildImage` after changing application source or the Dockerfile.
After changing only the launcher or wrapper, `.\docker\install-cli.ps1` reuses
the selected image. Neither command starts the sync service or publishes an
image.

To build and test a versioned image without installing the launcher:

```powershell
$version = ([xml](Get-Content .\Version.props -Raw)).Project.PropertyGroup.ReleaseVersion
$commit = (git rev-parse HEAD).Trim()
docker build --platform linux/amd64 --build-arg "VERSION=$version" --build-arg BUILD_NUMBER=0 --build-arg "VCS_REF=$commit" -t "local-notion:$version" .
docker run --rm --network none "local-notion:$version" --version
docker run --rm --network none "local-notion:$version" --help
```

Use `local-notion:*` for local development.
`ghcr.io/sphere10/local-notion:*` is reserved for official CI releases.

## Test the Docker sync service

The helper stores test state under the Git-ignored `.docker/` directory. For a
new test repository:

```powershell
.\docker\localnotion.ps1 -Action Configure
.\docker\localnotion.ps1 -Action Install
.\docker\localnotion.ps1 -Action Logs
```

To test an existing repository, stop every writer and import a separate copy:

```powershell
.\docker\localnotion.ps1 -Action Stop
.\docker\localnotion.ps1 -Action Import -SourceRepository 'C:\path\to\repository'
.\docker\localnotion.ps1 -Action Install
.\docker\localnotion.ps1 -Action Logs
```

Import excludes `.git`, moves a copied credential into the private Docker
secret, disables Git and web-server hooks in the copy, and leaves the source
unchanged. It rejects external paths, links, and a non-empty destination. Never
run multiple writers against one repository.

Manage the service with:

```powershell
.\docker\localnotion.ps1 -Action Status
.\docker\localnotion.ps1 -Action Stop
.\docker\localnotion.ps1 -Action Start
.\docker\localnotion.ps1 -Action Restart
.\docker\localnotion.ps1 -Action Run -CommandArgs @('--version')
```

See the [Docker guide](docker/README.md) for credentials, SSH, mappings, backups,
and uninstalling.

## Package release candidates locally

Local packaging writes ignored output beneath `publish/` and uploads nothing:

```powershell
.\build\package.ps1 -Runtime win-x64
$version = ([xml](Get-Content .\Version.props -Raw)).Project.PropertyGroup.ReleaseVersion
$commit = (git rev-parse HEAD).Trim()
.\build\test-package.ps1 -ArchivePath ".\publish\$version\artifacts\localnotion-win-x64.zip" -Runtime win-x64 -Version $version -BuildNumber 0 -SourceRevisionId $commit
.\build\package-docker-launcher.ps1 -Version $version -BuildNumber 0 -SourceRevisionId $commit
```

Release archives must retain `LICENSE`, `COPYRIGHT`, `COPYING.EXCEPTION`, and
the VisualRenderer and dependency notices copied into publish output.

## Publish an official release and Docker image

Only a Sphere10 maintainer with access to the official repository performs this
step. Do not manually push a development image to GHCR. The unified GitHub
Actions workflow publishes native archives, the Windows Docker launcher, the
GitHub Release, and the exact Docker image tested by CI.

The checkout must be clean, based on official `master`, and use
`Sphere10/LocalNotion` as `origin`. Choose a new semantic version and preview:

```powershell
.\build\release.ps1 -Version 1.6.1 -WhatIf
```

When correct, explicitly start publication:

```powershell
.\build\release.ps1 -Version 1.6.1
```

The helper updates and commits only `Version.props` when required, creates the
annotated `v1.6.1` tag, and atomically pushes the commit to `master` with that
tag. The tag starts `.github/workflows/release.yml`.

CI assigns one run number to all deliverables, builds and tests eight native
targets, builds and tests the Linux AMD64 image, and packages the Windows Docker
launcher. It publishes the archives and checksums to GitHub Releases, the tested
image as `ghcr.io/sphere10/local-notion:1.6.1`, and `latest` only when this is the
newest stable version.

Pull requests and manual runs with `publish: false` build and test without
publishing. Manual `publish: true` runs are accepted only from the official
default branch. Follow the Actions link printed by `release.ps1`, verify the
GitHub Release, and confirm anonymous image access. If publishing fails after
artifacts pass, follow [release recovery](build/README.md#recover-publication-after-a-publisher-fix).

## Contribution license

By contributing, you agree that your work is licensed under the
[GNU GPL version 3 or later](LICENSE), subject to
[COPYING.EXCEPTION](COPYING.EXCEPTION) where it applies. The exception grants
rights only for copyrights its named grantors own or are authorized to license.
Be respectful and constructive in issues, reviews, and pull requests.
