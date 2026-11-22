# Renderer characterization

Run `dotnet test tests/LocalNotion.Core.Tests/LocalNotion.Core.Tests.csproj --filter FullyQualifiedName~LegacyRenderingTests`.

The checked-in `legacy-main.txt` and `legacy-text.txt` were captured by running the original Core HTML and text renderers against the same deterministic 31-object source fixture before removing them. The test now uses only the new Core projection and independent renderer and compares their output to those captured results.

The fixture covers nested paragraphs, all three headings, toggle headings, adjacent and nested ordered/bulleted lists, quotes with children, callouts, code, to-dos, toggles, tables with both header dimensions, columns, a divider and an equation. Plain text matched exactly on Windows when the baseline was captured; the permanent comparison normalizes CRLF/LF for cross-platform execution.

HTML compares the content of `main`, excluding output asset addressing, with whitespace and attribute-order normalization. It additionally accounts for these deliberate changes:

- List containers now have their own occurrence IDs instead of reusing the enclosing page/item ID.
- The legacy unresolved `ln-color-{color}` list class is replaced by a concrete default color.
- A numbered list explicitly carries its starting number; a start of one is equivalent to the old implicit default.

The independent consumer and visual-model tests belong to Commercial/tests/Sphere10.Framework.Web.VisualRenderer.Tests. This solution verifies LocalNotion integration against the compiled Sphere10.Framework.Web.VisualRenderer package.

