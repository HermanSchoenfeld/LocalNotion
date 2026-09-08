# Official Local Notion Docker

Run Local Notion on your own computer or server using the image built from the official [Sphere10/LocalNotion repository](https://github.com/Sphere10/LocalNotion). The original [HermanSchoenfeld/LocalNotion repository](https://github.com/HermanSchoenfeld/LocalNotion) remains the upstream source. See the [product page](https://sphere10.com/products/localnotion) for the application overview and documentation. The root `Dockerfile` replaces the earlier `Containerfile`; `.dockerignore` excludes local data, secrets, and build output from the image.

The image runs the Local Notion CLI. It synchronizes Notion content and renders HTML into persistent storage. It does not include a web interface or an HTTP server, and Compose does not expose any ports.

## Choose how to run

| | Foreground command | Background sync service |
| --- | --- | --- |
| Start it with | `localnotion <command>` | `docker/localnotion.ps1 -Action Install` or `Start` |
| Runs for | One command; `sync` continues until Ctrl+C | Repeated synchronization while Docker is running |
| Repository | Your current folder or the host folder passed to `--path` | This checkout's `.docker/data` |
| Data changes | Directly updates the folder you selected | Updates the service repository; `Import` creates a separate copy |
| Use it for | Manual pulls, rendering, scripts, or foreground sync | Unattended backups and HTML generation |

The source-checkout helpers default to the local `local-notion:latest` image. The release bundle selects its published version of `ghcr.io/sphere10/local-notion`. Installing the command does not require starting the background service. Use one writer per repository: stop background sync before running commands against its `.docker/data` folder.

## Windows requirements

Start Docker Desktop in **Linux containers** mode. The image targets **linux/amd64**. The command launcher uses local Docker Desktop; it does not run against a remote Docker host. A separate Local Notion application or .NET SDK installation is not required.

## Download a released image

The [unified release workflow](../.github/workflows/release.yml) publishes the official **`ghcr.io/sphere10/local-notion`** image for **linux/amd64**, together with native archives and the Windows Docker installer bundle. [Release 1.5.0](https://github.com/Sphere10/LocalNotion/releases/tag/v1.5.0) is public, including the `1.5.0` and `latest` image tags. Newer stable releases update `latest` automatically.

```powershell
docker pull ghcr.io/sphere10/local-notion:latest
docker run --rm ghcr.io/sphere10/local-notion:latest --version
```

To select a particular published version instead of following `latest`, use its version tag. For example, after 1.5.0 is published:

```powershell
docker pull ghcr.io/sphere10/local-notion:1.5.0
docker run --rm ghcr.io/sphere10/local-notion:1.5.0 --version
```

Use the image digest recorded in the release metadata or workflow summary when an exact immutable image reference is needed. The official image supports anonymous pulls. If a pull is denied, check the image name and tag; maintainers can check [registry access settings](../build/README.md#public-container-registry-access).

## Install the localnotion command

Download the [Windows Docker installer bundle](https://github.com/Sphere10/LocalNotion/releases/latest/download/localnotion-docker-windows.zip), extract the complete ZIP, and run this in PowerShell from the extracted directory:

```powershell
.\install.bat
```

The bundle contains its own launcher source, scripts, and release metadata. Its installer pulls the bundled image version before invoking the launcher installer, so a repository checkout and a separate .NET SDK are not required.

If you already have a source checkout, you can select a published image explicitly. Pull it before running the checkout's installer:

```powershell
docker pull ghcr.io/sphere10/local-notion:1.5.0
.\docker\install-cli.ps1 -Image ghcr.io/sphere10/local-notion:1.5.0
```

For a local source build, use:

```powershell
.\docker\install-cli.ps1 -Image local-notion:latest -BuildImage
```

The checkout installer selects `local-notion:latest` on a first installation without options and builds it if missing. On later runs, it retains the configured image unless you pass `-Image`. This source-build behavior is separate from the release bundle's initial image pull.

The launcher is installed under `%LOCALAPPDATA%\Sphere10\LocalNotion\bin`, which is added to your user `PATH`. Administrator access is not required. Open a **new terminal** after installation:

```powershell
Get-Command localnotion
localnotion --version
localnotion --help
```

The command runs from the installed files without the extracted bundle or checkout. Repository folders and token files must remain at their configured paths. Each invocation uses the selected local image; it does not download updates automatically. Installing a released image for command mode does not change the background service's local `local-notion:latest` image or `local-notion` container name.

### How the Windows launcher is built

The installed command is built from [LocalNotionDockerLauncher.cs](LocalNotionDockerLauncher.cs). Its execution chain is:

```text
localnotion.exe → Windows PowerShell → localnotion-docker.ps1 → docker.exe → Local Notion CLI in the container
```

The C# wrapper preserves your arguments and current directory, starts the adjacent [PowerShell launcher](localnotion-docker.ps1), and returns its exit code. The script selects the local image, maps the repository and optional token file, and manages the temporary container.

[install-cli.ps1](install-cli.ps1) compiles the wrapper with Windows' .NET Framework `csc.exe`, using `%WINDIR%\Microsoft.NET\Framework64\v4.0.30319\csc.exe` or the corresponding `Framework` path. The generated executable goes into `.docker/cli-build/localnotion.exe`, which is ignored by Git. The installer then puts four files under `%LOCALAPPDATA%\Sphere10\LocalNotion\bin`:

| Installed file | Purpose |
| --- | --- |
| `localnotion.exe` | Compiled C# command wrapper |
| `localnotion-docker.ps1` | Copy of the PowerShell launcher |
| `localnotion-docker.json` | Image, state-volume name, and repository-to-token-file path mappings; no token contents |
| `localnotion-docker-install.json` | Installation identity and location used for updates and uninstall |

In [LocalNotion.sln](../LocalNotion.sln), **Other > Docker** groups the launcher source, installer, Docker configuration, guide, and workflow as solution items. This is a virtual folder for browsing and editing; the files keep their existing locations on disk. Building the solution builds the application projects. Creating or updating the Windows launcher remains an explicit installer step, so ordinary cross-platform .NET builds do not install commands or change `PATH`.

After editing the wrapper or launcher script, rerun `.\docker\install-cli.ps1` to compile and install the changes. It reuses an existing image. When the application source or Docker image definition changes, run `.\docker\install-cli.ps1 -Image local-notion:latest -BuildImage` to build and select the local source image.

### Run commands against a repository

Create an empty folder for a new repository, then initialize it:

```powershell
New-Item -ItemType Directory -Path "$env:USERPROFILE\Notion" -Force
Set-Location "$env:USERPROFILE\Notion"
localnotion init
localnotion status
```

For commands that contact Notion, set `LOCALNOTION_TOKEN_FILE` to an existing private file containing your integration token. This variable holds a **host file path**, not the token itself:

```powershell
$env:LOCALNOTION_TOKEN_FILE = 'C:\private\notion-token'
localnotion list --all
localnotion pull --all
localnotion render --all
```

The launcher mounts the token file read-only. Alternatively, a credential already saved in the repository can be used. If installation finds an imported service repository and its secret, it remembers that token file for that exact repository folder. For other folders, provide their token file with `LOCALNOTION_TOKEN_FILE` or use their saved repository credential. The integration must have access to the Notion content you want to synchronize. `init` can create the repository without a token; `list`, `pull`, and `sync` need one.

To use another folder without changing your current directory, pass its Windows path:

```powershell
localnotion status --path 'C:\Notion\Work'
localnotion pull --all --path 'C:\Notion\Work'
```

These commands operate **directly on that folder**. They do not import a copy. The launcher maps the selected host folder into the container and rewrites the CLI repository path automatically, so use Windows paths with `localnotion`.

### Existing Windows repositories

You can open an existing self-contained Windows repository directly with `localnotion`; importing a copy is optional. Windows backslashes in relative storage and render paths are accepted. Local Notion normalizes these paths in memory when loading the repository, and normal repository saves store forward slashes that work on Windows and Linux.

The launcher checks saved paths before opening the repository and rejects absolute paths or paths that escape its folder. All repository content must be available inside the selected folder; external storage needs a separate Docker configuration with explicit mounts. The launcher does not rewrite a rejected registry.

If you installed an earlier launcher, update both the launcher and application image from the current source checkout:

```powershell
.\docker\install-cli.ps1 -Image local-notion:latest -BuildImage
```

To work with a separate copy, stop the background service and use the import workflow below. If the imported source had no saved token, run the service helper's `Configure` action. Then rerun `.\docker\install-cli.ps1` so it records the copied repository's token-file mapping, and run commands from this checkout's `.docker/data` folder. Keep the service stopped while using that same folder with `localnotion`.

Git, NGINX, and Apache settings in a directly opened repository remain as saved; any enabled commands must work inside the Linux container. Local Notion supplies command-scoped `safe.directory` for the selected repository, so different host/container ownership does not block its Git operations. The same fix applies to native installations; see [Git change tracking](../README.md#git-change-tracking). Git identity and authentication still belong to the environment running the command. The import workflow disables those integrations in the copy. Extra filesystem options such as `--override-objects-path` and `--cancel-trigger` require a separate Docker configuration with the necessary mounts and are not supported by this launcher.

### Git credentials when Windows uses PuTTY / Pageant

Pageant's Windows agent and PuTTY's saved host keys are not forwarded into the Linux container by this launcher. The image includes OpenSSH and uses it for Git even if a repository's Windows configuration selects Plink. Give Docker its own SSH identity, or configure a separate SSH-agent bridge if you need to reuse Pageant.

The default `local-notion-state` volume stores the container user's home at `/var/lib/localnotion`, including `.gitconfig`, `.ssh/known_hosts`, and private keys you create there. Both command mode and the background service use this volume. These files survive temporary containers and image rebuilds; they are not included in the image. Use your configured volume name in the examples if you changed `stateVolume`. Treat its backups as private. Attach this same volume when generating the key and accepting a verified server host key; files saved in a temporary container without this mount disappear when that container is removed.

#### Create a Docker SSH identity

Use a current released image, or rebuild an older local image from the source checkout first:

```powershell
.\docker\install-cli.ps1 -Image local-notion:latest -BuildImage
```

Open an interactive setup shell. This mounts only the state volume. If you selected a released image, replace `local-notion:latest` in the direct Docker commands below with `ghcr.io/sphere10/local-notion:1.5.0` (or your selected version or digest):

```powershell
docker run --rm -it --pull never --entrypoint sh `
  --mount type=volume,source=local-notion-state,target=/var/lib/localnotion `
  local-notion:latest
```

Run these Linux commands inside that shell, substituting your Git commit identity:

```sh
git config --global user.name "Your Name"
git config --global user.email "you@example.com"
umask 077
mkdir -p "$HOME/.ssh"
chmod 700 "$HOME/.ssh"
if [ -e "$HOME/.ssh/id_ed25519" ] || [ -e "$HOME/.ssh/id_ed25519.pub" ]; then
  echo 'An SSH key already exists; keeping it.'
else
  ssh-keygen -t ed25519 -C "Local Notion Docker" -f "$HOME/.ssh/id_ed25519"
fi
cat "$HOME/.ssh/id_ed25519.pub"
```

The name and email identify commits; they do not authenticate to the server. A repository's local Git identity overrides these global defaults. The key command preserves an existing key; review it before deciding to reuse it. Only the `.pub` line should be copied to the server. See [OpenSSH key generation](https://man.openbsd.org/ssh-keygen).

Choose the passphrase to match how you will run commands. A passphrase protects a private key at rest, but the installed launcher and background service cannot prompt for it or access Pageant. For unattended use, use a dedicated key without a passphrase and limit its server account or deploy-key permissions to the required Git repositories. For an encrypted key, use an interactive container with `ssh-agent` and `ssh-add`, and run Git/Local Notion in that same session; starting an agent in this setup shell does not make it available to later launcher containers. See [OpenSSH agent usage](https://man.openbsd.org/ssh-agent).

#### Authorize the public key on the server

Prefer a repository-scoped deploy key or a dedicated Git service account. If the remote account is `root`, restrict this key to Git operations on the intended repository, with no shell, forwarding, or PTY access. A forced command must check both the requested Git operation and the exact repository path.

Use your existing trusted PuTTY login. Use the SSH account from the repository's remote URL: for example, `git@host.example.com:/srv/git/site.git` authenticates as `git`. Append an entry containing the complete `ssh-ed25519 ...` public key and the required restrictions to that account's `~/.ssh/authorized_keys`, keeping existing entries. The directory should have mode `700`, the file mode `600`, and both should belong to that account. If the server manages Git keys through a web interface, add the public key there instead. See [OpenSSH authorized keys](https://man.openbsd.org/sshd#AUTHORIZED_KEYS_FILE_FORMAT).

#### Verify the server and test access

While connected through trusted PuTTY, obtain the server's host-key fingerprint, for example:

```sh
ssh-keygen -lf /etc/ssh/ssh_host_ed25519_key.pub
```

Back in the Docker setup shell, connect using the actual remote username, hostname, and port:

```sh
ssh -o StrictHostKeyChecking=ask -T git@host.example.com
```

Accept the host key only when its algorithm and fingerprint match the trusted server. If SSH presents another algorithm, compare the corresponding server public host-key file. Use `-p PORT` for a nonstandard port. This records the verified host key in the persistent volume. A Git-only server may report that shell access is disabled even when authentication succeeds. Exit the SSH session if it opens a shell, then type `exit` to leave the Docker setup shell. See [OpenSSH host-key checking](https://man.openbsd.org/ssh_config#StrictHostKeyChecking).

For a key usable without a prompt, test the remote from PowerShell in your Windows repository folder. Replace `production` with the remote you use:

```powershell
docker run --rm --pull never --entrypoint git `
  --mount "type=bind,source=$((Get-Location).Path),target=/repo,readonly" `
  --mount type=volume,source=local-notion-state,target=/var/lib/localnotion `
  --env "GIT_SSH_COMMAND=ssh -o BatchMode=yes -o StrictHostKeyChecking=yes" `
  local-notion:latest -c safe.directory=/repo -C /repo ls-remote production
```

This lists remote references without committing or pushing content. Once it succeeds, run your normal command:

```powershell
localnotion pull --all
```

Local Notion will use the saved repository's Git settings, including any configured push at the end. Repositories using PuTTY session aliases need equivalent host, user, port, and identity settings in the container's `~/.ssh/config`, or a remote URL that names the actual server.

#### Troubleshoot SSH failures

- `Host key verification failed` means SSH could not establish trust in the server before authenticating your user. Check the hostname and port against the verified entry in `$HOME/.ssh/known_hosts` in `local-notion-state`. Repeat the fingerprint verification above with that volume attached if the entry is missing. Investigate a changed key through a trusted server connection before replacing it; keep host-key checking enabled.
- `Permission denied (publickey)` means the server did not accept an available user key. Check that the matching public key is authorized for the remote URL's SSH account, and that the private key is saved in the same state volume and usable without a prompt. Use the read-only `ls-remote` test above to check a Git-restricted key, since shell access may deliberately be disabled.

### Synchronize in the foreground

For continuous sync in the foreground:

```powershell
localnotion sync --all --poll-frequency 60
```

Leave the terminal open while synchronization runs; press **Ctrl+C** to stop. Each command uses a temporary container, and repository data remains in the selected host folder after it exits.

The launcher refuses to open a repository already used by a running sync container. Stop that service first. Other native applications or scheduled tasks that write to the same folder must also be stopped.

## Run the background sync service

Use the service helper from the root of this repository. It manages the separate `.docker/data` repository and the `local-notion` service in Docker Desktop.

### Start with a new repository

```powershell
.\docker\localnotion.ps1 -Action Configure
.\docker\localnotion.ps1 -Action Install
.\docker\localnotion.ps1 -Action Logs
```

`Configure` prompts for your Notion integration token with masked input and saves it locally. Make sure the integration has access to the Notion content you want to synchronize. `Install` builds the image from your current source and starts the Compose service. You can also run `Install` first: an empty deployment stays running and waits for a token before initializing its repository.

The local image is `local-notion:latest` (shorthand `local-notion`). The Compose project, service, and container are all named `local-notion` in Docker Desktop. No image is published during installation. By default, the helper builds with the `dev` image version and the current Git revision, then starts Compose with `--pull never --no-build` so it runs the image you just built.

### Import an existing Local Notion repository

Stop the application or scheduled task writing to the source repository before copying it. If this Docker deployment is already running, stop it too. Import requires an empty `.docker/data` directory and refuses to overwrite an existing deployment.

```powershell
.\docker\localnotion.ps1 -Action Stop
.\docker\localnotion.ps1 -Action Import -SourceRepository 'C:\path\to\repository'
.\docker\localnotion.ps1 -Action Install
.\docker\localnotion.ps1 -Action Logs
```

Import creates a separate copy under `.docker/data`. It skips `.git`, moves any copied registry credential into the local secret file, and removes that credential from the copied registry. It also disables Git, NGINX, and Apache integration settings in the copy so host-specific commands do not run inside the container. Your source repository remains unchanged.

Relative storage paths, saved resource render filenames (`resources[*].renders.*.local_path`), and CMS render paths (`cms_items[*].render_path`) are normalized for Linux, including paths for unchanged content. All these paths are validated before any repository files are copied; empty or null optional render paths are preserved. Import also rejects symbolic links and junctions. It rejects absolute paths or storage paths outside the repository: move that content into a self-contained repository before importing it. `Import` only creates the copy. Synchronization starts when you run `Install` or `Start`. If the source had no saved credential, run `Configure` before starting synchronization.

## Manage the background service

Run these from the repository root:

| Action | Command |
| --- | --- |
| Build and install the local image | `.\docker\localnotion.ps1 -Action Install` |
| Save or replace the integration token | `.\docker\localnotion.ps1 -Action Configure` |
| Start synchronization | `.\docker\localnotion.ps1 -Action Start` |
| Stop synchronization | `.\docker\localnotion.ps1 -Action Stop` |
| Restart the service | `.\docker\localnotion.ps1 -Action Restart` |
| Follow container logs | `.\docker\localnotion.ps1 -Action Logs` |
| Check container status | `.\docker\localnotion.ps1 -Action Status` |
| Show helper usage | `.\docker\localnotion.ps1 -Action Help` |

Compose runs `sync --all --poll-frequency 60`. The `LOCALNOTION_SYNC_SECONDS` environment variable can change this interval when Compose creates the container. The service uses `restart: unless-stopped`; Docker Desktop must be running for synchronization to run.

Use `Run` to pass an argument array directly to the CLI:

```powershell
.\docker\localnotion.ps1 -Action Run -CommandArgs @('--version')
.\docker\localnotion.ps1 -Action Run -CommandArgs @('--help')
```

The current CLI returns exit code `254` on Linux when displaying help; the helper treats this expected help result as success. `--version` returns `0`.

Stop continuous synchronization before running commands against the same repository. Even opening a repository can write its extracted themes, and multiple writers are not supported. For example:

```powershell
.\docker\localnotion.ps1 -Action Stop
.\docker\localnotion.ps1 -Action Run -CommandArgs @('status', '--path', '/repo')
.\docker\localnotion.ps1 -Action Run -CommandArgs @('pull', '--all', '--path', '/repo')
.\docker\localnotion.ps1 -Action Start
```

The image is also usable independently:

```powershell
docker build --platform linux/amd64 -t local-notion .
docker run --rm local-notion --version
```

## Background service storage and credentials

| Storage | Container path | Purpose |
| --- | --- | --- |
| `.docker/data` | `/repo` | Repository registry, downloaded content, rendered HTML, themes, and logs |
| `.docker/secrets` | `/run/secrets`, read-only | The local `notion-token` file |
| Compose volume `local-notion-state` | `/var/lib/localnotion` | Application state under the container user's home directory |

The default state volume is named `local-notion-state`. The `.docker` directory is excluded from Git and from the Docker build context. Keep the secret file private and include it only in backups you control.

For `list`, `pull`, and `sync`, credential precedence is explicit `--key`, then the file named by `NOTION_API_KEY_FILE`, then a credential already saved in the repository. Compose sets `NOTION_API_KEY_FILE=/run/secrets/notion-token`. File contents are trimmed and reread for each operation, allowing token changes to take effect on a later synchronization pass. Missing or empty secret files allow fallback to an existing repository credential. `init` does not copy the environment secret into the repository registry.

The container runs as the non-root `app` user, UID `1654`. The image pre-creates its writable home state directory, and Docker initializes the named volume from that directory. For Linux bind mounts, give UID `1654` write access to the repository directory and read access to the token file; host ownership and permissions still apply. Docker Desktop handles Windows bind mounts through its Linux environment.

To inspect generated output on Windows, open `.docker/data` in File Explorer. The repository's profile determines the HTML paths. Serving a website requires a separately configured HTTP server; a website preview service is not included.

## Backups and updates

Stop synchronization before taking a consistent backup. In command mode, back up the repository folders you selected and their private token files. For the background service, back up `.docker/data`, the private `.docker/secrets` directory, and the named application-state volume. Keep an image version or image digest you can return to if an update fails.

To update from release bundles, download and extract the desired [Windows Docker bundle](https://github.com/Sphere10/LocalNotion/releases/latest/download/localnotion-docker-windows.zip), then run its `.\install.bat`. It pulls and selects the image version in that bundle. From an updated source checkout, pull a published version and pass it to the installer; for example:

```powershell
docker pull ghcr.io/sphere10/local-notion:1.5.0
.\docker\install-cli.ps1 -Image ghcr.io/sphere10/local-notion:1.5.0
```

New `localnotion` commands use the selected image. The launcher does not automatically pull newer releases.

To update the installed command and rebuild its image from an updated source checkout:

```powershell
.\docker\install-cli.ps1 -Image local-notion:latest -BuildImage
```

New `localnotion` commands use the rebuilt image. This updates the command without starting the background sync service. Use `-Image` to select a different image name if needed.

To remove the installed command and its user `PATH` entry:

```powershell
.\docker\install-cli.ps1 -Uninstall
```

Uninstall preserves Docker images, repository data, volumes, and the separately managed sync service. If you installed with a custom `-InstallDirectory`, pass the same directory when uninstalling.

For the background service, run `.\docker\localnotion.ps1 -Action Install` to build and recreate the service, then inspect its status and logs. The persistent mounts survive container replacement. `Stop` preserves the data and state. `docker compose down -v` removes the named application-state volume; it does not delete the Windows bind-mounted data or secrets.

The stop signal is `SIGINT`, with a 60-second grace period, so Local Notion can cancel its current operation and close the repository. Wait for the container to stop before working on the same data elsewhere. The CLI reports cancellation as `-1`, which Docker on Linux displays as exit code `255`; a stopped container with `Cancelled successfully` in its logs is an expected graceful stop.

## Publishing an approved image

For maintainers, the [release and deployment guide](../build/README.md) covers publishing official images, CI/CD, versioning, and [public registry access](../build/README.md#public-container-registry-access).
