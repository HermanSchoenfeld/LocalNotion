// Copyright (c) Herman Schoenfeld 2018 - Present. All rights reserved. (https://sphere10.com/products/localnotion)
// Author: Herman Schoenfeld <herman@sphere10.com>
//
// Distributed under the GPLv3 software license, see the accompanying file LICENSE
// or visit https://github.com/HermanSchoenfeld/localnotion/blob/master/LICENSE
//
// This notice must not be removed when duplicating this file or its contents, in whole or in part.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Sphere10.Framework.Web.VisualRenderer;
using Notion.Client;
using Sphere10.Framework;
using R = Sphere10.Framework.Web.VisualRenderer;

namespace LocalNotion.Core;

/// <summary>Prepares CMS content and framing before the independent renderer is invoked.</summary>
public sealed class CmsRenderModelBuilder {

	private readonly CMSLocalNotionRepository _repository;

	private readonly ILocalNotionRepository _sources;

	private readonly NotionRenderModelBuilder _pages;

	private readonly string _outputDirectory;

	public CmsRenderModelBuilder(CMSLocalNotionRepository repository, string outputDirectory, ILogger logger = null)
		: this(repository, outputDirectory, logger, repository) {
	}

	internal CmsRenderModelBuilder(CMSLocalNotionRepository repository, string outputDirectory, ILogger logger, ILocalNotionRepository sources) {
		_repository = repository ?? throw new ArgumentNullException(nameof(repository));
		_sources = sources ?? throw new ArgumentNullException(nameof(sources));
		_outputDirectory = Path.GetFullPath(outputDirectory);
		_pages = new NotionRenderModelBuilder(_sources, logger);
	}

	public R.DocumentBlock Build(CMSItem item, int pageNumber = 1) => Build(item, pageNumber, null);

	internal R.DocumentBlock Build(CMSItem item, int pageNumber, CmsArticleListing listing) {
		ArgumentNullException.ThrowIfNull(item);
		if (pageNumber < 1 || (item.ItemType != CMSItemType.CategoryPage && pageNumber != 1))
			throw new ArgumentOutOfRangeException(nameof(pageNumber));
		R.DocumentBlock document = item.ItemType switch {
			CMSItemType.Page when item.Parts.Length == 1 => BuildPart(item.Parts[0], CMSPageType.Page),
			CMSItemType.Page => throw new InvalidOperationException("Page-based CMS items must have exactly one part."),
			CMSItemType.SectionedPage => BuildSections(item),
			CMSItemType.CategoryPage => BuildArticles(item, pageNumber, listing),
			CMSItemType.GalleryPage => BuildGallery(item),
			_ => throw new NotSupportedException($"Unsupported CMS item type '{item.ItemType}'.")
		};
		// Empty parts remain empty without loading inherited framing.
		if (document.Themes.Contains("empty", StringComparer.OrdinalIgnoreCase))
			return document with { RenderFrame = false, ShowPageHeader = false, Children = [] };
		if (item.ItemType == CMSItemType.Page && _repository.CMSItems.Any(listingItem =>
			listingItem.ItemType == CMSItemType.CategoryPage && listingItem.Parts.Contains(item.Parts[0], StringComparer.OrdinalIgnoreCase)))
			document = AddArticleNavigation(item, document);
		var (tokens, slots) = BuildFraming(item);
		// Standalone native child pages can opt into modifiers without inheriting section layout themes.
		if (item.ItemType == CMSItemType.Page) {
			var page = _repository.GetPage(item.Parts[0]);
			if (page.ParentResourceID != null && _repository.TryGetResource(page.ParentResourceID, out var parent) && parent.Type == LocalNotionResourceType.Page) {
				var commonThemes = tokens.TryGetValue("embedded_page_themes", out var common) ? common : null;
				var pageThemes = Guid.TryParse(page.ID, out var pageID) && tokens.TryGetValue($"embedded_page_themes:{pageID:D}", out var scoped) ? scoped : null;
				if (commonThemes != null || pageThemes != null)
					document = document with { Themes = document.Themes
						.Concat(new[] { commonThemes, pageThemes }.Where(value => value != null)
							.SelectMany(value => value.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)))
						.Distinct(StringComparer.Ordinal).ToArray() };
			}
		}
		// Empty parts remain empty, including framing, as with the existing empty theme.
		if (document.Themes.Contains("empty", StringComparer.OrdinalIgnoreCase))
			return document with { RenderFrame = false, ShowPageHeader = false, Children = [] };
		return document with { Tokens = tokens, Slots = slots, Author = string.IsNullOrWhiteSpace(item.Author) ? "Local Notion" : item.Author };
	}

	private R.DocumentBlock AddArticleNavigation(CMSItem item, R.DocumentBlock document) {
		var nodes = document.Children.SelectMany(Descendants).ToArray();
		var navigation = new List<VisualNode>();
		if (!nodes.OfType<R.BreadcrumbBlock>().Any())
			navigation.Add(_pages.BuildBreadcrumb(item.Parts[0], _outputDirectory));
		if (!nodes.OfType<R.TableOfContentsBlock>().Any() && nodes.OfType<R.HeadingBlock>().Any(heading =>
			heading.Level is >= 1 and <= 3 && heading.Text.Any(inline => !string.IsNullOrWhiteSpace(inline switch {
				R.TextInline text => text.Text,
				R.EquationInline equation => equation.Expression,
				_ => inline.PlainTextOverride
			}))))
			navigation.Add(new R.TableOfContentsBlock { PlainTextOverride = "Table of Contents" + Environment.NewLine });
		// A generated TOC follows the authored breadcrumb's owner without moving native content.
		if (navigation.Count == 1 && navigation[0] is R.TableOfContentsBlock) {
			var breadcrumbIndex = Array.FindIndex(document.Children, child => Descendants(child).OfType<R.BreadcrumbBlock>().Any());
			return document with { Children = document.Children.Take(breadcrumbIndex + 1).Concat(navigation).Concat(document.Children.Skip(breadcrumbIndex + 1)).ToArray() };
		}
		return navigation.Count == 0 ? document : document with { Children = navigation.Concat(document.Children).ToArray() };

		IEnumerable<VisualNode> Descendants(VisualNode node) {
			// Embedded documents have their own navigation and heading scope.
			if (node is R.DocumentBlock)
				yield break;
			yield return node;
			var children = node switch {
				R.ContainerVisual container => container.Children,
				R.ColumnListBlock columns => columns.Columns.Cast<VisualNode>().ToArray(),
				_ => Array.Empty<VisualNode>()
			};
			foreach (var child in children.SelectMany(Descendants))
				yield return child;
		}
	}

	private R.DocumentBlock BuildSections(CMSItem item) {
		var pages = item.Parts.Select(_repository.GetPage).ToArray();
		var first = pages.FirstOrDefault();
		var title = first?.CMSProperties?.Categories.LastOrDefault() ?? first?.Title ?? Constants.DefaultResourceTitle;
		return Shell(item, "cms", "cms-sectioned-page", title, pages,
			pages.Select(page => (VisualNode)BuildPart(page.ID, CMSPageType.Section)).ToArray());
	}

	private R.DocumentBlock BuildArticles(CMSItem item, int pageNumber, CmsArticleListing listing) {
		var content = _repository.CMSDatabase.GetContent(item.Slug);
		listing ??= new CmsArticleListing(_repository, item);
		var articles = listing.Articles;
		var pageArticles = listing.GetPage(pageNumber);
		var root = content.GetLogicalContentRoot();
		var source = articles.SelectMany(node => node.Content).FirstOrDefault();
		var all = new CMSContentNode { Slug = root.Slug, TitleOverride = root.Title.ToUpperInvariant() };
		var counts = new Dictionary<string, string> {
			["range_start"] = (articles.Length == 0 ? 0 : (pageNumber - 1) * CmsArticleListing.PageSize + 1).ToString(CultureInfo.InvariantCulture),
			["range_end"] = Math.Min(pageNumber * CmsArticleListing.PageSize, articles.Length).ToString(CultureInfo.InvariantCulture),
			["total_count"] = articles.Length.ToString(CultureInfo.InvariantCulture),
			["page_number"] = pageNumber.ToString(CultureInfo.InvariantCulture),
			["page_count"] = listing.PageCount.ToString(CultureInfo.InvariantCulture)
		};
		R.TemplateBlock body = Template("articles", counts, new() {
			["category_root"] = Category(all, 1, new HashSet<CMSContentNode>()),
			["categories"] = Group(root.Children.Where(node => node.Type == CMSContentType.Book)
				.Select(node => Category(node, 1, new HashSet<CMSContentNode>()))),
			["summaries"] = Group(pageArticles.Select((node, index) => Summary(node, index % 2 == 1))),
			["pagination"] = Template("articles_pagination", counts, new() { ["links"] = Group(PageLinks()) })
		});
		return Shell(item, "cms_articles", "cms-articles", content.Title,
			articles.SelectMany(node => node.Content).ToArray(), [body]) with {
			Keywords = content.Keywords.ToArray(),
			Description = content.Summary
		};

		IEnumerable<VisualNode> PageLinks() {
			if (listing.PageCount == 1)
				yield break;
			if (pageNumber > 1)
				yield return PageLink(pageNumber - 1, "Previous");
			var previous = 0;
			// Keep the control bounded even for large publications, with the first and last page always reachable.
			foreach (var number in Enumerable.Range(Math.Max(1, pageNumber - 2), Math.Min(listing.PageCount, pageNumber + 2) - Math.Max(1, pageNumber - 2) + 1)
				.Prepend(1).Append(listing.PageCount).Distinct()) {
				if (previous != 0 && number > previous + 1)
					yield return Template("articles_page_gap");
				yield return number == pageNumber
					? Template("articles_page_active", new() { ["label"] = number.ToString(CultureInfo.InvariantCulture) })
					: PageLink(number, number.ToString(CultureInfo.InvariantCulture));
				previous = number;
			}
			if (pageNumber < listing.PageCount)
				yield return PageLink(pageNumber + 1, "Next");
		}

		VisualNode PageLink(int number, string label) {
			string url;
			if (_repository.Paths.Mode == LocalNotionMode.Online) {
				url = ResolveCmsUrl(source, item.Slug);
				if (number > 1)
					url += "?page=" + number.ToString(CultureInfo.InvariantCulture);
			} else {
				if (item.ListingRenderPaths == null || item.ListingRenderPaths.Length != listing.PageCount)
					throw new InvalidOperationException("Offline article listing paths must be reserved before projection.");
				url = Path.GetRelativePath(_outputDirectory, Path.GetFullPath(item.ListingRenderPaths[number - 1],
					_repository.Paths.GetRepositoryPath(FileSystemPathType.Absolute))).Replace('\\', '/');
			}
			return Template("articles_page_link", new() { ["url"] = url, ["label"] = label });
		}

		VisualNode Category(CMSContentNode node, int indent, HashSet<CMSContentNode> ancestry) {
			if (indent > 128 || !ancestry.Add(node))
				throw new InvalidOperationException("Cycle or excessive depth in CMS category hierarchy.");
			try {
				return Template(node.Slug.Equals(content.Slug, StringComparison.OrdinalIgnoreCase) ? "articles_category_active" : "articles_category",
					new() {
						["indent_level"] = indent.ToString(CultureInfo.InvariantCulture),
						// Existing theme overrides prepend '/' to the slug; resolved destinations use the separate url token.
						["slug"] = LocalNotionHelper.SanitizeSlug(node.Slug),
						["url"] = ResolveCmsUrl(source, node.Slug),
						["title"] = node.Title
					},
					new() { ["children"] = Group(node.Children.Where(child => child.Type == CMSContentType.Book).Select(child => Category(child, indent + 1, ancestry))) });
			} finally { ancestry.Remove(node); }
		}

		VisualNode Summary(CMSContentNode node, bool alternate) {
			var primary = node.Content.FirstOrDefault();
			var image = primary == null ? "" : Feature(primary);
			var thumbnail = node.Thumbnail;
			var thumbnailUrl = thumbnail.Type == ThumbnailType.Image ? Resolve(primary, thumbnail.Data) : "";
			if (!string.IsNullOrWhiteSpace(image) && !string.IsNullOrWhiteSpace(thumbnailUrl) && Tools.FileSystem.DoPathsReferToSameFileName(image, thumbnailUrl)) {
				image = Feature(primary, preferCover: true);
				if (!string.IsNullOrWhiteSpace(image) && Tools.FileSystem.DoPathsReferToSameFileName(image, thumbnailUrl))
					image = "";
			}
			VisualNode thumbnailNode = thumbnail.Type switch {
				ThumbnailType.Emoji => Template("articles_summary_thumbnail_emoji", new() { ["emoji"] = thumbnail.Data }),
				ThumbnailType.Image => Template("articles_summary_thumbnail_image", new() { ["url"] = thumbnailUrl }),
				_ => new R.GroupBlock()
			};
			var feature = Template("articles_summary_feature", new() { ["url"] = CssUrl(image) }, new() { ["thumbnail"] = thumbnailNode });
			// Only the cover-fit modifier belongs on the feature; page layout Themes must not enter the collection.
			if (primary?.CMSProperties?.Themes?.Contains("header_cover_contain", StringComparer.Ordinal) == true)
				feature = feature with { Themes = ["cms_articles", "header_cover_contain"] };
			return Template(alternate ? "articles_summary_alt" : "articles_summary", new() {
				["title"] = node.Title ?? "",
				["created_on"] = node.CreatedOn.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
				["created_on_formatted"] = node.CreatedOn.ToString("D"),
				["summary"] = node.Summary ?? "",
				["slug"] = LocalNotionHelper.SanitizeSlug(node.Slug),
				["url"] = ResolveCmsUrl(primary, node.Slug)
			}, new() {
				["feature"] = feature
			});
		}
	}

	private R.DocumentBlock BuildGallery(CMSItem item) {
		var content = _repository.CMSDatabase.GetContent(item.Slug);
		var pages = content.Children.SelectMany(node => node.Content).Where(CMSHelper.IsPublicContent).ToArray();
		var title = pages.FirstOrDefault()?.CMSProperties?.Root ?? pages.FirstOrDefault()?.Title ?? Constants.DefaultResourceTitle;
		var galleryId = Tools.Text.ToCasing(TextCasing.KebabCase, title, FirstCharacterPolicy.HtmlDomObj);
		R.TemplateBlock body = Template("gallery", new() { ["gallery_id"] = galleryId }, new() {
			["badges"] = Group(pages.SelectMany(Badges).DistinctBy(badge => badge.Name).Select(badge => Template("gallery_badge", new() {
				["gallery_id"] = galleryId,
				["badge_name"] = badge.Name,
				["badge_title"] = badge.Title
			}))),
			["cards"] = Group(pages.Select(Card))
		});
		return Shell(item, "cms_gallery", "cms-gallery", title, pages, [body]);

		IEnumerable<(string Title, string Name)> Badges(LocalNotionPage page) {
			var properties = page.CMSProperties;
			if (properties == null)
				yield break;
			foreach (var category in new[] { properties.Category1, properties.Category2, properties.Category3, properties.Category4, properties.Category5 }) {
				if (!string.IsNullOrWhiteSpace(category))
					yield return (category, galleryId + "-" + Tools.Text.ToCasing(TextCasing.KebabCase, category, FirstCharacterPolicy.HtmlDomObj));
			}
		}

		VisualNode Card(LocalNotionPage page) {
			var image = Feature(page);
			return Template("gallery_card", new() {
				["gallery_id"] = galleryId,
				["badges"] = string.Join(" ", Badges(page).Select(badge => badge.Name)),
				["url"] = ResolveCmsUrl(page, page.CMSProperties?.CustomSlug ?? (page.TryGetRender(RenderType.HTML, out var render) ? render.Slug : "/")),
				["title"] = page.Title ?? "",
				["summary"] = page.CMSProperties?.Summary ?? ""
			}, new() {
				["cover"] = string.IsNullOrEmpty(image) ? new R.GroupBlock() : Template("gallery_card_cover", new() { ["title"] = page.Title ?? "", ["url"] = image })
			});
		}
	}

	private R.DocumentBlock Shell(CMSItem item, string theme, string type, string title, LocalNotionPage[] pages, VisualNode[] children) => new() {
		Title = title ?? Constants.DefaultResourceTitle,
		Description = item.Description ?? "",
		Author = string.IsNullOrWhiteSpace(item.Author) ? "Local Notion" : item.Author,
		Keywords = LocalNotionHelper.CombineMultiPageKeyWords(pages.Select(page => page.Keywords ?? [])).ToArray(),
		CreatedTime = pages.Length == 0 ? null : Timestamp(pages.Min(page => page.CreatedOn)),
		UpdatedTime = pages.Length == 0 ? null : Timestamp(pages.Min(page => page.LastEditedOn)),
		Metadata = new() { SourceId = string.Join(", ", pages.Select(page => page.ID)), Type = type, ObjectType = type },
		Themes = [theme],
		ShowPageHeader = false,
		Children = children
	};

	private R.DocumentBlock BuildPart(string pageId, CMSPageType type) {
		var page = _repository.GetPage(pageId);
		var (fragment, theme) = type switch {
			CMSPageType.Header => (true, "cms_header"),
			CMSPageType.NavBar => (true, "cms_navbar"),
			CMSPageType.Page => (false, "cms"),
			CMSPageType.Section => (true, "cms_section"),
			CMSPageType.Gallery => (false, "cms_gallery"),
			CMSPageType.Footer => (true, "cms_footer"),
			_ => throw new NotSupportedException($"Unsupported CMS part '{type}'.")
		};
		var themes = new[] { theme }.Union(page.CMSProperties?.Themes ?? []).ToList();
		var ancestry = new List<LocalNotionResource>();
		var visited = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
		LocalNotionResource ancestor = page;
		while (ancestor != null && visited.Add(ancestor.ID)) {
			ancestry.Add(ancestor);
			if (ancestor.ParentResourceID == null || !_repository.TryGetResource(ancestor.ParentResourceID, out ancestor))
				break;
		}
		if (ancestry.Skip(1).FirstOrDefault()?.Type == LocalNotionResourceType.Page)
			themes.AddRange(ancestry.OfType<LocalNotionEditableResource>()
				.Where(value => value.CMSProperties is { PageType: CMSPageType.Page, Themes: not null })
				.SelectMany(value => value.CMSProperties.Themes).Distinct());
		if (themes.Contains("empty", StringComparer.OrdinalIgnoreCase))
			return new R.DocumentBlock { RenderFrame = false, ShowPageHeader = false, Themes = themes.ToArray() };
		return _pages.Build(page.ID, _outputDirectory) with { RenderFrame = !fragment, Themes = themes.ToArray() };
	}

	private (IReadOnlyDictionary<string, string>, IReadOnlyDictionary<string, VisualNode>) BuildFraming(CMSItem item) {
		var tokens = new Dictionary<string, string> {
			["html_body_start"] = "",
			["html_body_end"] = "",
			["html_head_start"] = "",
			["html_head_end"] = "",
			["google_tag"] = "",
			["page_header"] = "",
			["page_navbar"] = "",
			["page_footer"] = "",
			["color"] = "default",
			["site_icon_url"] = ""
		};
		if (!string.IsNullOrWhiteSpace(item.InternalID)) {
			foreach (var graphNode in EnumerateInternalNodes(_sources.GetEditableResourceGraph(item.InternalID))) {
				if (string.IsNullOrWhiteSpace(graphNode.ObjectID) || !_sources.TryGetObject(graphNode.ObjectID, out var source) || source is not global::Notion.Client.CodeBlock block)
					continue;
				var caption = block.Code.Caption.ToPlainText();
				if (!string.IsNullOrWhiteSpace(caption))
					tokens[caption.Trim()] = block.Code.RichText.ToPlainText();
			}
		}
		if (_repository.TryGetResource(_repository.CMSDatabaseID, out var resource) && resource is LocalNotionEditableResource database && database.Thumbnail.Type == ThumbnailType.Image)
			tokens["site_icon_url"] = WebUtility.HtmlEncode(Resolve(database, database.Thumbnail.Data));
		var slots = new Dictionary<string, VisualNode>();
		if (!string.IsNullOrWhiteSpace(item.HeaderID))
			slots["page_header"] = BuildPart(item.HeaderID, CMSPageType.Header);
		if (!string.IsNullOrWhiteSpace(item.MenuID))
			slots["page_navbar"] = BuildPart(item.MenuID, CMSPageType.NavBar);
		if (!string.IsNullOrWhiteSpace(item.FooterID))
			slots["page_footer"] = BuildPart(item.FooterID, CMSPageType.Footer);
		return (tokens, slots);
	}

	private static IEnumerable<NotionObjectGraph> EnumerateInternalNodes(NotionObjectGraph root) {
		var pending = new Stack<(NotionObjectGraph Node, int Depth)>();
		var seen = new HashSet<NotionObjectGraph>(ReferenceEqualityComparer.Instance);
		pending.Push((root, 0));
		while (pending.TryPop(out var next)) {
			if (next.Node == null || next.Depth > 96 || !seen.Add(next.Node))
				continue;
			yield return next.Node;
			var children = next.Node.Children ?? [];
			for (var index = children.Length - 1; index >= 0; index--)
				pending.Push((children[index], next.Depth + 1));
		}
	}

	private string Feature(LocalNotionPage page, bool preferCover = false) {
		var tags = page.CMSProperties?.Tags ?? [];
		string FirstImage() => !string.IsNullOrWhiteSpace(page.FeatureImageID) && _sources.TryGetObject(page.FeatureImageID, out var value) && value is ImageBlock image
			? _pages.ResolveMediaUrl(page, image.Image, _outputDirectory) : "";
		string Cover() => Resolve(page, page.Cover);
		string Thumbnail() => page.Thumbnail.Type == ThumbnailType.Image ? Resolve(page, page.Thumbnail.Data) : "";
		var candidates = preferCover ? new Func<string>[] { Cover, Thumbnail }
			: tags.Contains(Constants.TagUseFirstImageAsFeature) ? [FirstImage, Cover, Thumbnail]
			: tags.Contains(Constants.TagUseCoverAsFeature) ? [Cover, Thumbnail]
			: tags.Contains(Constants.TagUseThumbnailAsFeature) ? [Thumbnail]
			: [FirstImage, Cover, Thumbnail];
		return candidates.Select(candidate => candidate()).FirstOrDefault(value => !string.IsNullOrWhiteSpace(value)) ?? "";
	}

	private string Resolve(LocalNotionEditableResource source, string urlValue) => string.IsNullOrWhiteSpace(urlValue) ? "" : source == null ? urlValue : _pages.ResolveUrl(source, urlValue, _outputDirectory);

	private string ResolveCmsUrl(LocalNotionEditableResource source, string slug) {
		if (string.IsNullOrWhiteSpace(slug))
			slug = "";
		if (Uri.TryCreate(slug, UriKind.Absolute, out var external) && external.Scheme is "http" or "https")
			return slug;
		var anchorIndex = slug.IndexOf('#');
		var anchor = anchorIndex >= 0 ? slug[anchorIndex..] : "";
		var targetSlug = anchorIndex >= 0 ? slug[..anchorIndex] : slug;
		if (_repository.Paths.Mode == LocalNotionMode.Online)
			return _repository.Paths.GetRemoteHostedBaseUrl().TrimEnd('/') + "/" + LocalNotionHelper.SanitizeSlug(targetSlug).TrimStart('/') + anchor;
		var target = _repository.CMSItems.FirstOrDefault(candidate => string.Equals(Tools.Url.StripAnchorTag(candidate.Slug ?? ""), targetSlug, StringComparison.OrdinalIgnoreCase));
		if (!string.IsNullOrWhiteSpace(target?.RenderPath))
			return Path.GetRelativePath(_outputDirectory, Path.GetFullPath(target.RenderPath, _repository.Paths.GetRepositoryPath(FileSystemPathType.Absolute))).Replace('\\', '/') + anchor;
		return Resolve(source, slug);
	}

	private static DateTimeOffset? Timestamp(DateTime value) => value == default ? null : new DateTimeOffset(value.Kind == DateTimeKind.Unspecified ? DateTime.SpecifyKind(value, DateTimeKind.Utc) : value);

	// This value is embedded in a quoted CSS url() before the enclosing HTML attribute is encoded.
	private static string CssUrl(string urlValue) => (urlValue ?? "").Replace("'", "%27").Replace("\\", "%5C")
		.Replace("\r", "%0D").Replace("\n", "%0A").Replace("\f", "%0C");

	private static R.GroupBlock Group(IEnumerable<VisualNode> nodes) => new() { Children = nodes.ToArray() };

	private static R.TemplateBlock Template(string name, Dictionary<string, string> tokens = null, Dictionary<string, VisualNode> slots = null) => new() {
		Template = name,
		Tokens = (tokens ?? new()).ToDictionary(token => token.Key, token => WebUtility.HtmlEncode(token.Value ?? "")),
		Slots = slots ?? new()
	};
}
