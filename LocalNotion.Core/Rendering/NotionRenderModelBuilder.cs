// Copyright (c) Herman Schoenfeld 2018 - Present. All rights reserved. (https://sphere10.com/products/localnotion)
// Author: Herman Schoenfeld <herman@sphere10.com>
//
// Distributed under the GPLv3 software license, see the accompanying file LICENSE
// or visit https://github.com/HermanSchoenfeld/localnotion/blob/master/LICENSE
//
// This notice must not be removed when duplicating this file or its contents, in whole or in part.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Runtime.Serialization;
using System.Threading;
using System.Threading.Tasks;
using Sphere10.Framework.Web.VisualRenderer;
using Notion.Client;
using Sphere10.Framework;
using BookmarkBlock = Notion.Client.BookmarkBlock;
using R = Sphere10.Framework.Web.VisualRenderer;

namespace LocalNotion.Core;

public enum ProjectionPurpose { Html, Text }

/// <summary>Materializes source data and links into an independent, finite visual tree.</summary>
public sealed partial class NotionRenderModelBuilder {

	private readonly ILocalNotionRepository _repository;

	private ILinkGenerator _linkGenerator;

	private readonly ILogger _logger;

	// Leave room for list/container wrappers within the renderer traversal limit.
	private const int MaximumDepth = 96;

	public NotionRenderModelBuilder(ILocalNotionRepository repository = null, ILogger logger = null) {
		_repository = repository;
		_logger = logger;
	}

	private ILinkGenerator Links => _linkGenerator ??= _repository is null ? null : LinkGeneratorFactory.Create(_repository);

	public R.DocumentBlock Build(string resourceID, string outputDirectory) {
		if (_repository is null)
			throw new InvalidOperationException("A repository is required when projecting a stored resource.");
		var resource = (TryGetResource(resourceID, out var stored) ? stored : null) as LocalNotionEditableResource
			?? throw new InvalidOperationException($"Resource '{resourceID}' is not renderable.");
		var graph = _repository.GetEditableResourceGraph(resource.ID);
		return Build(resource, graph, LoadProjectionObjects(graph), outputDirectory);
	}

	public R.DocumentBlock Build(LocalNotionEditableResource resource, NotionObjectGraph graph, IDictionary<string, IObject> objects,
				string outputDirectory = null, ProjectionPurpose purpose = ProjectionPurpose.Html) {
		ArgumentNullException.ThrowIfNull(resource);
		ArgumentNullException.ThrowIfNull(graph);
		ArgumentNullException.ThrowIfNull(objects);
		if (purpose == ProjectionPurpose.Html && _repository is null)
			throw new InvalidOperationException("HTML source projection requires a repository; use the visual model directly for independent rendering.");
		var context = new ProjectionContext(resource, objects, outputDirectory, purpose);
		var root = FindObject(context, graph.ObjectID);
		R.Metadata metadata = this.Metadata(context, root, graph.ObjectID);
		context.Active.Add(NormalizeID(graph.ObjectID));
		var children = root is Database databaseValue
			? new VisualNode[] { MapDatabase(context, databaseValue, graph) }
			: MapChildren(context, graph, 0);
		var ancestors = purpose == ProjectionPurpose.Html ? GetAncestry(resource) : Array.Empty<LocalNotionResource>();
		var tags = resource.CMSProperties?.Tags ?? [];
		var isSubPage = ancestors.Skip(1).FirstOrDefault()?.Type == LocalNotionResourceType.Page;
		return new R.DocumentBlock {
			Metadata = metadata,
			Title = resource.Title ?? string.Empty,
			Name = resource.Name ?? string.Empty,
			Description = root is Database descriptionDatabase ? Plain(descriptionDatabase.Description) : resource.CMSProperties?.Summary ?? string.Empty,
			Subtitle = root is Database subtitleDatabase ? MapRichText(context, subtitleDatabase.Description) : [],
			Keywords = resource.Keywords?.ToArray() ?? [],
			Author = "Local Notion",
			CreatedTime = Timestamp(root is Page pageValue && pageValue.CreatedTime != default ? pageValue.CreatedTime : resource.CreatedOn),
			UpdatedTime = Timestamp(root is Page updatedPage && updatedPage.LastEditedTime != default ? updatedPage.LastEditedTime : resource.LastEditedOn),
			Icon = purpose == ProjectionPurpose.Html ? MapThumbnail(resource, resource.Thumbnail, outputDirectory) : null,
			CoverUrl = purpose == ProjectionPurpose.Html ? ResolveUrl(resource, resource.Cover, outputDirectory) : null,
			TitleOnCover = tags.Contains(Constants.TagShowTitleOnBanner) || isSubPage && tags.Contains(Constants.TagShowChildPageTitleOnBanner),
			Themes = purpose == ProjectionPurpose.Html ? _repository.DefaultThemes?.ToArray() ?? ["default"] : [],
			Children = children,
			PlainTextOverride = root is Database ? string.Empty : null
		};
	}

	public R.Reference ResolveReference(LocalNotionEditableResource source, string objectID, string outputDirectory,
				bool inline = false, bool omitIndicator = false) {
		if (!TryResolveResourceUrl(source, objectID, RenderType.HTML, outputDirectory, out var url, out var target))
			return new R.Reference { Label = $"Unresolved child resource {objectID}", IsAvailable = false, PlainTextOverride = Environment.NewLine };
		R.Icon icon = target is LocalNotionEditableResource editable ? MapThumbnail(editable, editable.Thumbnail, outputDirectory) : null;
		return new R.Reference {
			Url = url,
			Label = target.Title ?? string.Empty,
			Icon = icon,
			ShowIndicator = !omitIndicator && icon is not null,
			PlainTextOverride = Environment.NewLine
		};
	}

	private IDictionary<string, IObject> LoadProjectionObjects(NotionObjectGraph graph) {
		var objects = new Dictionary<string, IObject>();
		var visited = new HashSet<NotionObjectGraph>(ReferenceEqualityComparer.Instance);
		var attemptedIDs = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
		var pending = new Queue<(NotionObjectGraph Node, int Depth)>();
		pending.Enqueue((graph, 0));
		while (pending.TryDequeue(out var entry)) {
			if (entry.Node is null || !visited.Add(entry.Node))
				continue;
			if (attemptedIDs.Add(NormalizeID(entry.Node.ObjectID)))
				foreach (var candidate in IDCandidates(entry.Node.ObjectID))
					if (_repository.TryGetObject(candidate, out var source) && source is not null) {
						objects[candidate] = source;
						break;
					}
			// Do not traverse beyond what the mapper can render. Breadth-first order
			// preserves the shallowest route when a graph instance appears repeatedly.
			if (entry.Depth >= MaximumDepth)
				continue;
			foreach (var child in entry.Node.Children ?? [])
				pending.Enqueue((child, entry.Depth + 1));
		}
		return objects;
	}

	private VisualNode[] MapChildren(ProjectionContext context, NotionObjectGraph parent, int depth) {
		var children = parent.Children ?? [];
		var result = new List<VisualNode>();
		for (var index = 0; index < children.Length;) {
			var sourceObject = FindObject(context, children[index]?.ObjectID);
			var numbered = sourceObject is NumberedListItemBlock;
			if (numbered || sourceObject is BulletedListItemBlock) {
				var items = new List<R.ListItemBlock>();
				do {
					var mapped = MapNode(context, children[index++], depth + 1);
					items.Add(mapped as R.ListItemBlock ?? new R.ListItemBlock { Children = [mapped] });
					sourceObject = index < children.Length ? FindObject(context, children[index]?.ObjectID) : null;
				} while (numbered ? sourceObject is NumberedListItemBlock : sourceObject is BulletedListItemBlock);
				result.Add(new R.ListBlock { Type = numbered ? R.ListType.Numbered : R.ListType.Bulleted, Items = items.ToArray() });
			} else
				result.Add(MapNode(context, children[index++], depth + 1));
		}
		return result.ToArray();
	}

	private VisualNode MapNode(ProjectionContext context, NotionObjectGraph graph, int depth) {
		if (graph is null)
			return new R.UnsupportedBlock { Type = "missing", Diagnostic = "Missing graph node.", PlainTextOverride = Environment.NewLine };
		var source = FindObject(context, graph.ObjectID);
		R.Metadata metadata = this.Metadata(context, source, graph.ObjectID);
		if (depth > MaximumDepth || !context.Active.Add(NormalizeID(graph.ObjectID)))
			return new R.UnsupportedBlock { Metadata = metadata, Type = "cyclic-reference", Diagnostic = "Source content exceeds the traversal depth or contains a cycle.", PlainTextOverride = Environment.NewLine };
		try {
			VisualNode[] Children() => MapChildren(context, graph, depth);
			VisualNode[] OwnedChildren() => source is Block { HasChildren: true } ? Children() : [];
			VisualNode node = source switch {
				global::Notion.Client.ParagraphBlock item => new R.ParagraphBlock { Text = MapRichText(context, item.Paragraph?.RichText), Color = MapColor(item.Paragraph?.Color), Children = OwnedChildren() },
				HeadingOneBlock item => new R.HeadingBlock { Level = 1, Text = MapRichText(context, item.Heading_1?.RichText), Color = MapColor(item.Heading_1?.Color), IsToggleable = item.Heading_1?.IsToggleable ?? false, Children = OwnedChildren() },
				HeadingTwoBlock item => new R.HeadingBlock { Level = 2, Text = MapRichText(context, item.Heading_2?.RichText), Color = MapColor(item.Heading_2?.Color), IsToggleable = item.Heading_2?.IsToggleable ?? false, Children = OwnedChildren() },
				HeadingThreeBlock item => new R.HeadingBlock { Level = 3, Text = MapRichText(context, item.Heading_3?.RichText), Color = MapColor(item.Heading_3?.Color), IsToggleable = item.Heading_3?.IsToggleable ?? false, Children = OwnedChildren() },
				global::Notion.Client.QuoteBlock item => new R.QuoteBlock { Text = MapRichText(context, item.Quote?.RichText), Color = MapColor(item.Quote?.Color), Children = OwnedChildren(), PlainTextOverride = "\"" + PlainText(item.Quote?.RichText) + "\"" + Environment.NewLine },
				global::Notion.Client.CalloutBlock item => new R.CalloutBlock { Text = MapRichText(context, item.Callout?.RichText), Color = MapColor(item.Callout?.Color), Icon = MapIcon(context, item.Callout?.Icon), Children = OwnedChildren() },
				global::Notion.Client.ToDoBlock item => new R.ToDoBlock { Text = MapRichText(context, item.ToDo?.RichText), Color = MapColor(item.ToDo?.Color), IsChecked = item.ToDo?.IsChecked ?? false, Children = OwnedChildren() },
				global::Notion.Client.ToggleBlock item => new R.ToggleBlock { Text = MapRichText(context, item.Toggle?.RichText), Color = MapColor(item.Toggle?.Color), Children = OwnedChildren() },
				BulletedListItemBlock item => new R.ListItemBlock { Text = MapRichText(context, item.BulletedListItem?.RichText), Color = MapColor(item.BulletedListItem?.Color), Children = OwnedChildren() },
				NumberedListItemBlock item => new R.ListItemBlock { Text = MapRichText(context, item.NumberedListItem?.RichText), Color = MapColor(item.NumberedListItem?.Color), Children = OwnedChildren() },
				global::Notion.Client.TabBlock => MapTabs(context, graph, depth),
				global::Notion.Client.ColumnBlock => new R.ColumnBlock { Children = OwnedChildren() },
				global::Notion.Client.ColumnListBlock => MapColumns(context, graph, depth),
				global::Notion.Client.TableBlock item => MapTable(context, graph, item, depth),
				global::Notion.Client.TableRowBlock item => new R.TableRowBlock { Cells = (item.TableRow?.Cells ?? []).Select(cell => new R.TableCellBlock { Text = MapRichText(context, cell) }).ToArray() },
				global::Notion.Client.CodeBlock item => MapCode(context, item),
				global::Notion.Client.EquationBlock item => new R.EquationBlock { Expression = item.Equation?.Expression ?? string.Empty },
				global::Notion.Client.DividerBlock => new R.DividerBlock(),
				Page item => new R.ReferenceBlock { Reference = Reference(context, item.Id, true), PlainTextOverride = Environment.NewLine },
				ChildPageBlock item => new R.ReferenceBlock { Reference = Reference(context, item.Id, true), PlainTextOverride = Environment.NewLine },
				LinkToPageBlock item => new R.ReferenceBlock { Reference = Reference(context, item.LinkToPage?.GetId(), false), PlainTextOverride = Environment.NewLine },
				Database item => MapDatabase(context, item, graph),
				global::Notion.Client.BreadcrumbBlock => MapBreadcrumb(context),
				global::Notion.Client.TableOfContentsBlock item => new R.TableOfContentsBlock { Color = MapColor(item.TableOfContents?.Color), PlainTextOverride = "Table of Contents" + Environment.NewLine },
				AudioBlock item => MapMedia(context, item.Audio, R.MediaType.Audio) with { PlainTextOverride = $"Audio ({item.Audio})" + Environment.NewLine },
				ImageBlock item => MapMedia(context, item.Image, R.MediaType.Image) with { PlainTextOverride = $"Image ({item.Image}) {item.Image?.Caption}" + Environment.NewLine },
				FileBlock item => MapMedia(context, item.File, R.MediaType.File) with { PlainTextOverride = PlainText(item.File?.Caption) + Environment.NewLine },
				PDFBlock item => MapMedia(context, item.PDF, R.MediaType.Pdf) with { PlainTextOverride = PlainText(item.PDF?.Caption) + $"PDF: {item.PDF}" },
				VideoBlock item => MapVideo(context, item),
				EmbedBlock item => MapEmbed(context, item),
				User item => new R.ParagraphBlock { Text = [new PersonInline { Person = MapPerson(item) }], PlainTextOverride = PersonText(item) },
				_ => Unsupported(source)
			};
			return node with { Metadata = metadata };
		} finally {
			context.Active.Remove(NormalizeID(graph.ObjectID));
		}
	}

	private R.TabsBlock MapTabs(ProjectionContext context, NotionObjectGraph graph, int depth) {
		var panels = (graph.Children ?? []).Select(child => {
			var node = MapNode(context, child, depth + 1);
			if (node is not R.ParagraphBlock paragraph)
				return node; // Preserve unexpected source content rather than dropping it.
			var source = FindObject(context, child.ObjectID) as global::Notion.Client.ParagraphBlock;
			return (VisualNode)new R.TabBlock {
				Metadata = paragraph.Metadata,
				Text = paragraph.Text,
				Color = paragraph.Color,
				Icon = MapIcon(context, source?.Paragraph?.Icon),
				Children = paragraph.Children
			};
		}).ToArray();
		return new R.TabsBlock { Children = panels };
	}

	private R.ColumnListBlock MapColumns(ProjectionContext context, NotionObjectGraph graph, int depth) {
		var columns = (graph.Children ?? []).Select(child => MapNode(context, child, depth + 1)).Select(node =>
			node as R.ColumnBlock ?? new R.ColumnBlock { Metadata = node.Metadata, Children = [node] }).ToArray();
		return new R.ColumnListBlock { Columns = columns };
	}

	private R.TableBlock MapTable(ProjectionContext context, NotionObjectGraph graph, global::Notion.Client.TableBlock table, int depth) {
		var rows = table.HasChildren ? (graph.Children ?? []).Select(child => MapNode(context, child, depth + 1)).OfType<R.TableRowBlock>().ToArray() : [];
		return new R.TableBlock {
			ColumnCount = table.Table?.TableWidth ?? 0,
			// Preserve the existing renderer's interpretation, independent of the API names.
			FirstRowIsHeader = table.Table?.HasRowHeader ?? false,
			FirstColumnIsHeader = table.Table?.HasColumnHeader ?? false,
			Rows = rows,
			PlainTextOverride = table.HasChildren ? null : Environment.NewLine
		};
	}

	private VisualNode MapCode(ProjectionContext context, global::Notion.Client.CodeBlock blockValue) {
		var code = Plain(blockValue.Code?.RichText);
		if (blockValue.Code?.Language == "html" && Plain(blockValue.Code.Caption).Trim() == "{INJECT}")
			return new R.RawHtmlBlock { Html = code, Text = PlainText(blockValue.Code.RichText), PlainTextOverride = PlainText(blockValue.Code.RichText) + Environment.NewLine };
		return new R.CodeBlock { Code = code, Language = MapCodeLanguage(blockValue.Code?.Language), Caption = MapRichText(context, blockValue.Code?.Caption), PlainTextOverride = PlainText(blockValue.Code?.RichText) + Environment.NewLine };
	}

	private static CodeLanguage MapCodeLanguage(string language) {
		var identifier = (language ?? string.Empty).Replace("#", "Sharp").Replace("+", "Plus")
			.Replace("-", string.Empty).Replace(".", string.Empty).Replace(" ", string.Empty);
		return Enum.TryParse<CodeLanguage>(identifier, true, out var parsed) &&
			   string.Equals(Enum.GetName(parsed), identifier, StringComparison.OrdinalIgnoreCase)
			? parsed
			: CodeLanguage.Text;
	}

	private R.Reference Reference(ProjectionContext context, string id, bool omitIndicator)
		=> context.Purpose == ProjectionPurpose.Text
					? new R.Reference { Label = string.Empty, IsAvailable = false, PlainTextOverride = Environment.NewLine }
					: ResolveReference(context.Resource, id, context.OutputDirectory, omitIndicator: omitIndicator);

	private R.Metadata Metadata(ProjectionContext context, IObject source, string id) {
		var normalized = NormalizeID(id);
		var count = context.Occurrences.TryGetValue(normalized, out var seen) ? seen : 0;
		context.Occurrences[normalized] = count + 1;
		return new R.Metadata {
			SourceId = id,
			Anchor = count == 0 ? normalized : normalized + "-" + (count + 1),
			ObjectType = source?.Object.ToString().ToLowerInvariant().Replace('_', '-') ?? "block",
			Type = source is IBlock blockValue ? blockValue.Type.ToString().Replace('_', '-') : source?.GetType().Name ?? "missing"
		};
	}

	private static R.UnsupportedBlock Unsupported(object source) => new() {
		Type = source?.GetType().Name ?? "missing",
		Diagnostic = Diagnostic(source),
		PlainTextOverride = source is BookmarkBlock ? Environment.NewLine + Environment.NewLine : Environment.NewLine
	};

	private IObject FindObject(ProjectionContext context, string id) {
		foreach (var candidate in IDCandidates(id))
			if (context.Objects.TryGetValue(candidate, out var source))
				return source;
		if (context.Purpose == ProjectionPurpose.Html)
			foreach (var candidate in IDCandidates(id))
				if (_repository.TryGetObject(candidate, out var source))
					return source;
		return null;
	}

	private LocalNotionResource[] GetAncestry(LocalNotionResource resource) {
		var items = new List<LocalNotionResource>();
		var ids = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
		while (resource is not null && ids.Add(NormalizeID(resource.ID)) && items.Count < MaximumDepth) {
			items.Add(resource);
			if (string.IsNullOrWhiteSpace(resource.ParentResourceID) || !TryGetResource(resource.ParentResourceID, out resource))
				break;
		}
		return items.ToArray();
	}

	private static DateTimeOffset? Timestamp(DateTime value) => value == default ? null : new DateTimeOffset(value.Kind == DateTimeKind.Unspecified ? DateTime.SpecifyKind(value, DateTimeKind.Utc) : value);

	private static R.Color MapColor(global::Notion.Client.Color? colorValue) => MapColor(colorValue?.ToString());

	private static R.Color MapColor(string colorValue) => Enum.TryParse<R.Color>(colorValue?.Replace("_", string.Empty), true, out R.Color visual) ? visual : R.Color.Default;

	private static string EnumToken<T>(T value) where T : struct, Enum => value.GetAttribute<EnumMemberAttribute>()?.Value?.Replace('_', '-') ?? value.ToString().ToLowerInvariant();

	private static string Diagnostic(object value) {
		try { return Tools.Json.WriteToString(value ?? "NULL"); } catch { return value?.GetType().Name ?? "NULL"; }
	}

	private static string Plain(IEnumerable<RichTextBase> text) => string.Concat((text ?? []).Select(item => item?.PlainText ?? (item as RichTextText)?.Text?.Content ?? (item as RichTextEquation)?.Equation?.Expression ?? string.Empty));

	private sealed class ProjectionContext(LocalNotionEditableResource resource, IDictionary<string, IObject> objects, string outputDirectory, ProjectionPurpose purpose) {

		public LocalNotionEditableResource Resource { get; } = resource;

		public IDictionary<string, IObject> Objects { get; } = objects;

		public string OutputDirectory { get; } = outputDirectory;

		public ProjectionPurpose Purpose { get; } = purpose;

		public HashSet<string> Active { get; } = new(StringComparer.OrdinalIgnoreCase);

		public Dictionary<string, int> Occurrences { get; } = new(StringComparer.OrdinalIgnoreCase);
	}
}
