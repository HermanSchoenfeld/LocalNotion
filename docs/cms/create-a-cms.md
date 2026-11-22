# Create a blank CMS

`create-cms` creates one new Notion database with the complete CMS schema and preset choices. The finished database has **zero active content rows** and does not clone the reference website. One temporary setup row is created and then moved to Notion Trash.

`create-cms` is available from Local Notion **1.7.0** in native builds and Docker. Earlier releases, including `v1.6.3` (`1.6.3.7`) and `1.5.0.12`, do not include it. Run `localnotion create-cms --help` to check the selected executable; update to version 1.7.0 or later if the command is missing.

## Prepare Notion access

Choose an ordinary Notion page to contain the new database. Give your integration access to that page and **read, insert-content and update-content** capabilities. Creation needs these to register the complete palette, verify the schema and move its temporary setup row to Trash. Pulling later also needs read access.

Copy the parent **page UUID**, not a CMS database or data-source ID. Keep the integration key private.

## Command and credentials

```text
localnotion create-cms --parent PARENT_PAGE_UUID [--title "My Website"] [--key INTEGRATION_KEY] [--path EXISTING_REPOSITORY] [-v|--verbose]
```

| Option | Meaning |
| --- | --- |
| `--parent` | Required non-empty parent-page UUID accessible to the integration |
| `--title` | Defaults to `My Website`; 1–2000 characters |
| `--key`, `-k` | Explicit integration key; avoid shared terminals and shell history |
| `--path`, `-p` | Optional existing repository from which to read a stored key |
| `--verbose`, `-v` | More diagnostic output |

Credential lookup uses `--key`, then a non-empty readable file named by `NOTION_API_KEY_FILE`, then the stored key from `--path` if supplied. The optional path is a credential source only: it is not an output directory and its repository is not changed.

For example, put the key in a protected file outside the website repository. Replace the placeholder UUID before running:

```powershell
$env:NOTION_API_KEY_FILE = "../private/notion-key.txt"
localnotion create-cms --parent "PARENT_PAGE_UUID" --title "Website CMS"
```

On Linux/macOS, set the variable with `export NOTION_API_KEY_FILE="../private/notion-key.txt"` before the same command. Restrict file permissions and exclude the key file from source control.

## Result and side effects

Success output gives a new database ID, Notion link and initialization command. The current schema contains twenty-one [columns](schema.md), including optional authored Keywords, eight Type options, four Status options, thirteen [Tags](tags.md) and 235 [Themes](theme-reference.md), with their preset editor colors. The Theme palette includes refined/standard menu layouts, linked product cards, compact/wide modifiers and an auxiliary-colour menu surface. Presentation choices stay in Themes; behavior controls such as `@HideNavBar` stay in Tags. These six menu presets are supplied by the current development build; check the installed version's preset catalogue when using an older 1.7.0 build.

It contains no copied rows, page blocks, media, credentials, routes or deployment settings. Notion limits the choices accepted in a schema request, so registering the full Theme palette briefly uses one Hidden setup row. The command moves that row to Trash and verifies there are no active rows. The setup row remains in Notion Trash under its normal trash lifecycle; it is not permanently erased by this command. Leave it in Trash rather than restoring it as website content. Creation does not initialize a local repository, synchronize, render, commit, push or provision a server.

Each invocation creates another database; it is not an idempotent name lookup. After a timeout or cancellation, inspect the parent page before retrying. A request may have completed without a success message. On a partial failure, the command reports the created database and any known setup-row identifier for recovery. Inspect and resolve that partial result; do not rerun creation to repair it, because doing so creates a second database.

## Initialize the local repository

Create an empty local folder and use the returned **database ID**:

```powershell
mkdir website
$notionKey = (Get-Content -LiteralPath $env:NOTION_API_KEY_FILE -Raw).Trim()
localnotion init --cms "NEW_DATABASE_UUID" --profile website --path "./website" --key $notionKey
Remove-Variable notionKey
```

The folder must exist first. This example leaves Git, automatic push and Nginx generation disabled unless you explicitly enable their options. The example reads the private key file into memory and explicitly supplies `init --key`; `init` does not resolve `NOTION_API_KEY_FILE` by itself. Protect the generated registry and do not publish its integration key.

Open the database and [add your first pages](first-website.md). Initialization creates local storage, not homepage rows. Creating/opening the local repository also supplies the matching menu templates and their public CSS, JavaScript and license. These assets follow the resolved repository root even when the private Themes path is customized, and existing template/resource overrides are preserved. Choose the desired [menu layout and colours](headers-and-navigation.md#choose-the-menu-layout-and-colours) on the shared NavBar row, then publish its generated output and assets together.

If creation fails, check the UUID, integration access, read/insert/update capabilities and credential source. Use reported recovery identifiers to inspect a partial result before deciding whether to complete or discard it. See [troubleshooting](troubleshooting.md).
