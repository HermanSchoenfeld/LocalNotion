# Local Notion

**[Product page and documentation](https://sphere10.com/products/localnotion)**

Local Notion is an open-source command-line tool for keeping Notion content on your own storage and using it for backups, offline reading, websites, and applications.

The official repository is [Sphere10/LocalNotion](https://github.com/Sphere10/LocalNotion). The original [HermanSchoenfeld/LocalNotion](https://github.com/HermanSchoenfeld/LocalNotion) repository remains the upstream source.

## Get started with Docker

Use Docker to run Local Notion as a normal command or a background synchronization service. On Windows, start Docker Desktop in **Linux containers** mode. The official image targets **linux/amd64**.

The official stable image is `ghcr.io/sphere10/local-notion:latest`. [Local Notion 1.5.0](https://github.com/Sphere10/LocalNotion/releases/tag/v1.5.0) is publicly available with all downloads below. Newer stable releases update `latest` automatically.

```powershell
docker pull ghcr.io/sphere10/local-notion:latest
docker run --rm ghcr.io/sphere10/local-notion:latest --version
```

For a Windows `localnotion` command, download the [Docker launcher installer](https://github.com/Sphere10/LocalNotion/releases/latest/download/localnotion-docker-windows.zip), extract the entire ZIP, and run this from its directory:

```powershell
.\install.bat
```

The installer pulls the image version recorded in the bundle, installs the launcher for your user, and adds it to your user PATH. The bundle includes the launcher source and scripts; no repository checkout or separate .NET SDK is required. Open a new terminal, then run:

```powershell
localnotion --version
localnotion --help
```

For repeatable deployments, select an explicit image version such as `ghcr.io/sphere10/local-notion:1.5.0`. From a source checkout, `.\docker\install-cli.ps1 -Image local-notion:latest -BuildImage` builds and installs the local image.

The command uses your current folder, or the folder selected by `--path`. The background service keeps a separate repository under `.docker/data`. The [Docker guide](docker/README.md) covers both modes, token setup, importing data, updates, and [public registry access](docker/README.md#publishing-an-approved-image).

## Features

- **Local backups:** Download pages, databases, attachments, and underlying objects.
- **Continuous synchronization:** Poll Notion for changes and update your local repository.
- **HTML export:** Browse downloaded content without depending on Notion's renderer.
- **Custom themes:** Control the appearance of exported pages and databases.
- **Offline publications:** Create linked HTML for e-books, manuals, and other distributable content.
- **Website content:** Use Notion as a CMS and generate HTML for a web server.
- **Application integration:** Download and render content for users of your own backend.
- **Git history:** Track changes to local content through Git.

Workspace restore is listed as **coming soon** on the [Local Notion product page](https://sphere10.com/products/localnotion).

## Native downloads

Download a native archive to run Local Notion directly on Windows, Linux, or macOS. Unified releases use the same asset names, so these links follow the latest published GitHub release.

| Platform | Download |
| --- | --- |
| Windows x64 | [localnotion-win-x64.zip](https://github.com/Sphere10/LocalNotion/releases/latest/download/localnotion-win-x64.zip) |
| Windows x86 | [localnotion-win-x86.zip](https://github.com/Sphere10/LocalNotion/releases/latest/download/localnotion-win-x86.zip) |
| Windows ARM64 | [localnotion-win-arm64.zip](https://github.com/Sphere10/LocalNotion/releases/latest/download/localnotion-win-arm64.zip) |
| Linux x64 | [localnotion-linux-x64.tar.gz](https://github.com/Sphere10/LocalNotion/releases/latest/download/localnotion-linux-x64.tar.gz) |
| Linux ARM64 | [localnotion-linux-arm64.tar.gz](https://github.com/Sphere10/LocalNotion/releases/latest/download/localnotion-linux-arm64.tar.gz) |
| Linux ARM | [localnotion-linux-arm.tar.gz](https://github.com/Sphere10/LocalNotion/releases/latest/download/localnotion-linux-arm.tar.gz) |
| macOS Intel | [localnotion-osx-x64.tar.gz](https://github.com/Sphere10/LocalNotion/releases/latest/download/localnotion-osx-x64.tar.gz) |
| macOS Apple Silicon | [localnotion-osx-arm64.tar.gz](https://github.com/Sphere10/LocalNotion/releases/latest/download/localnotion-osx-arm64.tar.gz) |

Each unified release includes release notes, [SHA-256 checksums](https://github.com/Sphere10/LocalNotion/releases/latest/download/SHA256SUMS.txt), and [release metadata](https://github.com/Sphere10/LocalNotion/releases/latest/download/release.json). Browse [all releases](https://github.com/Sphere10/LocalNotion/releases) for earlier versions.

Extract the complete archive and keep native libraries and supporting files beside the executable. Run `.\localnotion.exe --help` on Windows or `./localnotion --help` on Linux/macOS for portable use. A separate .NET installation is not required.

For installation under your user account, run `.\install.bat` on Windows or `sh ./install.sh` on Linux/macOS from the extracted directory. Windows supports `-InstallRoot` and `-NoPath`; Unix supports `--prefix`. The included README explains the version directories and PATH setup. These helpers retain earlier installations. macOS archives are unsigned and are not notarized by Apple.

## Screenshots

[![Local Notion Screenshot 1](https://sphere10.com/files/3effe161-c70b-486e-9921-7e26b5fee9dd/Screenshot_1_-_1366x768.webp)](https://sphere10.com/products/localnotion)

[![Local Notion Screenshot 2](https://sphere10.com/files/21d5ba17-2a9c-4be9-940f-452be3ff433f/Screenshot_2_-_1366x768.webp)](https://sphere10.com/products/localnotion)

[![Local Notion Screenshot 3](https://sphere10.com/files/2038f1c0-2830-422b-baa7-d4132ca8ccce/Screenshot_3_-_1366x768.webp)](https://sphere10.com/products/localnotion)

## Documentation & Resources

Use the [Local Notion product page](https://sphere10.com/products/localnotion) for the product overview, and these guides for setup and operation:

- [How Local Notion Works](https://sphere10.com/products/localnotion/how-local-notion-works): repositories, stored files, themes, and rendering profiles.
- [Getting Started](https://sphere10.com/products/localnotion/getting-started): create a Notion integration and grant it access to your content.
- [Local Notion Manual](https://sphere10.com/products/localnotion/local-notion-manual): installation, CLI commands, and rendering options.

## Build From Source

### Prerequisites

- Windows, Linux, or macOS
- **.NET SDK 8.0+**
- Visual Studio 2022+ (optional, but recommended for Windows)

> **Note:** This repository includes projects targeting both **.NET 8** and **.NET Standard 2.0**. Install the .NET 8 SDK to build the full solution.

### Build (CLI)

From the repository root:

```bash
# Restore dependencies
dotnet restore

# Debug build
dotnet build -c Debug

# Release build
dotnet build -c Release
```

### Run

You can run the CLI project directly:

```bash
dotnet run --project LocalNotion.CLI/LocalNotion.CLI.csproj -c Debug -- --help
```

### Build (Visual Studio)

1. Open the solution (`LocalNotion.sln`) in Visual Studio.
2. Set `LocalNotion.CLI` as the startup project.
3. Build with **Build > Build Solution**.
4. Run with **Debug > Start Without Debugging**.

The **Other > Docker** solution folder contains the Docker configuration and Windows launcher source, scripts, and guide. These are solution items for browsing and editing. Use the [launcher installer](docker/README.md#how-the-windows-launcher-is-built) to build and install the Docker-backed Windows command; a normal solution build builds the application projects.

## CI/CD and publishing releases

The [CI/CD and release guide](build/README.md) explains the build stages, supported platforms, test runs, permissions, release assets, and failure recovery. The pipeline lives in [.github/workflows/release.yml](.github/workflows/release.yml); its scripts and version settings are available in **Other → Build and Release** in Visual Studio.

`Version.props` holds the release version, starting at **1.5.0**. GitHub Actions supplies one build number for every artifact in a run; local builds default to `0`. The CLI's `--version` includes the release version, build number, and source commit.

From a clean checkout with PowerShell 7, publish the next version with:

```powershell
.\build\release.ps1 -Version 1.5.1 -WhatIf
.\build\release.ps1 -Version 1.5.1
```

The command updates the version when needed, commits that change, and pushes the release tag. GitHub Actions builds and tests all packages, then publishes the downloads to GitHub Releases and the image to GHCR. Use the workflow's manual **Run workflow** option with **publish disabled** to test the pipeline before a release.

## Usage

Local Notion is operated via a command-line interface that works similarly to Git.

### Quick Start

1. **Create a Notion Integration** in the [Notion Developer Portal](https://www.notion.so/my-integrations).
2. **Copy the integration token** (Internal Integration Secret).
3. **Share pages/databases** with your integration so it can access them.

### Initialize a Local Notion Repository

```bash
localnotion init -k secret_YourIntegrationToken
```

### List Available Content

```bash
# List top-level objects
localnotion list

# List all objects (including children)
localnotion list --all
```

### Pull Content

```bash
# Pull entire workspace
localnotion pull --all

# Pull specific object by ID
localnotion pull -o 33c6a405-2b1e-4bd6-82a0-236c820cc8a3
```

### Sync (Auto-Backup)

```bash
# Continuously sync every 30 seconds (default)
localnotion sync --all

# Custom poll frequency (every 10 seconds)
localnotion sync --all -f 10
```

### Git change tracking

For a repository initialized with `--git`, Local Notion stages and commits local changes after supported operations. With `--git-push`, it also pushes to the configured upstream. Git identity, remote configuration, and authentication must be available to the account running Local Notion.

Local Notion passes `-c safe.directory=<selected-repository>` to each Git command. This trusts the explicitly selected repository for that command only, including when restored files, a different operating-system account, or a container mount gives the repository a different owner. It does not change global or system Git configuration or trust every repository. This behavior is shared by native Windows/Linux builds and Docker.

Git's `detected dubious ownership` error is an ownership check, not a timeout or synchronization delay; it can stop staging before commit or push is reached. See [Git's safe.directory documentation](https://git-scm.com/docs/git-config#Documentation/git-config.txt-safedirectory). Update Local Notion to a build containing this fix; Docker installations can select a current image from [GitHub Releases](https://github.com/Sphere10/LocalNotion/releases) or run `.\docker\install-cli.ps1 -Image local-notion:latest -BuildImage` from the source checkout.

### Render Content

```bash
# Re-render a specific page
localnotion render -o 33c6a405-2b1e-4bd6-82a0-236c820cc8a3

# Re-render all content
localnotion render --all
```

### Available Commands

```
status     Provides status of the Local Notion repository
init       Creates a Local Notion repository
clean      Cleans your local Notion repository by removing dangling pages, files and databases
remove     Remove resources from a Local Notion repository
list       Lists objects from Notion which can be pulled into Local Notion
sync       Synchronizes a Local Notion repository with Notion (until process manually terminated)
pull       Pulls Notion objects into a Local Notion repository
render     Renders a Local Notion object (using local state only)
prune      Removes objects from a Local Notion that no longer exist in Notion
license    Manages Local Notion license
help       Display more information on a specific command
version    Display version information
```

For detailed help on any command:

```bash
localnotion help <command>
```

## Rendering Modes

Local Notion supports multiple rendering modes for different use cases:

| Mode | Description |
|------|-------------|
| **Backup** | Default mode. Downloads content with local file-based URLs. |
| **Offline** | Like Backup, but also downloads externally linked resources (images, videos). |
| **Publishing** | Like Offline, but with a simplified directory structure for distributable content. |
| **Website** | Generates URLs suitable for web server hosting. Ideal for CMS use cases. |

Specify the mode when initializing your repository:

```bash
localnotion init -k secret_YourToken -x website
```

## Repository Layout

A Local Notion repository contains:

- `databases/` — rendered database HTML files
- `files/` — file attachments
- `pages/` — rendered page HTML files
- `workspaces/` — rendered workspace HTML files
- `.localnotion/` — internal data (objects, graphs, themes, registry, logs)

## Troubleshooting

- **401/403 Unauthorized**: Token is invalid or the page/database is not shared with the integration.
- **Missing content**: Ensure the desired pages/databases are shared with your integration and you're referencing the correct object IDs.
- **Rate limits/timeouts**: Retry later or reduce the scope of a pull; large workspaces may need multiple runs.
- **Build failures**: Verify SDK install with `dotnet --info`, then run `dotnet restore` and `dotnet build`.

## Contributing

Contributions are welcome!

- Keep changes small and focused.
- Follow formatting rules from `.editorconfig`.
- Add/update tests where applicable.

See [CONTRIBUTING.md](CONTRIBUTING.md) for more details.

## License

This project is licensed under the **GNU GPL v3.0** (or later).

- See [`LICENSE`](LICENSE)
- Copyright details: [`COPYRIGHT`](COPYRIGHT)

## Credits

**Author:** Herman Schoenfeld (<herman@sphere10.com>)

**Website:** [https://sphere10.com/products/localnotion](https://sphere10.com/products/localnotion)

**Copyright:** © Herman Schoenfeld 2018 - Present. All rights reserved.
