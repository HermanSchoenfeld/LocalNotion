# Regression checks

## Repository path compatibility

Run from the source checkout with the .NET 8 SDK or later:

```powershell
dotnet run --project tests/LocalNotion.PathCompatibility
```

The executable tests Windows-style stored paths on the host platform. Run it on both Windows and Linux when changing path handling. It creates synthetic repositories in a temporary directory and removes them when finished; it does not contact Notion or use real credentials.

Coverage includes opening existing resource and CMS render files, replacing an existing render, saving and reopening portable paths, preserving unrelated metadata and absolute paths, creating a repository from a Windows-style profile, and leaving registry bytes unchanged during load and a no-op save. A failed assertion returns a nonzero exit code.

## Git ownership regression

After building the local Docker image, run this from the source checkout in PowerShell:

```powershell
docker build --platform linux/amd64 -t local-notion:latest .
docker run --rm --network none --user root --entrypoint /bin/sh -e LOCALNOTION_GIT_OWNERSHIP_TEST=1 --mount "type=bind,source=$((Resolve-Path tests).Path),target=/tests,readonly" local-notion:latest /tests/docker-git-ownership.sh
```

The equivalent test command for a Linux CI runner is:

```sh
docker run --rm --network none --user root --entrypoint /bin/sh \
  -e LOCALNOTION_GIT_OWNERSHIP_TEST=1 \
  --mount "type=bind,source=$PWD/tests,target=/tests,readonly" \
  local-notion:latest /tests/docker-git-ownership.sh
```

Use a disposable container with no data or state volumes mounted. The script refuses a nonempty `/repo`. It creates synthetic root-owned, group-writable repositories and runs the actual Local Notion CLI as the image's unprivileged `app` account (UID 1654).

Coverage includes `init --git --git-push` and `clean`, successful add/commit/push to disposable local bare remotes, and paths both at `/repo` and outside it with spaces and punctuation. The container has no network, and the test uses no Notion token or real Git identity. It also verifies that standalone Git still rejects the differently owned repositories and that user/global and system Git configuration remain unchanged. This exercises the same Git integration used after pull and sync. A logged Git error fails the test even if the CLI returns exit code zero.

### Native Windows Git

To exercise the installed Windows Git executable and the native .NET CLI without Docker:

```powershell
dotnet build LocalNotion.CLI -c Release
.\tests\windows-git-ownership.ps1
```

Use `-CliPath <native-dll-or-exe>` to select another build, or `-KeepFixture` to retain the synthetic test data for diagnosis. The test supports Windows PowerShell 5.1 and PowerShell 7.

The Windows test uses Git's child-process-only ownership simulation with a temporary repository, a disposable local bare remote, an isolated home and Git configuration, and paths containing spaces, Unicode, and a trailing separator. It exercises the actual CLI `init --git --git-push` and `clean` commands, checks the pushed contents, and verifies that standalone Git still rejects repositories whose ownership is considered different. It does not access a real repository, real credentials, or a network remote, and does not alter the Windows user's Git configuration.
