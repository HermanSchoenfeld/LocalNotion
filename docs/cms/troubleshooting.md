# CMS troubleshooting

Find the first boundary missing expected evidence: Notion save, pull, render, transfer, activation or public serving. Fix the owner of that problem.

## Creation and schema

| Symptom | Check |
| --- | --- |
| Unknown create-cms command | Update to Local Notion 1.7.0 or later, then check `localnotion create-cms --help`. For Docker, update and select the image used by the launcher or service. |
| Parent rejected | Supply a non-empty page UUID, not a database/data-source identifier. |
| Missing key | Check explicit key, key-file variable, then optional repository credential source. |
| Access rejection | Check exact parent-page access and read/insert/update capabilities needed for creation and setup cleanup. |
| Creation timeout/cancellation | Inspect the parent and reported database/setup-row recovery identifiers. Resolve or discard a partial result; do not rerun creation to repair it. Success leaves zero active rows and one setup row in Trash, which should not be restored as website content. |
| Database not recognized | Compare exact [column names/types](schema.md); title similarity is irrelevant. |
| File parsing failure | Choose a supported type; File is a reserved unsupported preset. |

## Content and layout

| Symptom | Check |
| --- | --- |
| Typed edit disappears | Verify saved state after a Notion reload before pulling. |
| Local content is old | Confirm selected CMS/repository and access, then pull; render does not fetch. |
| Ordinary page missing | Check Published status and due Publish On in UTC. |
| Section text invisible | Check `empty` theme and the complete composed route. |
| Draft Footer/Internal still active | Framing bypasses publication gating. Change placement or ordinary-row framing flags deliberately. |
| Hide flag on Footer ineffective | Framing Tags/Summary are cleared; flag the ordinary content. |
| Wrong inherited header | Check route placement and `@UseParentHeader`/`@HideHeader`. |
| Page fails “exactly one part” with local framing | In 1.5.0.12, Published framing at the same ordinary Page route can be counted as another body part. Set pure Header/NavBar/Footer rows Hidden and the Page Published, then regenerate. The Header/NavBar examples use this tested workaround. |
| Breadcrumb keeps an old parent title | Regenerate descendants after renaming the parent. A fresh full render corrected the inspected unchanged article in the isolated 1.5.0.12 preview. |
| Copy belongs to another record | Trace the owning Section/framing row, not just the visible URL. |
| Order unstable | Use distinct integer Sequence values. |
| Theme differs after upgrade | Existing extracted overrides can mask embedded defaults. |
| Theme preset fails | A choice name does not install a theme; inspect catalog/configuration errors. |

## Routes and customization

| Symptom | Check |
| --- | --- |
| Later category ignored | Fill Root and earlier categories; traversal stops at first blank. |
| Home is `/home` | Use Custom Slug `/` for a Page or compose root Sections. |
| Gallery path unexpected | Root groups entries; categories are badges. |
| Route collision | Separate unrelated Pages and Section collections. |
| Section link changes the page path | Put only the base path in Custom Slug. The sanitizer removes a literal `#`; inspect actual rendered IDs for visitor anchor links. |
| Deep entry fails while rendering Breadcrumb | Installed 1.5.0.12 fails beyond six ancestor-category positions. Use a short explicit linked trail back to the collection and chapter home, then regenerate. |
| Gallery card repeats its parent path | Inspect the old template's relative href before changing CMS routes. The staging 1.5.0.12 template uses `href="/{url}"` to restore root-relative links; review this override when upgrading to the newer URL resolver. |
| Documentation heading fragment does not scroll | Generated heading IDs need not match Markdown heading slugs. Link to the chapter base until its actual rendered anchor is verified. |
| Injected HTML displays as code | Require HTML language and exact `{INJECT}` caption. |
| Internal style missing | Verify caption/token, consuming template, selected source and hide flag. |
| Branch Internal loses global values | Nearest source is selected; ancestor tokens are not automatically merged. |
| ContactForm creates no form | Built-in behavior unconfirmed; document/configure its form/backend consumer. |

## Publishing and protection

| Symptom | Check |
| --- | --- |
| Pull unexpectedly publishes | Review Git enabled/push settings and remote. Preview in isolation. |
| Correct local HTML, stale public site | Verify transfer, active config, mounts, proxy target and cache. |
| Receiver commit matches, site stale | Matching Git state does not prove server activation or mounted bytes. |
| Reload monitor fails | Check service, script, permissions, watch path and site target; test one harmless event. |
| Reload reads old mappings | Individually mounted Docker files can retain old bindings after replacement. Compare bytes and activate appropriately. |
| Missing styles/images | Deploy emitted assets/media; check base paths and online asset origins. |
| Mermaid remains an ordinary code block | Check the native Code language and that the executable is Local Notion 1.7.0 or later. Earlier releases, built on renderer 3.1.3, do not render Mermaid. |
| Mermaid source is visible but no diagram appears | Check JavaScript and the emitted public `render-assets/` requests. Keep private `.localnotion` paths blocked. An explanatory message on one diagram indicates a source/render error; inspect that source before changing the whole page. |
| Private URL accessible | `@Private` excludes sitemap entries; configure Authentication for access control. |
| Protected URL accessible anonymously | Verify active authorization mappings and alternate file/media paths. |

## Useful diagnostics

Record the public route, intended Notion item, executable version, failing stage and sanitized error. For hosting issues, distinguish saved Notion content, changed local HTML, transferred files and active server state. Avoid full registry/object dumps, authentication values and keys. Follow [publishing](publishing.md) for boundary-by-boundary evidence.
