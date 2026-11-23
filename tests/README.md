# Tests

The LocalNotion .NET test project uses Sphere10.Framework's NUnit conventions: discoverable fixtures, parameterized cases, and constraint assertions through `Assert.That(...)`. Tests appear in the IDE test explorer and run through `dotnet test`. The shared package references live in `tests/Directory.Build.props` and use stable NUnit, adapter, and test SDK releases.

Use the .NET SDK selected by `global.json`: 11.0.100, with stable patch updates allowed. The first build may need NuGet restore; the default tests require no Notion credentials or network access to source content.

| Project | Folders and coverage |
| --- | --- |
| `LocalNotion.Core.Tests` | `Rendering/Projection`, `Rendering/Integration`, `Rendering/Characterization`, and `Repository`: Notion mapping, link resolution, graph safety, source immutability, repository/CMS rendering, legacy output parity, and portable paths. References Core, which supplies the renderer and Notion SDK dependencies, and CLI for compiled application metadata checks. External repository/live cases are explicit. |

Run all default tests from the repository root:

```powershell
dotnet test LocalNotion.sln -c Release
```

Run or discover one project:

```powershell
dotnet test tests/LocalNotion.Core.Tests/LocalNotion.Core.Tests.csproj -c Release
dotnet test tests/LocalNotion.Core.Tests/LocalNotion.Core.Tests.csproj -c Release --list-tests
```

Core fixtures use `Unit`, `Integration`, and `Characterization` categories; external runs have their own `RepositoryIntegration`, `LiveIntegration`, and `RenderingComparison` categories.

The release workflow runs the solution tests on Windows x64 and Linux x64 and uploads TRX results. Default filesystem fixtures use uniquely named temporary directories and clean them up. Test state is isolated before enabling fixture parallelism. Standalone renderer tests belong to the separate Commercial solution. LocalNotion projection and integration tests reference Core, the renderer package, and the Notion SDK.

## Existing repository and live Notion integration

External repository tests are NUnit `[Explicit]` cases and are skipped by the default run. Set `LOCALNOTION_TEST_REPOSITORY` (or the NUnit `RepositoryPath` test parameter), then select the category to opt in. The repository category runs stored online overrides and embedded offline rendering as separate cases.

```powershell
$env:LOCALNOTION_TEST_REPOSITORY = 'D:\Databases\MyNotionRepository'
dotnet test tests/LocalNotion.Core.Tests/LocalNotion.Core.Tests.csproj -c Release --filter 'TestCategory=RepositoryIntegration'
```

The fixtures copy stored objects, graphs, media and overrides into temporary repositories, render all source and CMS documents, and verify the original repository's file inventory and contents remain unchanged. Credentials and deployment configuration are stripped from the copies.

To exercise the Notion sync orchestrator against the configured source before rendering, select the live category:

```powershell
dotnet test tests/LocalNotion.Core.Tests/LocalNotion.Core.Tests.csproj -c Release --filter 'TestCategory=LiveIntegration'
```

The live fixture reads the API token and CMS database ID from the supplied repository's `.localnotion/registry.json`, uses the token only in memory, and never prints it. Sync writes and rendered output stay in the temporary copy. Copies are removed after verification. These cases are integration tests, not unit tests.

## Compare registered existing HTML with fresh rendering

`RenderingComparison` is an explicit NUnit integration category. It compares registered source/CMS HTML with fresh output from the same stored repository and rendering mode. Source resources without a registered HTML baseline are excluded; the test does not invent a baseline for previously unrendered content. A registered path whose baseline file is missing fails the baseline check.

```powershell
$env:LOCALNOTION_TEST_REPOSITORY = 'D:\Databases\MyNotionRepository'
dotnet test tests/LocalNotion.Core.Tests/LocalNotion.Core.Tests.csproj -c Release --filter 'TestCategory=RenderingComparison'
```

The source repository is read only. The fixture verifies its file inventory and hashes after the run, and performs rendering inside temporary copies with registry credentials and deployment configuration removed. It uses stored objects and files without a live Notion refresh. The comparison retains its temporary directory for inspection and prints the location in NUnit progress output with the comparison totals.

The retained directory contains:

- `before/`: the existing HTML and its supporting files.
- `after/`: fresh rendering and its generated asset bundle.
- `comparison.json`: per-document paths, hashes, and separate `ExactMatch` (raw bytes), `DomMatch` (parsed DOM including doctype), and `EquivalentMatch` (normalized comparison) results. NUnit also attaches this report to the test result.
- `dom/before/<HTML path>.txt` and `dom/after/<HTML path>.txt`: canonical documents for examining normalized differences.

The test requires normalized equivalence by default and reports all three comparison levels separately. For an observational run against saved HTML that predates the stored objects, set the NUnit `RequireEquivalent` parameter to `false`; the report still records every mismatch, while rendering, asset, and source-integrity assertions remain enforced. Do not interpret a successful observational run as equality. The comparison runs under invariant culture so date labels are reproducible across machines. Normalization is limited to these presentation-preserving differences:

- Recognized legacy theme CDN URLs and generated asset URLs are replaced by the SHA-256 hash of the corresponding file bytes in each copy. The file must exist inside that copy. Query strings and fragments remain significant; changed asset bytes remain different.
- Generated list-wrapper IDs can replace absent or duplicated inherited IDs. For those wrappers, the legacy unresolved default-color token and an explicit default ordered-list start of `1` are normalized. Source item anchors and unique existing list anchors remain significant.
- Attributes are sorted, browser-collapsible text whitespace is normalized, and newline indentation between structural elements is omitted. Inline separator spaces, nonbreaking spaces, code/preformatted content, scripts, styles, doctype, and layout-bearing elements/classes/attributes remain part of the comparison. Preformatted line endings are normalized from CRLF to LF.

To use the same settings for the comparison and the two stored-repository smoke tests, supply a `.runsettings` file:

```xml
<RunSettings>
  <TestRunParameters>
    <Parameter name="RepositoryPath" value="D:\Databases\MyNotionRepository" />
    <Parameter name="ExistingRendersOnly" value="false" />
    <Parameter name="RequireEquivalent" value="false" />
  </TestRunParameters>
</RunSettings>
```

`ExistingRendersOnly` applies to the stored-repository smoke tests and excludes source resources without an HTML render. Its default is `false`, which renders every source resource. The comparison always uses existing registered renders.

```powershell
dotnet test tests/LocalNotion.Core.Tests/LocalNotion.Core.Tests.csproj -c Release --settings staging.runsettings --filter 'TestCategory=RenderingComparison|TestCategory=RepositoryIntegration'
```

Browser screenshots are a separate diagnostic step using the retained copies. NUnit's HTML/DOM comparison does not execute browser JavaScript or establish pixel identity; report screenshot observations separately from these test results.

## Renderer package integration

LocalNotion consumes `Sphere10.Framework.Web.VisualRenderer` from its NuGet package,
or from Framework source when built with `-p:UseSphere10FrameworkSource=true`. Renderer tests live in Framework's
`tests/Sphere10.Framework.Web.VisualRenderer.Tests` project. Tests in this solution
verify Notion projection, persistence and repository rendering.

## Repository path compatibility

```powershell
dotnet test tests/LocalNotion.Core.Tests/LocalNotion.Core.Tests.csproj -c Release --filter 'FullyQualifiedName~Windows'
```

Run these NUnit integration fixtures on Windows and Linux when changing path handling. Coverage includes opening stored resource and CMS render files, replacing renders, saving and reopening portable paths, preserving unrelated metadata and absolute paths, creating repositories from Windows-style profiles, and keeping registry bytes unchanged during load and no-op save. The fixtures use synthetic temporary repositories and never contact Notion.

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
