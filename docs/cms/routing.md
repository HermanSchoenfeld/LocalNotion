# Addresses and hierarchy

A slug is the path after the domain. `guides/cms/writing` becomes `/guides/cms/writing`. The website hierarchy comes from these paths, not a Notion relation column or database-view row order.

## Default Page routes

Local Notion combines consecutive nonblank Root, Category1–Category5, then the item title. Category traversal stops at the first blank. Values are normalized for URLs.

| Item | Root | Category1 | Category2 | Path |
| --- | --- | --- | --- | --- |
| About Us | blank | blank | blank | `/about-us` |
| Writing | Guides | CMS | blank | `/guides/cms/writing` |
| Writing | Guides | blank | CMS | `/guides/writing` |
| Writing | blank | CMS | blank | `/writing` |

Fill components consecutively. Category2 cannot skip Category1; Category1 cannot skip Root.

Categories create intermediate hierarchy nodes. Documentation can become an inferred book/article listing without a dedicated row for each category. Adding a Page at a category route can change that listing structure; inspect the full result.

## Custom routes and home

Custom Slug overrides the default. Enter a path such as `guides/cms`, not a complete hostname. Local Notion normalizes it and removes its leading slash internally.

Enter the **page path only**, without a `#` fragment. The inspected slug sanitizer removes a literal hash rather than preserving an anchor: an input such as `studio#contact` becomes `studiocontact`, changing the page path. A link to a Section is a separate visitor-facing address; do not paste that full anchored link into Custom Slug.

A simple Page homepage should use Custom Slug `/`, which becomes the empty internal home slug. A Page merely titled Home with no override normally becomes `/home`.

Moving a route can change descendant structure, inherited framing and links. Regenerate and inspect affected navigation and bookmarks.

## Sections

A Section uses categories as its page path and adds a title-based anchor. A Contact Section with Root Company contributes to `/company#contact`; blank categories contribute to `/#contact`.

Sections at the same base path compose one route, ordered by Sequence. For a custom location, put only that base path in Custom Slug, such as `studio`; Local Notion adds a title-derived fragment during parsing. Fragments identify content within one HTML page, not separate Nginx routes.

For the older installed renderer, verify the actual rendered anchor before sharing a precise section link. Its outer Section element uses the compact Notion page UUID, and its inner title anchor is derived separately from the resource name. An invented Custom Slug fragment does not create either ID.

## Sitemap URLs

Local Notion writes `cms/sitemap.xml` when it generates Nginx hosting files. Every entry uses a fully qualified HTTP or HTTPS URL, including the homepage. Google cannot accept relative paths such as `/cases/telstra` in a sitemap.

An absolute `paths.base_url` in `.localnotion/registry.json` supplies the public origin and optional path prefix. If the base URL is relative (normally `/`), the CMS database title supplies the domain, for example `sphere10.com`; HTTPS is assumed unless the title includes `http://` or `https://`. The sanitized database filesystem name is not a domain. A relative prefix such as `/Docs/` is retained, including its case.

Unicode paths are UTF-8 percent-encoded in sitemap URLs. Private routes and descendants remain excluded, and each entry retains the latest modification date of its source pages. This fixes sitemap addresses without changing article slugs or website links.

Run `localnotion render --all --path <repository>` to regenerate the sitemap from stored content when Nginx hosting is enabled. This also renders pages and regenerates hosting mappings, and can commit or push according to the repository's Git settings. For a sitemap-only correction, render an isolated copy with Git disabled, validate the XML, and publish only `cms/sitemap.xml`. Compare its route set with the previous sitemap before deployment, then verify the public `/sitemap.xml` response and resubmit it in Search Console.

## Galleries

Each Gallery row supplies a card and the page it opens. Root groups the cards into a gallery, and the Item title supplies the card page's path. Category1–Category5 become badges/filter values, not nested path segments.

| Item | Type | Root | Category1 | Role |
| --- | --- | --- | --- | --- |
| Orchard App | Gallery | Cards | Mobile | `/cards/orchard-app`, Mobile badge |
| River Dashboard | Gallery | Cards | Web | `/cards/river-dashboard`, Web badge |

Their parent `/cards` can become the gallery of cards. Keep Root consistent for cards in one gallery. The location name is an example; the cards could just as readily introduce products, photographs, articles or team members.

## Framing and child pages

Header, NavBar, Footer and Internal use their category/custom path without appending the row title. A root framing row can be named Shared Footer without creating `/shared-footer`. Root Guides places a more specific Footer at that branch.

Inheritance follows route ancestors. Moving content or framing can therefore change selected navigation/customization.

A Notion child page appends its title to the owning page route. For a Section parent, its fragment is discarded first: a Details child of `/company#services` becomes `/company/details`. A non-Section parent's fragment is instead treated as a path component. Verify complex anchor/child combinations before relying on them.

Native Breadcrumb rendering in installed 1.5.0.12 has a depth limit: more than six ancestor-category positions can fail rendering. Use a short explicit linked trail for those deep entries, with links back to the collection and chapter home. This restores the tested deep Gallery detail pages; the ordinary manual chapters can use the native widget.

## Advanced tokens and collisions

Slug processing supports values including `{page_name}`, `{id}`, `{root}`, `{category1}` through `{category5}`, `{status}`, `{publish_on}`, `{themes}` and `{custom_slug}`. These are advanced substitutions, not extra columns. Start with literal paths; verify formatting before depending on dates/tokens in public URLs.

Keep unrelated Page routes unique. Avoid accidentally colliding a Page with a category listing or Section collection. Generated CMS items and hosting mappings show the final model more accurately than the Notion table view alone.
