# CMS database schema

Keep canonical names and types exactly as listed. Notion views can hide or reorder columns without changing the schema.

All twenty-one columns are created by `create-cms`. Structural detection retains the existing nineteen required names, with Authentication and Keywords optional, and explicitly checks Themes as multi-select and Sequence as number. Parsing expects compatible canonical types for the other fields too. Existing CMS databases do not have to add Keywords to keep working.

## All twenty-one properties

| Property | Exact Notion type | Purpose and normal use |
| --- | --- | --- |
| Item | `title` | Page title; normally contributes the last path component. |
| Type | `select` | CMS role. A blank value defaults to Page; choose an explicit supported role. |
| Status | `select` | Ordinary content requires Published. Blank/unrecognized values become Hidden. This is not Notion's separate status type. |
| Publish On | `date` | Optional earliest publication time, compared with UTC. |
| Themes | `multi_select` | Render overrides; empty selection uses applicable defaults. |
| Custom Slug | `rich_text` | Optional normalized route override; `/` explicitly selects home for a Page. |
| Sequence | `number` | Ordering value converted to an integer; use distinct whole numbers. |
| Root | `rich_text` | First hierarchy component; blank means root level. |
| Category1 | `rich_text` | Next component after Root. |
| Category2 | `rich_text` | Next component after Category1. |
| Category3 | `rich_text` | Next component after Category2. |
| Category4 | `rich_text` | Next component after Category3. |
| Category5 | `rich_text` | Next component after Category4. |
| Tags | `multi_select` | Behavioral flags and custom metadata; see [tags](tags.md). |
| Summary | `rich_text` | Short CMS description used in summaries and metadata. |
| Keywords | `rich_text` | Optional comma-separated authored keyword phrases. Nonempty phrases override automatic extraction; clearing an override restores automatic keywords. |
| Authentication | `rich_text` | Optional secret-bearing route credentials; blank for public content. Optional to structural detection. |
| Created By | `created_by` | Notion-managed creator. |
| Created On | `created_time` | Notion-managed creation time. |
| Edited By | `last_edited_by` | Notion-managed last editor. |
| Edited On | `last_edited_time` | Notion-managed edit time. |

Created/edited properties are native system columns, not text columns with similar names. Notion fills them automatically on created pages, including the temporary setup row retained in Trash after creation. The finished CMS has no active content rows. The canonical schema has no formulas, relations or rollups.

## Type options

| Option | Preset color | Support |
| --- | --- | --- |
| Page | green | Standalone content |
| Section | gray | Part of a composed page |
| Gallery | purple | Gallery entry/listing behavior |
| File | default | Reference preset; unsupported by the current CMS parser |
| NavBar | blue | Navigation framing |
| Header | yellow | Banner/header framing |
| Footer | red | Footer framing |
| Internal | pink | Template-token framing |

See [page types](page-types-and-publication.md). Generated `sectioned_page` is not another option to add.

## Status options

| Option | Preset color | Ordinary content |
| --- | --- | --- |
| Draft | yellow | Excluded |
| QA | blue | Excluded |
| Published | green | Included when Publish On is empty or due |
| Hidden | red | Excluded |

Status is a select, so there are no status groups. Header, NavBar, Footer and Internal bypass this gate and have Tags/Summary cleared during parsing. Do not disable a Footer by merely setting Draft; see [publication caveats](page-types-and-publication.md).

## Palette and structure

All [twelve tags](tags.md) and [228 themes](theme-reference.md) are preset. Editor choice colors do not determine website colors. A custom tag needs a consumer; a theme name needs a matching valid theme. Adding an option does not implement a feature.

Root and categories are plain text, filled consecutively. Ordinary routing stops at the first blank, including blank Root. Gallery rows use Root for grouping and Category1–Category5 for badges. Generated menu/header/footer/parts references are output metadata, not missing relation columns.
