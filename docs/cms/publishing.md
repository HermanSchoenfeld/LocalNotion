# Pull, render and publish

The repeatable workflow is: save in Notion, pull locally, inspect output, transfer it, activate the host and verify the served website. These stages have separate evidence.

## Check settings first

Confirm the repository and CMS database. Review Git enabled, automatic push, remote target and Nginx settings. In an existing repository, pull/render can also commit and publish when configured to do so. A command called pull is not necessarily download-only.

In Local Notion **1.7.0 and later**, `localnotion git --path "./website"` displays the supported Git settings. Add `--enabled false --push false` to disable automatic commits and pushes for later commands. The configuration command itself does not commit or push. Earlier releases, including `v1.6.3` (`1.6.3.7`), do not include it; check `localnotion git --help`. See [Git configuration](../git.md) for all options and standard Git setup.

For previews, use a separate repository or isolated copy with Git/push disabled and no deployment remote. Preserve real-site settings. Avoid concurrent writers and run Local Notion processes sequentially where shared application settings can be accessed by multiple invocations.

## Pull and render

After verifying the Notion save:

```sh
localnotion pull --all --path "./website"
```

In CMS mode, all targets the configured CMS. Pull renders by default. Keep credentials available through the supported key source; creation access does not prove access to every linked resource. Use installed help for supported options before choosing force-download or rendering overrides.

To rebuild from stored state without fetching Notion changes:

```sh
localnotion render --all --path "./website"
```

Render is useful after theme edits but cannot fetch recent editorial changes. It can still trigger configured Git behavior. Continuous `sync` polls until stopped; introduce it after a manual cycle works and its publication effects are understood.

## Inspect output and transfer

Check the complete affected route, its framing, section order, links, metadata, assets and narrow-screen layout. Shared framing can affect many routes; an `empty` section can intentionally show no text. The website profile produces hosted links, so opening a file directly is not equivalent to serving its intended HTTP paths. Use an appropriate preview or staging host.

Deployment is configured separately. A private Git receiver that updates a checked-out branch is one arrangement; copying an approved generated artifact is another. Verify target remote/branch and receiver state. Transfer all required HTML, assets, media and route mappings, not just the visible page file.

Pages containing Mermaid definitions also need their emitted public `render-assets/` files, even when the rest of an online site uses CDN-hosted themes. Publish the HTML and its hashed runtime directory together, including license notices. Keep `.localnotion` inaccessible publicly; exposing private configuration is not an asset-hosting fix. Check a diagram and its runtime requests in the served page. See [Mermaid requirements](../renderer.md#mermaid-code-blocks).

## Nginx activation

For a **new, empty repository folder**, choose hosting generation and the public base URL before its first initialization. For example, after creating the empty folder:

```powershell
$notionKey = (Get-Content -LiteralPath $env:NOTION_API_KEY_FILE -Raw).Trim()
localnotion init --cms "NEW_DATABASE_UUID" --profile website --path "./website" --nginx --override-base-url "https://www.example.com" --key $notionKey
Remove-Variable notionKey
```

Replace the example domain and database ID, and set `NOTION_API_KEY_FILE` to the protected key file described in [Create a blank CMS](create-a-cms.md). The PowerShell recipe explicitly supplies `init --key`; `init` does not resolve that environment variable itself. This generates hosting configuration, not a server installation or activation. Preserve an existing repository's configuration rather than reinitializing it.

`--git` optionally enables automatic change tracking/commits; configure Git identity first. `--git-push` additionally enables automatic pushes and should be used only when the intended deployment remote and branch are ready. These options are deliberately omitted from the example: enabling them changes later commands' publication effects.

When enabled, Nginx generation writes configuration under `.localnotion/nginx/conf`, including `ln-urls.conf`. The active host must include the correct mappings and serve the expected output paths.

Article collections generate separate static listing documents of up to 20 summaries each. Their generated routes use query selectors such as `/articles?page=2`, preserving all article slugs. Publish every tracked slice together with `ln-urls.conf`; the query selector must reach the Nginx origin, and any HTML cache must distinguish it. Existing listing authorization also applies to extra backing files. See [Article collections and static pagination](articles.md) for query behavior, offline exports and rebuild checks.

Files on disk and active configuration are different states. Some installations monitor changes and reload; others restart a per-site container. Check the actual service, script, watched path and target. Validate configuration before activation and verify that only the intended site changed.

Individually mounted Docker configuration files can retain old file bindings after a host-side atomic replacement. If host/container bytes differ, repair the mount/activation arrangement; a targeted container restart can be one remedy. A successful reload alone does not prove the container sees the new file.

### Redirects file

Generation also writes `ln-redirects.conf`, the redirects from [old addresses](routing.md#redirects-from-old-addresses), and `ln-urls.conf` ends with `include ln-redirects*.conf;`. Nginx resolves that relative to its configuration directory, so the redirects file must sit beside `ln-urls.conf` there. A host that mounts configuration files one by one needs a mount for it too, for example `/root/websites/<domain>/.localnotion/nginx/conf/ln-redirects.conf` at `/etc/nginx/ln-redirects.conf`, read-only. Adding a mount to an existing Docker container means recreating it with its other settings unchanged. The include names a file pattern, so a host without the file keeps running and serves no redirects until it is mounted. Publish the redirects file together with `ln-urls.conf`.

DNS, TLS and reverse proxies are separate host responsibilities. Ensure the public hostname reaches the intended site service. The creation command provisions none of these systems.

### Keep private repository files inaccessible

The current development generator includes these restrictions in a newly created `nginx.conf`. They deny both each private directory's own URL and every path beneath it. The generated route mappings and public theme assets remain separate.

```nginx
location = /.localnotion { return 404; }
location ^~ /.localnotion/ { return 404; }
location = /.git { return 404; }
location ^~ /.git/ { return 404; }
```

Existing `nginx.conf` and `mime.types` files are intentionally preserved during normal generation. An application upgrade or another pull does not add these rules to an existing host. Review the active site's configuration and add any missing restrictions inside its `server` block, before `include ln-urls.conf;`. Preserve its route mappings, authentication policies, MIME configuration and other custom settings. Do not delete the hosting folder or replace the whole configuration to obtain these four rules.

Validate the configuration with `nginx -t`, activate it on the intended host, then check that requests to `/.localnotion`, `/.localnotion/registry.json`, `/.git` and `/.git/config` return a denial, while ordinary pages and public assets still load. Verify both the origin service and the public hostname: a proxy response can conceal an exposed origin. Do not print, save or include the response body when testing a potentially exposed private file. For file-mounted containers, also verify that the container sees the updated configuration before activation.

## Authentication and secrets

Publication, privacy and authentication are separate:

| Mechanism | Controls | Limit |
| --- | --- | --- |
| Status/Publish On | Inclusion of ordinary content during regeneration | Framing bypasses it; deployment must complete |
| `@Private` | Sitemap exclusion, inherited by descendants | Does not deny direct access or remove files |
| Authentication | Credentials in applicable generated host mappings | Only effective when the active host uses those mappings |

Authentication is rich text. The current Nginx integration uses a `username:password` value as an HTTP Basic credential. It is not an encrypted account store or multi-user identity service. Each independently authored CMS Page uses its own Authentication value: a protected `/private` Page does not automatically protect a separate `/private/child` CMS row. Native Notion child pages inside a CMS row inherit that row's authentication when parsed. Sectioned pages, galleries and books combine authentication from their included parts; different values do not establish independent valid logins. Set the intended credentials on every independent protected CMS page and relevant section/part, then test each route. Ordinary non-CMS resources use Notion ancestry for inheritance.

Use HTTPS. Verify unauthenticated denial and authorized access after deployment, including child pages, attachments and alternate file URLs. An authorized HTTP 200 is not evidence that unauthorized access is denied.

Generated Nginx mappings protect both an authenticated route and its repository-relative backing-file URL with the same credential and MIME policy. This also covers raw pages and attachments with inherited authentication. Physical file locations retain their case and literal filename characters; spaces are quoted in the configuration rather than URL-encoded. Existing route entries retain their order and content. Incompatible policies for a shared target, a conflicting physical URL, an escaping path or unsupported protected-path syntax stop generation before replacing the existing mappings. Resolve the source conflict and regenerate; do not hand-edit a generated map as the durable fix. After upgrading the generator, regenerate, validate and activate the host configuration, then test anonymous requests to both forms of URL.

Registries, cached objects, Authentication values, generated mappings and Git history may contain secrets. Basic Authorization encoding is reversible. Keep operational repositories private, protect key files, and avoid full config/object dumps in screenshots/logs/issues. Configure the host to deny access to `.localnotion`, Git metadata and secret files. Check that alternate static-file paths do not bypass route authorization.

## Verify each boundary

| Stage | Evidence |
| --- | --- |
| Notion save | Reload the owning page; exact change persists |
| Pull | Current content exists in the selected local repository |
| Render | Generated route contains the intended composition/content |
| Transfer | Receiver files/revision match approved output |
| Activation | Active server/container uses deployed config and files |
| Public serving | HTTPS content, assets and restrictions behave correctly |

For a first staging cycle use a harmless recognizable test sentence, then remove it through Notion and complete the cycle again. A matching Git revision or HTTP 200 alone does not prove visible success.

## Rollback

Know the previous deployable state. Host-only rollback can be overwritten by the next sync if Notion still contains the new content. Decide which authoring/generation/deployment states must change, verify the served result and resume automation deliberately. See [troubleshooting](troubleshooting.md).

## Clean rebuild and optional outage test

See [Rebuild a staging website from scratch](rebuilding.md) for a complete recovery-copy, empty-publication and fresh-pull workflow. It explains how to retain private configuration, custom themes and Git history while clearing local resource indexes, keep automatic Git disabled during generation, inspect logs and output, then publish and verify the replacement. Publishing an empty payload deliberately causes downtime and is an optional staging test, not a routine update requirement.

## Embedded detail pages

Keep related detail content in a native child Page when it belongs to a Section but needs its own destination. Leave **Custom Slug** unset: LocalNotion calculates the child address from the parent and title. Link an overview menu to the saved Heading 1 block inside that Page; preserve its block ID when editing the heading.

To omit the child Page's inline widget from the overview, select `@HideEmbeddedPages` on the containing Section in **Tags**. It is a behavior control, not a Theme; LocalNotion projects it to `DocumentBlock.HideEmbeddedPages` without changing the Theme selection. The child still publishes normally. This setting does not provide privacy or authentication. See [the renderer contract](../renderer-themes.md#hiding-embedded-page-references) for scope, migration from the mistaken development Theme, and the separate `embedded_page_themes` Internal setting for real presentation packages.

After moving an existing datasource Page into a native Page, verify its saved parent and title-only properties, then pull and check the calculated route. A cached `custom_slug` value can be calculated output; it is not evidence of an authored Custom Slug. Verify existing content, heading IDs and menu references, then inspect the published desktop and mobile destinations and the overview independently.
