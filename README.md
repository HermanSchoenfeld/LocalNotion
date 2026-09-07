# Local Notion

**[Product page, downloads, and documentation](https://sphere10.com/products/localnotion)**

Local Notion is an open-source command-line tool for keeping Notion content on your own storage and using it for backups, offline reading, websites, and applications.

The official repository is [Sphere10/LocalNotion](https://github.com/Sphere10/LocalNotion). The original [HermanSchoenfeld/LocalNotion](https://github.com/HermanSchoenfeld/LocalNotion) repository remains the upstream source.

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

## Downloads

Pre-built binaries are available for multiple platforms:

- **Windows**: [Download (x64)](https://sp10-downloads.s3.us-west-1.amazonaws.com/localnotion/1.3/localnotion-win-x64-1.3.zip)
- **macOS**: [Download (x64)](https://sp10-downloads.s3.us-west-1.amazonaws.com/localnotion/1.3/localnotion-osx-x64-1.3.zip)
- **Linux**: [Download (x64)](https://sp10-downloads.s3.us-west-1.amazonaws.com/localnotion/1.3/localnotion-linux-x64-1.3.zip)
- **Other platforms**: [All Downloads](https://sphere10.com/products/localnotion/downloads)

## Official Local Notion Docker

Use the Docker image either as a normal `localnotion` command or as a background synchronization service. Docker Desktop must be running in Linux containers mode on Windows.

Install the command for your Windows account from this repository:

```powershell
.\docker\install-cli.ps1
```

Open a new terminal, then run:

```powershell
localnotion --version
localnotion --help
```

The command runs the container against your current folder, or the folder selected by `--path`. The background service uses its own repository under `.docker/data`. The [Docker guide](docker/README.md) explains both modes, token setup, importing data, updates, and publishing images after approval.

## Screenshots

[![Local Notion Screenshot 1](https://sphere10.com/files/3effe161-c70b-486e-9921-7e26b5fee9dd/Screenshot_1_-_1366x768.webp)](https://sphere10.com/products/localnotion)

[![Local Notion Screenshot 2](https://sphere10.com/files/21d5ba17-2a9c-4be9-940f-452be3ff433f/Screenshot_2_-_1366x768.webp)](https://sphere10.com/products/localnotion)

[![Local Notion Screenshot 3](https://sphere10.com/files/2038f1c0-2830-422b-baa7-d4132ca8ccce/Screenshot_3_-_1366x768.webp)](https://sphere10.com/products/localnotion)

## Documentation & Resources

Use the [Local Notion product page](https://sphere10.com/products/localnotion) for the product overview and downloads, and these guides for setup and operation:

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

Git's `detected dubious ownership` error is an ownership check, not a timeout or synchronization delay; it can stop staging before commit or push is reached. See [Git's safe.directory documentation](https://git-scm.com/docs/git-config#Documentation/git-config.txt-safedirectory). Update Local Notion to a build containing this fix; Docker installations can run `.\docker\install-cli.ps1 -BuildImage` from the source checkout.
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