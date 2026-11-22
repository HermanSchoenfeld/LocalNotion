# Local Notion

<picture>
  <source media="(prefers-color-scheme: dark)" srcset="assets/branding/local-notion-logo-dark.svg">
  <img src="assets/branding/local-notion-logo.svg" alt="Local Notion" width="420">
</picture>

**[Product page and documentation](https://sphere10.com/products/localnotion)**

Local Notion is an open-source command-line tool for keeping Notion content on your own storage and using it for backups, offline reading, websites, and applications.

The official repository is [Sphere10/LocalNotion](https://github.com/Sphere10/LocalNotion). The original [HermanSchoenfeld/LocalNotion](https://github.com/HermanSchoenfeld/LocalNotion) repository remains the upstream source.

## Features

- **Local backups:** Download pages, databases, attachments, and underlying objects.
- **Continuous synchronization:** Poll Notion for changes and update your local repository.
- **HTML export:** Browse downloaded content without depending on Notion's renderer.
- **Mermaid diagrams:** Render diagrams authored in native Notion Code blocks, with captions and independent error handling. Requires a [Mermaid-enabled build](docs/renderer.md#availability).
- **Custom themes:** Control the appearance of exported pages and databases.
- **Offline publications:** Create linked HTML for e-books, manuals, and other distributable content.
- **Website content:** Use Notion as a CMS and generate HTML for a web server.
- **Application integration:** Download and render content for users of your own backend.
- **Git history:** Track changes to local content through Git.

Workspace restore is listed as **coming soon** on the [Local Notion product page](https://sphere10.com/products/localnotion).

## Standalone renderer

LocalNotion uses the MIT-licensed Sphere10.Framework.Web.VisualRenderer library. Workspace builds use its updated source; package builds require an explicitly selected compatible version. See [rendering integration](docs/renderer.md).

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

See the [changelog](CHANGELOG.md) for command, CMS and packaging changes in version 1.7.0.

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
- [CMS guide](docs/cms/index.md): create a website database, understand its fields, compose pages and shared framing, select themes, and publish a Notion-authored website.

### Create a website CMS

Available from Local Notion **1.7.0**, `create-cms` creates one empty Notion database with the canonical website schema and preset Type, Status, Tags, and Themes options. Connect your Notion integration to the intended parent page first. Put its token in a private file and set `NOTION_API_KEY_FILE` to that file's path, then run:

```bash
localnotion create-cms --parent <notion-page-id> --title "My Website"
```

The command prints the new database ID and URL. It leaves zero active content rows; one temporary setup row is retained in Notion's Trash to work around its 100-option request limit. It does not copy existing website content, initialize a local repository, or publish a website. See [Create a CMS](docs/cms/create-a-cms.md) for credentials, required permissions, version availability, and the next steps. [Build your first website](docs/cms/first-website.md) provides a complete example.

Current development builds include refined and standard menu layouts, optional linked product cards, compact/wide modifiers and an auxiliary-colour menu surface. Creating or opening a local repository installs their matching templates and public assets while preserving existing overrides. Choose these options on the NavBar row's **Themes**, with the existing `color_aux_*` palette where needed. See [menu layout and colours](docs/cms/headers-and-navigation.md#choose-the-menu-layout-and-colours).

## Build From Source

### Prerequisites

- Windows, Linux, or macOS
- **.NET 11 SDK** (the SDK version is selected by [global.json](global.json))
- Visual Studio 2026 (18.0+) for optional Windows IDE builds

> **Note:** This repository includes projects targeting both **.NET 11** and **.NET Standard 2.0**. Install the .NET 11 SDK selected by global.json to build the full solution.

### Framework dependency and build modes

LocalNotion has no Enterprise package, license activation, repository quota or
page quota. It uses the MIT Framework application lifecycle and the independently
MIT Sphere10.Framework.Web.VisualRenderer library.

The default build uses **post-DRM-extraction Framework source** from a sibling
`../Framework` directory. Set `Sphere10FrameworkRoot` to use another checkout.
It must contain `src/Sphere10.Framework`, `src/Sphere10.Framework.Data` and
`src/Sphere10.Framework.Application`, with the DRM implementation removed from
Application. No Enterprise checkout, private package feed or product key is needed.

The clean Framework source, including the visual renderer, is public in
[HermanSchoenfeld/Sphere10.Framework](https://github.com/HermanSchoenfeld/Sphere10.Framework).
The exact tested commit is recorded in [build/framework-source.ref](build/framework-source.ref).
Use that pin when reproducing the source build. Framework.Application 3.1.2 still
contains legacy DRM and is not supported. Package builds require one exact,
tested clean Framework version for all references. See the
[Framework publishing guide](https://github.com/HermanSchoenfeld/Sphere10.Framework/blob/master/docs/nuget-publishing.md)
for stable and prerelease installation. Source changes do not update installed
LocalNotion executables or publish a LocalNotion release.

### Build (CLI)

From the repository root, with the clean `../Framework` checkout containing
`Sphere10.Framework.Web.VisualRenderer` available:

```bash
dotnet build LocalNotion.sln -c Release -p:BuildNumber=0 -p:BuildRevision=0
dotnet test tests/LocalNotion.Core.Tests/LocalNotion.Core.Tests.csproj -c Release -p:BuildNumber=0 -p:BuildRevision=0
```

For a different source location, add
`-p:Sphere10FrameworkRoot=/absolute/path/to/Framework`. Explicit build numbers
avoid incrementing the checkouts' local build counters. See [renderer build modes](docs/renderer.md#building) for the shared Framework source/package configuration.

Package mode restores a **separately produced and verified clean package set**
from the private Gitea NuGet registry, without a Framework source checkout.
Configure its service-index URL and credentials in your user-level NuGet
configuration under the source name `sphere10-gitea-private`. Keep credentials
outside this repository. [NuGet.Config](NuGet.Config) maps Framework packages to
that source and third-party packages to NuGet.org; source builds still need no
private feed.

Select one exact clean Framework version explicitly for restore, build and test:

```bash
dotnet build LocalNotion.sln -c Release -p:BuildNumber=0 -p:BuildRevision=0 -p:UseSphere10FrameworkSource=false -p:Sphere10FrameworkVersion=3.2.0-ci.160.43660265392.1
dotnet test tests/LocalNotion.Core.Tests/LocalNotion.Core.Tests.csproj -c Release -p:BuildNumber=0 -p:BuildRevision=0 -p:UseSphere10FrameworkSource=false -p:Sphere10FrameworkVersion=3.2.0-ci.160.43660265392.1
```

The version must be 3.1.3 or later and provide all four matching Framework
packages, including the visual renderer. No default version, Enterprise/DRM
dependency or fallback to 3.1.2 is supplied. Package availability and compatibility
must be verified independently of source-mode builds.

### Run

```bash
dotnet run --project LocalNotion.CLI/LocalNotion.CLI.csproj -c Debug -p:BuildNumber=0 -p:BuildRevision=0 -- --help
```

### Build (Visual Studio)

1. Open the solution (`LocalNotion.sln`) in Visual Studio.
2. Set `LocalNotion.CLI` as the startup project.
3. Build with **Build > Build Solution**.
4. Run with **Debug > Start Without Debugging**.

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

Local Notion **1.7.0 and later** provides `localnotion git` to inspect or change these settings without initializing, committing or pushing:

```sh
localnotion git --path "./website"
localnotion git --path "./website" --enabled true --push false
```

Earlier releases, including `v1.6.3` (`1.6.3.7`), do not include this command. Run `localnotion git --help` to check your selected executable. See [Git configuration](docs/git.md) for all options, publication effects and standard Git setup.

Local Notion passes `-c safe.directory=<selected-repository>` to each Git command. This trusts the explicitly selected repository for that command only, including when restored files, a different operating-system account, or a container mount gives the repository a different owner. It does not change global or system Git configuration or trust every repository. This behavior is shared by native Windows/Linux builds and Docker.

Git's `detected dubious ownership` error is an ownership check, not a timeout or synchronization delay; it can stop staging before commit or push is reached. See [Git's safe.directory documentation](https://git-scm.com/docs/git-config#Documentation/git-config.txt-safedirectory). Update Local Notion using the [native downloads](#native-downloads) or [Docker setup instructions](#get-started-with-docker).

### Render Content

```bash
# Re-render a specific page
localnotion render -o 33c6a405-2b1e-4bd6-82a0-236c820cc8a3

# Re-render all content
localnotion render --all
```

### Mermaid Diagrams

In Notion, insert a native **Code** block and select **Mermaid** as its language. Write the diagram definition in that block and optionally add a caption. The normal `pull` and `render` commands generate the diagram; no extra command, CMS Theme selection or injected page script is required.

The browser renders the diagram from source that stays hidden in the page. An invalid definition shows an explanatory message without stopping other diagrams. Diagrams require JavaScript, and without it a diagram stays unrendered with its source hidden. Include the generated public `render-assets/` directory when publishing or moving the HTML.

See the [CMS authoring example](docs/cms/index.md#add-mermaid-diagrams) and [rendering and build requirements](docs/renderer.md#mermaid-code-blocks). Updated local builds and public GitHub/Docker releases have separate availability; check the documented renderer requirement for the executable you use.

### Available Commands

```
status     Provides status of the Local Notion repository
init       Creates a Local Notion repository
git        Displays or changes repository Git settings
create-cms Creates an empty Notion website database with the canonical CMS schema
clean      Cleans your local Notion repository by removing dangling pages, files and databases
remove     Remove resources from a Local Notion repository
list       Lists objects from Notion which can be pulled into Local Notion
sync       Synchronizes a Local Notion repository with Notion (until process manually terminated)
pull       Pulls Notion objects into a Local Notion repository
render     Renders a Local Notion object (using local state only)
prune      Removes objects from a Local Notion that no longer exist in Notion
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
- `.localnotion/` — internal data (objects, graphs, registry, logs, generated render assets)
- `.localnotion/themes/` — deployed built-in themes and local overrides; missing files are restored when LocalNotion creates or opens a repository, while existing files are preserved

## Independent rendering

Rendering is supplied by the MIT-licensed Sphere10.Framework.Web.VisualRenderer package. See [rendering integration](docs/renderer.md) and [theme overrides](docs/renderer-themes.md).

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

Maintainers: see the [release and deployment guide](build/README.md) for CI/CD, versioning, and the next-release checklist.

## License

LocalNotion source code is licensed under the **GNU GPL v3.0** (or later), except separately licensed materials. **Sphere10.Framework.Web.VisualRenderer** and the clean **Sphere10 Framework** dependencies are MIT-licensed. Preserve their license and third-party notices when distributing binaries.

The [VisualRenderer linking exception](COPYING.EXCEPTION) grants additional permission only for copyrights Herman Schoenfeld or Sphere 10 Software Pty Ltd own or are authorized to license. It does not grant permission on behalf of other contributors or override third-party licenses. See [renderer licensing and distribution](docs/renderer.md#licensing-and-distribution).

- See [`LICENSE`](LICENSE)
- Copyright details: [`COPYRIGHT`](COPYRIGHT)
- Renderer linking exception: [`COPYING.EXCEPTION`](COPYING.EXCEPTION)

## Credits

**Author:** Herman Schoenfeld (<herman@sphere10.com>)

**Website:** [https://sphere10.com/products/localnotion](https://sphere10.com/products/localnotion)

**Copyright:** © Herman Schoenfeld 2018 - Present. All rights reserved.

## Get started with Docker

Use Docker to run Local Notion as a normal command or a background synchronization service. On Windows, start Docker Desktop in **Linux containers** mode. The official image targets **linux/amd64**.

The official stable image is `ghcr.io/sphere10/local-notion:latest`. Native archives and the Docker launcher bundle are published together on [GitHub Releases](https://github.com/Sphere10/LocalNotion/releases). Newer stable releases update `latest` automatically.

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

For repeatable deployments, select an explicit published image version such as `ghcr.io/sphere10/local-notion:1.7.0`, or the immutable image digest recorded in that release's metadata. From a source checkout, `.\docker\install-cli.ps1 -Image local-notion:latest -BuildImage` builds and installs the local image.

The command uses your current folder, or the folder selected by `--path`. The background service keeps a separate repository under `.docker/data`. The [Docker guide](docker/README.md) covers both modes, token setup, importing data, updates, and Git authentication.

The **Other > Docker** solution folder contains the Docker configuration and Windows launcher source, scripts, and guide. These are solution items for browsing and editing. Use the [launcher installer](docker/README.md#how-the-windows-launcher-is-built) to build and install the Docker-backed Windows command; a normal solution build builds the application projects.
