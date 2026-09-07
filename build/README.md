# Build and release Local Notion

The build system lives in this repository. It does not require `Y:\builds`, the old SP10 PowerShell module, external attachment folders, signing certificates, or developer-machine paths. Build outputs, temporary packaging directories, and smoke-test extractions are kept under the Git-ignored `publish/` directory.

## Requirements

Use the .NET 8 SDK, Git, and PowerShell **7.4 or later** for packaging and CI helpers. The release entry point requires PowerShell 7 or later; use 7.4 for the complete toolchain. Docker Desktop or Docker Engine is needed for Docker builds and image checks. The local release command requires authenticated Git push access to the official repository. GitHub Actions supplies GitHub CLI and its own token for the publishing job; you do not need to install GitHub CLI locally.

The native Windows archive installer is a separate end-user script compatible with Windows PowerShell **5.1**. Users do not need the .NET SDK to run self-contained native archives or the Windows Docker launcher bundle.

## Version and build number

[Version.props](../Version.props) is the shared source for the CLI and Core release version, currently `1.5.0`. [Directory.Build.props](../Directory.Build.props) and [Directory.Build.targets](../Directory.Build.targets) derive the application version fields from that value. The vendored `Notion.Client` keeps its upstream package version.

For version `1.5.0`, build number `17`, and commit `<commit>`:

| Field | Value |
| --- | --- |
| `Version` and `PackageVersion` | `1.5.0` |
| `AssemblyVersion` and `FileVersion` | `1.5.0.17` |
| `InformationalVersion` and CLI `--version` identity | `1.5.0+build.17.sha.<commit>` |
| Native archive `VERSION.txt` | `1.5.0-build.17` |

Prereleases retain their suffix in the release and informational versions; assembly/file versions use the numeric release components. The commit suffix is included when source control information is available. Packaging resolves Git HEAD unless `-SourceRevisionId` is supplied explicitly.

Normal local and Visual Studio builds use build number **0**. Compiling does not edit a counter or increment the version. CI resolves `github.run_number` once in the metadata job and passes the same version, number, and commit to every native package, Docker image, and launcher bundle.

The workflow run number increases for each new run of this workflow. Pull-request and preview runs consume numbers too, so published release build numbers can have gaps. Rerunning jobs in the same workflow run keeps that run number; a new dispatch starts a new number. This is a workflow counter, not a counter of successful releases.

The build number and each numeric release component must be **0–65534** to fit assembly metadata. The scripts and MSBuild targets explicitly fail above that limit; they never wrap or silently reset the number. A maintainer must choose and implement a new counter policy before the workflow reaches the limit.

## Package locally

From the repository root in PowerShell:

```powershell
./build/package.ps1 -Runtime win-x64
```

This uses the version in `Version.props`, build `0`, and Git HEAD. The archive is written to `publish/<version>/artifacts/localnotion-win-x64.zip`. To supply a build identity and output directory explicitly:

```powershell
$commit = (git rev-parse HEAD).Trim()
./build/package.ps1 -Runtime linux-x64 -Version 1.5.0 -BuildNumber 17 -SourceRevisionId $commit -OutputDirectory ./publish/example/artifacts
```

The script publishes the complete self-contained `net8.0` output, includes native dependencies, then adds the platform installer, README, repository `LICENSE` and `COPYRIGHT`, `VERSION.txt`, and `localnotion-release.json`. Native archives use the repository's GPL license. No old proprietary EULA, credentials, or private Docker state is copied. Repackaging a RID into the same local output directory replaces its archive.

To package all supported RIDs locally:

```powershell
$platforms = Get-Content ./build/platforms.json -Raw | ConvertFrom-Json
foreach ($platform in $platforms.include) {
    ./build/package.ps1 -Runtime $platform.rid
}
```

Windows compatibility wrappers `publish-all.bat`, `publish-win-x64.bat`, and `publish-linux-x64.bat` call this same script and forward packaging options. They produce local files; they do not publish a release.

### Validate an archive

Run the smoke test on a host that can execute the packaged RID. For a local Windows x64 package of version 1.5.0:

```powershell
$commit = (git rev-parse HEAD).Trim()
./build/test-package.ps1 -ArchivePath ./publish/1.5.0/artifacts/localnotion-win-x64.zip -Runtime win-x64 -Version 1.5.0 -BuildNumber 0 -SourceRevisionId $commit
```

The helper validates the extracted metadata and executable, then checks the CLI version and help output. Default extraction uses a unique directory under `publish/.smoke` and is cleaned afterward. `-SkipExecution` checks archive structure and identity when the local host cannot execute its RID. `-ExtractDirectory` requires a new directory and keeps the extraction for external checks. Neither option installs the application.

### Docker and its Windows installer bundle

Build a local versioned Docker candidate:

```powershell
$commit = (git rev-parse HEAD).Trim()
docker build --platform linux/amd64 --build-arg VERSION=1.5.0 --build-arg BUILD_NUMBER=0 --build-arg "VCS_REF=$commit" -t local-notion:1.5.0 .
docker run --rm --network none local-notion:1.5.0 --version
```

Package the standalone Windows Docker launcher installer:

```powershell
./build/package-docker-launcher.ps1 -Version 1.5.0 -BuildNumber 0 -SourceRevisionId $commit
```

The bundle contains only the launcher source/scripts, installer entry points, README, license notices, and release metadata. Its root `install.ps1` pulls `ghcr.io/sphere10/local-notion:<version>`, verifies the image is available, then calls the bundled launcher installer with that image selected. It supports `-InstallDirectory` and `-NoPath`; a repository checkout is not required. It compiles the small Windows launcher inside the extracted bundle, so extraction must be in a writable directory. The Docker image itself is downloaded from GHCR and is not embedded in the ZIP.

## Platforms and release assets

[platforms.json](platforms.json) supplies the workflow matrix:

| RID | CI runner | Archive | Execution check |
| --- | --- | --- | --- |
| `win-x64` | `windows-latest` | `localnotion-win-x64.zip` | Windows x64 |
| `win-x86` | `windows-latest` | `localnotion-win-x86.zip` | Windows x86 |
| `win-arm64` | `windows-11-arm` | `localnotion-win-arm64.zip` | Windows ARM64 |
| `linux-x64` | `ubuntu-24.04` | `localnotion-linux-x64.tar.gz` | Linux x64 |
| `linux-arm64` | `ubuntu-24.04-arm` | `localnotion-linux-arm64.tar.gz` | Linux ARM64 |
| `linux-arm` | `ubuntu-24.04` | `localnotion-linux-arm.tar.gz` | ARM32 under QEMU |
| `osx-x64` | `macos-15-intel` | `localnotion-osx-x64.tar.gz` | macOS Intel |
| `osx-arm64` | `macos-15` | `localnotion-osx-arm64.tar.gz` | macOS Apple Silicon |

The ARM32 job first checks the archive without executing it, then runs the extracted executable in a `linux/arm/v7` .NET runtime-dependencies container under QEMU and verifies its full version identity. Other matrix jobs run their packaged executables directly. Windows x64 and Linux x64 also run the repository-path regression. Docker is built for `linux/amd64` and checked for version, help, and Git ownership behavior before its exact tested image is saved for publication.

Each published release contains these eight native archives plus:

- `localnotion-docker-windows.zip`: the standalone Windows Docker launcher installer.
- `release.json`: version, build, commit, image identity, and archive names, sizes, and SHA-256 values.
- `SHA256SUMS.txt`: checksums for the nine archives and `release.json`.

Asset names remain stable across releases. URLs under `https://github.com/Sphere10/LocalNotion/releases/latest/download/` therefore follow the current stable release. Select a specific release page when an exact version is required.

Native archives support portable use with all extracted files kept together. Windows `install.bat`/`install.ps1` can install under the current user's profile and update only user PATH; `-NoPath` skips PATH changes. Unix `install.sh` stores versions under the chosen prefix and manages its `localnotion` symlink. Both retain older version directories. Unix tar archives preserve executable permissions. These builds are unsigned; macOS builds are not notarized.

## How the CI/CD pipeline runs

The [release workflow](../.github/workflows/release.yml) has four stages:

1. **Resolve metadata.** The metadata job reads `Version.props`, checks the requested version or tag, resolves the exact commit, and assigns the workflow run number. It supplies one immutable identity and the matrix from `platforms.json` to all downstream jobs.
2. **Build in parallel.** Eight native jobs publish and archive the CLI for their RIDs while the Docker job builds the `linux/amd64` image and Windows launcher bundle. Each job receives the same version, build number, and commit.
3. **Test the deliverables.** Native jobs extract their archives and check metadata and execution; ARM32 runs under QEMU. Docker checks the candidate's version/help and Git ownership behavior. The workflow uploads all nine archive artifacts and saves the exact tested Docker image. A failed matrix or Docker check prevents the release job from running.
4. **Publish after validation.** For a publishing run, the release job downloads those outputs, validates the archives and saved image, and creates or resumes a matching draft. It uploads the artifacts and metadata, pushes the matching versioned Docker image, and verifies anonymous registry access before making the release public. Only a newer stable version can update both GitHub's latest selection and Docker `latest`.

### Triggers

| Trigger | Behavior |
| --- | --- |
| Pull request changing workflow, build, Docker, or application files | Build and test; upload preview artifacts; no publication |
| Manual **Run workflow**, `publish: false` | Build and test the selected ref; optional version override; no publication |
| Push of a `v*` tag | Publish only in `Sphere10/LocalNotion`, with a matching `Version.props` and a commit on the official default branch |
| Manual **Run workflow**, `publish: true` | Publication is allowed only in `Sphere10/LocalNotion` from its default branch; the version and existing-release checks still apply |

The normal release entry point below creates and pushes the tag that triggers automatic publication. The manually dispatched preview is useful before that tag exists.

### Permissions and registry setup

The local release helper uses your existing Git authentication to push to the official origin. Its atomic push needs permission to update master and create the release tag; repository branch/tag protection can reject it. No Notion integration credential is used by any build or release check.

Workflow jobs start with `contents: read`. Only the final release job requests `contents: write` for GitHub releases and `packages: write` for GHCR. It passes GitHub's automatic token as `GH_TOKEN` to GitHub CLI and authenticates Docker to `ghcr.io` using that same token. Do not add a personal access token or signing certificate to package files. Organization Actions policy must permit the workflow and these requested token permissions.

If the GHCR package already exists, its settings must allow this repository's workflow to write to it, either through inherited repository permissions or explicit **Manage Actions access**. For anonymous downloads, an organization owner must also make the package **Public**. A public repository alone does not change package visibility. See the [registry setup instructions](../docker/README.md#publishing-an-approved-image).

### Logs, artifacts, and failures

Open [Actions → Build and release Local Notion](https://github.com/Sphere10/LocalNotion/actions/workflows/release.yml). The run title includes its build number. Open a failed job and step to see validation output; native jobs are named `Package <rid>`. Successful preview outputs appear in the run's **Artifacts** list, including `native-<rid>` and `docker-launcher`. Published downloads and notes appear on [GitHub Releases](https://github.com/Sphere10/LocalNotion/releases).

Native and launcher workflow artifacts are retained for seven days; the saved tested Docker image is retained for one day. Retry failed publication jobs promptly in the same run so the exact tested outputs remain available. Fix a build/test failure before creating a new publishing attempt. If publication already created a draft, follow the identity and retry rules below; an expired or changed artifact is not permission to overwrite a release with rebuilt bytes.

## Preview the workflow

Open [Build and release Local Notion](https://github.com/Sphere10/LocalNotion/actions/workflows/release.yml), choose **Run workflow**, select the intended ref, and leave **publish** set to **false**. Leave **version** blank to use `Version.props`, or supply a preview version.

The preview builds and checks every package and the Docker candidate, and uploads workflow artifacts. It does not publish a GitHub release, push a container image, or update `latest`. Pull requests affecting the workflow, build system, Docker files, or application code run the same checks without publication. Workflow artifacts have limited retention; they are not release downloads.

## Publish a release

Commit and review all intended changes first. From a clean official checkout, preview the next release action:

```powershell
./build/release.ps1 -Version 1.5.1 -WhatIf
```

`-WhatIf` validates the checkout and proposed version. It may query the official origin and fetch master, but does not edit files, commit, tag, or push.

Publish with one command:

```powershell
./build/release.ps1 -Version 1.5.1
```

The helper checks that fetch and push URLs both identify `Sphere10/LocalNotion`, the checkout is clean including untracked files, HEAD descends from `origin/master`, and the version does not downgrade. It refuses an existing local or remote `v<version>` tag. It updates and commits only `Version.props` when needed, creates an annotated tag, then atomically pushes HEAD to master and the tag together. Normal remote permissions and branch protection still apply; no force push is used.

The tag starts [.github/workflows/release.yml](../.github/workflows/release.yml). Its metadata job requires the tag to match `Version.props` and checks that the release commit belongs to the official default branch. Publication waits for all native and Docker checks. A manual workflow dispatch can also request publication, but only from the official repository's default branch; use the release helper for the normal version-and-tag flow.

[publish-github-release.ps1](publish-github-release.ps1) validates the nine archives and saved Docker image, then publishes matching assets and image metadata. The version is immutable: an existing published release, a changed tag, or a conflicting versioned image is rejected. Do not move a published tag or reuse its version for changed binaries.

If publication stops while a draft exists, rerun the failed jobs in the **same workflow run**. A matching draft can resume only when version, build number, commit, image identity, and existing asset hashes agree. A fresh dispatch has a different build number and cannot take over that draft. Inspect an uncertain push or publication result before retrying; the local helper retains its commit and tag after a failed atomic push.

Stable releases update GitHub's latest release and Docker `latest` only when their semantic version is newer than the current stable selections. Prereleases and older stable releases retain explicit version tags without moving either latest pointer. Use the image digest in `release.json` or the workflow summary for immutable deployments.

Anonymous access to GHCR is checked before the draft is published. If organization policy prevents public packages, an owner must enable public package creation and make the package public; see the [Docker publishing and public-access guide](../docker/README.md#publishing-an-approved-image). Changing version tags does not resolve package visibility restrictions.
