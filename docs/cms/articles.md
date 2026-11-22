# Article collections and static pagination

Local Notion publishes an article collection as a hierarchy of category listings and independently addressable articles. Each article remains a Notion page with its own content. The listing displays summaries and links to those pages; it does not combine their full bodies into one document.

## Organize the collection in Notion

Use ordinary **Page** rows with a common **Root**, then consecutive **Category1** through **Category5** values to organize the articles. Leave **Custom Slug** blank for normal calculated addresses. Write a useful **Summary**, provide a representative image when appropriate, and use **Sequence** to control ordering.

An optional **Keywords** text property supplies authored page metadata. Enter comma-separated phrases such as `Sphere10 Framework, C#, ObjectSpaces`. Local Notion trims phrase edges, ignores empty entries and preserves spelling and order. Nonempty authored keywords take precedence over automatic extraction. Existing databases without this property, or pages with a blank value, retain automatic keywords and their existing cache behavior. Clearing a previously authored value restores automatic keywords from the available body, including during a metadata-only pull.

New CMS databases include **Keywords** as a `rich_text` property. For an existing CMS, add an optional Text property named exactly **Keywords**. It is page metadata, separate from behavior **Tags** and presentation **Themes**. Standalone article and gallery metadata use the effective keywords through the normal renderer. Article-listing overview metadata keeps its existing collection policy. The existing automatic extractor and collection aggregation limits are unchanged.

For example, a Page named *Signing messages*, Root *Articles*, and Category1 *Cryptography* produces an article under `/articles/cryptography/signing-messages`. The CMS also supplies the collection and category listings. A filtered Notion database view can help edit a branch, but the view itself does not determine publication or website ordering.

Ordinary publication rules still apply. Draft, Hidden, QA and future-scheduled ordinary content are not included as published articles. Keep authentication configured on the appropriate content owner and verify the generated hosting rules before exposing a collection.

## Breadcrumbs and table of contents

Write articles with native headings in a logical H1/H2/H3 hierarchy. Include a native **Breadcrumb** before the native **Table of Contents** near the beginning when authoring in Notion. The article renderer supplies missing navigation for existing articles and preserves authored equivalents without adding a second copy. When an article already has a breadcrumb but needs an automatic TOC, the generated TOC follows the breadcrumb's containing block. Authored content, native widget positions and heading anchors remain unchanged.

Breadcrumb links follow the existing collection/category hierarchy. TOC links target the article's actual content headings and retain their anchors. An article without content headings has no heading entries to display; add meaningful sections in Notion when the content warrants them.

## Fit a branded cover in listing features

An article that explicitly selects the `header_cover_contain` Theme also passes that modifier to its native `articles_summary_feature` widget. The adapter scopes the feature to `cms_articles` followed by `header_cover_contain`; it does not copy the article's other page Themes into the collection. Unselected articles keep their existing feature rendering.

Install a compatible native override at `.localnotion/themes/header_cover_contain/articles_summary_feature.html` before selecting the modifier. The override can contain the complete branded cover at the top of the existing feature frame while preserving its native thumbnail, dimensions and surrounding collection layout. This requires both the matching adapter build and the installed override; the selection alone does not install an external Theme package. See [Renderer themes](../renderer-themes.md) for native override ownership and installation.

## Navigate a large collection

The default article presentation contains up to **20 summaries per listing page**. Local Notion generates a separate static HTML document for each slice. It does not send every article summary to the browser and hide the unused entries.

| Public address | Content |
| --- | --- |
| `/articles` | First 20 summaries in the collection. |
| `/articles?page=2` | The next slice of summaries. |
| `/articles/cryptography?page=2` | The second slice within that category, when one exists. |
| `/articles/cryptography/signing-messages` | The complete article at its normal address. |

Page numbers are query parameters on a listing. They never become article slugs, category names or additional Notion rows. Adding or removing an article can change its position in the collection, while its existing article address remains unchanged by pagination.

The collection name and URL depth are unrestricted by pagination. `Articles` and `/articles` above are examples, not required names or reserved routes. A collection can instead be at `/fixed/fixed2/knowledge`, with a category listing at `/fixed/fixed2/knowledge/security?page=2` and an independent article at `/fixed/fixed2/knowledge/security/signing-messages`. Pagination uses each listing's complete resolved CMS address; it does not locate a segment named `articles` or rebuild article links from a page number. Continue using the normal calculated CMS addresses rather than adding Custom Slug overrides for pagination.

Ordering is applied before slicing. Explicit Sequence values sort ascending, followed by creation time descending and an ordinal slug tie-break. Unnumbered articles follow explicitly numbered articles. Leave Sequence blank on all articles for a newest-created-first collection. Editing prose, images or metadata does not change the position of articles whose creation times match. The displayed date is the article's creation date, not its scheduled Publish On value. Pagination does not introduce a new publication-date field or a collection-wide browser sorting engine.

The total, displayed range and navigation links are calculated during generation. Previous/Next and a bounded set of numbered links request another document. Each document contains only its own summaries and images, along with the shared framing and category navigation. The existing CMS loading indicator is retained.

The built-in `cms_articles` presentation places the category hierarchy on the left of consistent article summary rows. The hierarchy stays available while scrolling on desktop and becomes a **Browse topics** disclosure on phones. This changes the article-listing section's framing, without replacing the site's banner, menu or other sections. It uses the existing category names, creation dates, summaries, icons and feature images. No special collection name, CMS Theme selection or Custom Slug is required.

Each article's title and date appear above its image and summary. Images alternate between left and right through the existing summary variants. Click anywhere within a summary to open its article; a subtle background and title-color change indicate hover, with a visible outline for keyboard focus. Hierarchy links use readable body-color text at normal weight, reserving bold and the selected background for the current destination. Shared article CSS and JavaScript are external theme resources, served by the standard CDN online or included in the generated asset bundle offline. Changing an extracted online resource requires publishing the corresponding CDN bytes as well as rendering the website.

## Host the query URLs with Nginx

The generated Nginx mappings select a fixed, pre-rendered file for each supported `page` value. This remains a static website: no Notion query, application server or database lookup runs when a visitor changes pages.

The generator extends each collection's existing exact route. Nginx matches that route using the path and reads the page selector separately from the query string. A configuration entry named `location = /articles?page=2` would not provide this behavior.

The same mapping applies to every generated collection and category route at any depth. Its exact path comes from that CMS item's resolved slug, including all parent segments. The label shown to visitors and the site's domain do not select the backing file.

Missing or empty `page` and `page=1` select the first document. Unknown, malformed or out-of-range values return 404. Repeated `page` parameters return 400. Other query arguments, such as tracking parameters, do not select another article or affect the collection's ordering.

Generated targets are selected from the known listing outputs. Query text is never used directly as a filesystem path. Authenticated listings retain their existing authorization checks on every slice and on their backing-file URLs.

Deploy the HTML, generated mappings and required assets together. Validate and activate the updated configuration on the actual host. If a proxy or CDN caches HTML, its cache rules must distinguish the `page` query argument. See [Pull, render and publish](publishing.md) for configuration, transfer and verification.

Query routing requires the generated Nginx rules or an equivalent host configuration. A generic static server that ignores query arguments will serve the first file for every page. Offline exports use links to their actual generated files instead of relying on Nginx query routing.

## Rebuild after changes

Use the normal stored-content render command after upgrading the executable or changing presentation:

```sh
localnotion render --all --path "./website" --fault-tolerant false
```

Use `pull --all` instead when the saved Notion content needs downloading. Check the repository's automatic Git settings before either operation; generation can also commit and push when those settings are enabled.

Local Notion tracks all generated listing files on their owning CMS item. A completed rebuild updates the page count and removes obsolete slices when the collection shrinks. Do not create manual CMS rows or Custom Slug overrides for pagination, or hand-edit the generated HTML and mappings.

After publishing, open the first and second listing pages, follow an article link from each, and verify that the article addresses have not acquired a page parameter or page-number path. Also check a category listing, an invalid page request and any applicable access restrictions.

This feature requires a build containing static article pagination in both Local Notion and Sphere10.VisualRenderer. Source implementation and a staging deployment do not imply availability in an older installed executable, GitHub release or Docker image.
