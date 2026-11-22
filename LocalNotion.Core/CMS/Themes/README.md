# Local Notion CMS menu themes

These are Local Notion's MIT-licensed Refined Navbar 1.1.3 presets. The runtime files retain their original bytes and license; the generic renderer remains independently versioned.

`RepositoryThemeDeployment` installs missing private definitions under the repository's configured Themes path. It also installs the two owned resource families at `themes/navbar_refined/resources` and `themes/navbar_bg_aux/resources` beneath the resolved repository directory, matching their `online_url` values. Existing private templates and public resource overrides, including empty files, are preserved.

The blank CMS schema offers six presentation options: `navbar_refined`, `navbar_refined_product_cards`, `navbar_refined_compact`, `navbar_refined_wide`, `navbar_standard`, and `navbar_bg_aux`. They are Themes, not Tags. The schema does not select a menu style or populate content rows.

The refined and standard layouts are alternatives. The auxiliary modifier consumes the existing `color_aux_*` contrast pairs; a menu does not have to share the body background. See the [CMS navigation guide](../../../docs/cms/headers-and-navigation.md).
