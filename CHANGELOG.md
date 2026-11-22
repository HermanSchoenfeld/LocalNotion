# Changelog

## 1.7.0 (unreleased)

- Keep old page and file addresses as redirects. The repository registry records an address, with the date, whenever a page, section page, gallery card, category, raw export or file moves, and drops it when the address is used again. Nginx generation serves them from `ln-redirects.conf` as 301 (hard) or 302 (soft) with the query string, and the new `redirects` command lists, adds and removes them.

- Generate absolute sitemap URLs, including the homepage, from the configured public base URL or CMS database domain title. Preserve path prefixes, encode Unicode URLs, and retain private-route exclusions and modification dates.

- Preserve Unicode letters, digits and combining marks in page/database names and CMS routes, fixing Arabic-only titles failing during pull. Normalize equivalent spellings consistently and use a stable Notion ID when a resource title has no usable slug characters. Refreshed existing content uses the new rules too; accented/mixed-script routes can change, and the old addresses are kept as redirects. See [Unicode URL migration](docs/cms/routing.md#upgrading-an-existing-website).
- Verify external `link_mention` and `link_preview` values deserialize and render their text without requiring a Notion object ID; unknown mention types also retain their text instead of aborting rendering.

- Import and render native Notion tab blocks, including nested tabs, keyboard controls and links that reveal a hidden panel. Every panel stays in the static page, so readers without JavaScript, text output and print keep all content.
- Update the vendored Notion client to upstream `notion-sdk-net` main, preserving the HTTP-client lifetime fix and the existing JSON fixes. See [SDK provenance](Notion.Client/UPSTREAM.md).
- Fetch web bookmark previews during pull from Open Graph, Twitter Card and HTML metadata, and store them with the page so that rendering makes no destination requests.
- Add the optional **Keywords** CMS property. Authored phrases override automatic keyword extraction, and clearing them restores it.
- Remove a CMS row's cached page and route when its data source no longer returns it. Rows of other sources and databases are kept, and a missing source or failed query never prunes cached rows.
- Refresh a page's stored metadata when it moves or its properties change under an unchanged edit time, without downloading its body again.
- Omit the Authentication column from the rendered table of a registered CMS database.
- Install the Refined Navbar menu themes (`navbar_refined` with its product-card, compact and wide companions, `navbar_standard` and `navbar_bg_aux`) together with their public CSS and JavaScript, and offer them in the Themes options of a new `create-cms` database.

- Generate static article and category listings in slices of 20 summaries, with query-based Nginx pagination, unchanged article slugs, tracked output cleanup and offline file navigation.
- Supply missing breadcrumb and heading-based table-of-contents navigation on listed article pages while preserving authored navigation and heading anchors.
- Render native Notion Mermaid Code blocks in builds using the Mermaid-enabled renderer, with captions, hidden diagram source and per-diagram error fallback. Ordinary code remains code; no additional CLI command or CMS Theme is required.
- Publish returned browser assets beneath the public `render-assets/` directory, preserving web-server protection of the private `.localnotion` tree. Regenerate and transfer affected HTML and assets together when upgrading older output.
- Add `create-cms` to create an empty Notion website database with the canonical CMS schema and preset Type, Status, Tags and Themes options.
- Add `git` to view and change an existing repository's automatic commit and push settings without publishing content.
- Honor explicit `true` and `false` values for `--render` and `--fault-tolerant`, reject invalid values, and preserve their enabled defaults and bare-switch behavior.
- Refresh enabled NGINX hosting mappings and the sitemap after a successful full (`--all`) standalone render so newly created or renamed CMS pages use their current output files. Skip hosting generation and cleanup when a fault-tolerant batch contains render failures; targeted renders retain the previous hosting artifacts until a clean full render.
- Preserve framing tags on CMS rows and inherit Gallery headers through `@UseParentHeader`, while respecting explicit header selection and `@HideHeader`.
- Remove Local Notion's Enterprise/DRM dependencies, activation and license-enforced quotas. Preserve the GPL license, renderer linking exception and separately licensed dependency notices.
- Update the Local Notion logo and native application icon.
- Apply shared ReadyToRun publishing defaults across native archives, Docker and local deployment, with bundle compression disabled for the local Windows deployment to reduce startup allocation.
- Expand the CMS guide with schema, page composition, themes, media, hosting and clean rebuild procedures; document Git configuration and update native and Docker installation examples.

The release packaging is configured to produce eight self-contained native archives, the Windows Docker launcher bundle, SHA-256 checksums and release metadata. The official Docker image targets `linux/amd64`. Public GitHub and Docker publication remains pending; the locally installed development build is separate. Check the metadata for a published release's build number, source revision and immutable container digest.
