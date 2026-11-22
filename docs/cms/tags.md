# CMS tag reference

The canonical Tags multi-select presets these thirteen exact strings in this order. Colors label choices in Notion, not website styling.

Put flags on ordinary content. Header, NavBar, Footer and Internal have Tags/Summary cleared during CMS parsing, so tagging a framing row itself does not control its route as expected.

| Tag | Preset color | Meaning and limits |
| --- | --- | --- |
| `@ContactForm` | green | Custom reference choice; no built-in source behavior established. Does not itself supply a form or backend. |
| `@ShowChildPageTitleOnBanner` | default | Requests child-page title on its cover/banner in the render model; theme/cover determine presentation. |
| `@ShowTitleOnBanner` | yellow | Requests this page's title on its cover/banner. |
| `@HideInternal` | green | Suppresses the Internal customization selected for this route. |
| `@UseParentHeader` | blue | Uses the parent header; `@HideHeader` wins. |
| `@HideHeader` | red | Suppresses the route's header. |
| `@HideNavBar` | brown | Suppresses the route's navbar. |
| `@HideFooter` | pink | Suppresses the route's footer. |
| `@UseCoverAsFeature` | orange | Prefers the cover image, with thumbnail fallback. |
| `@UseThumbnailAsFeature` | gray | Uses the image thumbnail/icon as the feature candidate; an emoji is not an image URL. |
| `@UseFirstImageAsFeature` | purple | Prefers the first image block, then cover and thumbnail. |
| `@Private` | gray | Excludes the route and descendants from the generated sitemap; does not deny direct access. |
| `@HideEmbeddedPages` | default | Omits automatic embedded Page widgets from the containing document. Independent destinations and authored links remain available. |

Every Tag starts with one `@` followed by PascalCase, including `@HideEmbeddedPages`. Select the exact spelling. Existing databases need their old `hide_embedded_pages` or interim `HideEmbeddedPages` Tag renamed to `@HideEmbeddedPages`; neither old spelling has a runtime alias. See [the renderer contract](../renderer-themes.md#hiding-embedded-page-references) for migration from the obsolete development Theme.

## Feature-image precedence

Avoid selecting several feature-source flags together. Current preference is first-image flag, then cover flag, then thumbnail flag. Without a flag, normal candidates are first image, cover, image thumbnail. Templates can display these values differently.

## Inheritance and privacy

Nested Notion child pages inherit their parent CMS row's Tags. Route-based framing inheritance is separate: a route chooses a local fragment, inherits one or suppresses it.

The Private check walks route ancestors. It remains a sitemap rule, not access control. For protection use Authentication and verify the active host, including alternate file paths; see [publishing](publishing.md).

## Custom tags

Additional tags are metadata unless code, themes or customization consume them. Some website CMS schemas include `@ContactFormNoAnimate`; it is outside the canonical thirteen and no built-in semantics are promised here. Document its consumer and test output before depending on a custom name.
