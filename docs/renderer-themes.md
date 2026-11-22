# Embedded themes and output assets

`Sphere10.Framework.Web.VisualRenderer` embeds the built-in theme templates, includes, configuration, and assets as one ZIP generated at build time. Editable assets remain in its source `Themes/` folder without appearing individually in Visual Studio's project items. Rendering reads selected entries into memory; it does not create or populate a theme directory. LocalNotion extracts missing built-in files from that archive when opening or creating a repository, preserving existing overrides.

```csharp
var renderer = new HtmlRenderer(new ThemeCatalog(new ThemeOptions {
    ThemesDirectory = optionalOverrideDirectory
}));
var result = renderer.Render(document, new RenderOptions {
    Themes = ["default"],
    Environment = RenderEnvironment.Offline,
    AssetBaseUrl = "../render-assets"
});
```

Omit `ThemeCatalog` or `ThemesDirectory` to use the embedded defaults. A configured directory need not exist. LocalNotion additionally deploys missing built-in files to its configured themes path, normally `.localnotion/themes`, on repository creation and opening. Existing files are preserved. This deployment belongs to Core; the standalone renderer does not perform it.

Current development builds of LocalNotion Core also supply the Refined Navbar family: `navbar_refined`, `navbar_refined_product_cards`, `navbar_refined_compact`, `navbar_refined_wide`, `navbar_standard` and `navbar_bg_aux`. These product-owned presets are installed alongside the renderer's generic themes. Their CSS, JavaScript and MIT license are also installed at the corresponding public `themes/navbar_refined/resources` and `themes/navbar_bg_aux/resources` paths beneath the resolved repository directory. Public asset placement is independent of a custom private Themes path. Missing files are repaired on reopen; existing private definitions and public resource overrides, including empty files, remain untouched. Publish these resources with the generated site. See [navigation layout and colours](cms/headers-and-navigation.md#choose-the-menu-layout-and-colours).

To override one file, create the corresponding named-theme path, for example `.localnotion/themes/default/paragraph.html`. The remaining files, including `.config.json`, fall back to the assembly. Empty files count as overrides. In the standalone renderer, removing an override reveals the embedded version on the next independent render or render batch. LocalNotion also restores that missing built-in file on its next repository open. A session keeps the bytes it read, so editing files during later renders cannot change an earlier result. `HtmlRenderer` calls `ThemeCatalog.CreateSnapshot()` per render; a catalog that is already a snapshot returns itself and reuses sessions for the same ordered themes, environment, mode and asset base URL. Pass `catalog.CreateSnapshot()` to the renderer when rendering a group of documents against one consistent set of overrides. Direct `CreateSession` calls on a catalog with a mutable override directory refresh independently.

A custom theme needs its own `.config.json` and can inherit a built-in theme:

```json
{
  "type": "html",
  "base": "default",
  "tokens": {
    "BrandName": { "offline": "Example", "online": "Example" }
  }
}
```

For each named theme, disk files override the matching embedded files. The first selected theme contributes its complete base chain, applied from the oldest base through the selected theme. Each later selected theme overlays only its own files and tokens, in selection order; its base chain is not applied again. An explicitly selected base later in the list therefore overrides earlier values with its own definitions. This preserves the original renderer's cascade: decorative themes can replace their specific includes without resetting earlier carousel, column, or typography templates. File origin never outranks these theme priorities.

The existing template selection order remains: mode folder plus environment variant, mode folder generic, root generic, root environment variant. In particular, `readonly/paragraph.html` is selected before `paragraph.html`.

Missing required templates, invalid configuration, unknown themes, and inheritance/include cycles report `ThemeException`; invalid override files are not silently ignored. File names and token names use ordinal comparison. Preserve their spelling across operating systems.

Previously extracted theme copies remain ordinary overrides. They can mask newer embedded defaults and corrected built-in template references. The library never deletes or rewrites them automatically.

## Publishing the result

An `HtmlRenderResult` contains `Html` and `Assets`. Each asset has:

- `RelativePath`: a path such as `assets/{sha256}/resources/local-notion/css/ln.css`.
- `ContentType`: the media type to return when serving it.
- `Content`: the in-memory bytes of that render's resource snapshot.

The HTML's asset URLs are `AssetBaseUrl` plus those relative paths; when no base is supplied, they are relative to the rendered document. A web host can serve `Assets` from memory using their paths and content types. A static exporter can write them under its chosen output asset directory. LocalNotion publishes generated assets under the website's public `render-assets/` directory, separate from its private `.localnotion/themes` override directory. Online links include the configured website base URL; offline links are relative to the rendered document.

Earlier LocalNotion builds wrote these bundles to `.localnotion/render-assets`. Regenerate affected HTML and publish the new `render-assets/` output together when upgrading; older output is not moved or deleted automatically. Keep `.localnotion` denied by the web server rather than exposing it to make old asset links work.

The bundle hash covers the effective resource paths and bytes. Different overrides cannot overwrite resources used by an earlier document. Place custom assets and their relative dependencies under the theme's `resources/` tree. Each bundle preserves complete effective resource subtrees, including MathJax fonts and modules and Prism's bundled language grammars. Reuse identical bundle paths when publishing several documents.

`RenderEnvironment.Online` uses each asset's owning theme `online_url`, whether that file comes from disk or the assembly. Existing extracted theme folders and local file overrides do not disable CDN links. Inherited files retain their owning theme's URL: CMS scripts use the CMS CDN directory, while the inherited LocalNotion script uses the default theme directory. Template and include contents still prefer disk overrides. To change a remotely served asset, publish it at the configured URL or configure the theme to use your own hosted URL. Offline rendering, and assets whose owning theme has no online URL, use the emitted resource bundle and `AssetBaseUrl`.

The no-disk library contract is HTML plus in-memory resources. An offline static page requires the emitted resource files beside its output, or a host that serves the same bytes. It is not a promise of self-contained single-file HTML. External media and social embeds retain their own network requirements.

The development renderer's Mermaid runtime deliberately has no CDN URL, so Mermaid documents return its pinned scripts, stylesheet and notices in both online and offline modes. The native Code block's language selects this behavior; no CMS Theme option or page-owned script is needed. See [Mermaid integration](renderer.md#mermaid-code-blocks) for the current development/public-package boundary.

## Hiding embedded page references

Select `@HideEmbeddedPages` in the CMS **Tags** field on the containing document or Section to omit native embedded Page widgets. This is a behavior control, not a Theme. New CMS databases include the Tags option only; existing databases need that option added before editors can select it. Presentation **Themes** remain ordered template, style and resource packages such as `header_large`, `h3_typed` and `badge`.

Child Pages are still synchronized, published and independently addressable. Their heading links, authored paragraph links, inline mentions and explicit Link to Page blocks remain intact. This does not hide a whole Section or replace publication/access controls. Native child Pages inherit their parent's Tags through the existing CMS metadata policy, so a child may receive the behavior through its own projected metadata without suppressing its body.

LocalNotion marks only native Page/ChildPage references as `ReferenceBlock.IsEmbeddedPage` and sets `DocumentBlock.HideEmbeddedPages` from **Tags only**. HTML suppresses those automatic references in the nearest owning document; nested documents have their own flag. Plain-text output is unchanged. Visible embedded references retain the normal `embedded_page.html` presentation hook, which custom Themes may style.

Earlier development builds used `hide_embedded_pages` as a Tag and also mistakenly offered it as a Theme. Rename the existing Tags option and its selections to `@HideEmbeddedPages`, including any interim unprefixed `HideEmbeddedPages` spelling. Neither older spelling is a runtime alias. Move any old `hide_embedded_pages` Theme selections to that canonical Tag explicitly, preserving other selections, then remove the old option from the Themes schema and retire the obsolete suppression Theme files. The old spelling is not a runtime alias, and Tags are not interpreted as Themes. An extracted old Theme override is not removed by installing a new renderer.

For standalone native child Pages that need explicit modifiers, an inherited Internal Code block captioned `embedded_page_themes` can hold comma-separated Theme names, for example `h3_typed,h3_color_aux,color_text_aux_primary`. These are appended in order, with duplicates removed. This setting applies only when the rendered CMS Page has a native Page parent. It does not style datasource Page rows, parent Sections, framing slots or the sectioned overview. Section layout Themes remain excluded; ordinary Page-theme inheritance retains its existing behavior. Theme names must be installed and compatible with the child's base template.

To select modifiers for one native child without affecting every document sharing the same Internal source, use a Code caption `embedded_page_themes:<page-uuid>`. Replace `<page-uuid>` with that child's lowercase, hyphenated UUID (the canonical `Guid` `D` format), and put comma-separated genuine Theme names in the Code body. The adapter normalizes its source page ID before looking up this exact, case-sensitive caption. For example, a page-specific body of `link_button` adds button styling only to that child. The common `embedded_page_themes` list is applied first, then the matching page-specific list; duplicates are removed in first-seen order. Other page UUIDs, datasource rows, framing slots and sectioned overviews do not receive the scoped selection. Both settings are Internal presentation configuration, not Tags; no page ID or slug is hardcoded in the renderer.

## Mermaid template overrides

Mermaid uses `mermaid.html`, separate from the generic `code.html` template. A custom theme inheriting `default` or `cms` can override that file through the normal theme mechanism. Keep these parts of the native contract:

- The `ln-mermaid` figure and `data-ln-mermaid` / `data-ln-mermaid-state="pending"` hooks.
- The `.ln-mermaid-diagram` display area and `.ln-mermaid-error` message element.
- A `.ln-mermaid-source` disclosure containing the encoded source in a `code` element.
- The `{runtime}` token, which supplies the stylesheet, Mermaid library and initializer.

The renderer resolves runtime loading after final document composition. An unused slot or discarded fragment therefore cannot consume the loader needed by a visible diagram. Preserve this token rather than injecting another script per block or Section.

The internal `.mermaid` theme owns the pinned runtime files. Their five emitted resources include both upstream license files; retain all five when exporting or publishing. Override assets deliberately through the configured theme directory rather than editing generated hashed files. See [Mermaid rendering](renderer.md#mermaid-code-blocks) for source controls, failure behavior and the JavaScript requirement.

Run theme regressions with:

```powershell
dotnet test tests/LocalNotion.Core.Tests/LocalNotion.Core.Tests.csproj --filter TestCategory=Integration
```
