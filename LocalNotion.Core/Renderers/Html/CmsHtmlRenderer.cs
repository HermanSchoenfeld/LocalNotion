// Copyright (c) Herman Schoenfeld 2018 - Present. All rights reserved. (https://sphere10.com/products/localnotion)
// Author: Herman Schoenfeld <herman@sphere10.com>
//
// Distributed under the GPLv3 software license, see the accompanying file LICENSE 
// or visit https://github.com/HermanSchoenfeld/localnotion/blob/master/LICENSE
//
// This notice must not be removed when duplicating this file or its contents, in whole or in part.

using Sphere10.Framework;
using Notion.Client;

namespace LocalNotion.Core;

public class CmsHtmlRenderer : HtmlRenderer {

	public CmsHtmlRenderer(
		RenderMode renderMode,
		CMSLocalNotionRepository repository,
		HtmlThemeManager themeManager,
		ILinkGenerator resolver,
		IBreadCrumbGenerator breadCrumbGenerator,
		ILogger logger
	) : base(renderMode, repository, themeManager, resolver, breadCrumbGenerator, logger) {
	}

	protected new CMSLocalNotionRepository Repository => (CMSLocalNotionRepository)base.Repository;

	public bool IsPartialRendering { get; set; }

	protected override string Render(Page page)
		=> IsPartialRendering ? RenderPageContent(page) : base.Render(page);

	public string RenderCmsItem(CMSItem cmsItem)  {
		switch (cmsItem.ItemType) {
			case CMSItemType.Page:
				Guard.Argument(cmsItem.Parts.Length == 1, nameof(cmsItem), "Page-based items must have exactly one part");
				return RenderPage(cmsItem);
			case CMSItemType.SectionedPage:
				return RenderSectionedPage(cmsItem);
			case CMSItemType.CategoryPage:
				return RenderArticlesPage(cmsItem);
			case CMSItemType.GalleryPage:
				return RenderGalleryPage(cmsItem);
			default:
				throw new ArgumentOutOfRangeException();
		}
	}

	protected virtual string RenderPage(CMSItem cmsItem)  {
		// load framing (no cms header for normal pages, rely on page header)
		var ambientTokens = FetchFramingTokens(string.Empty, cmsItem.MenuID, cmsItem.FooterID, cmsItem.InternalID);
		return RenderCmsItemPart(cmsItem.Parts[0], CMSPageType.Page, ambientTokens);
	}

	protected virtual string RenderSectionedPage(CMSItem cmsItem)  {
		Guard.ArgumentNotNull(cmsItem, nameof(cmsItem));
		var sections = cmsItem.Parts.Select(Repository.GetPage).ToArray();
		var title = sections.Length > 0 ? sections[0].CMSProperties.Categories.LastOrDefault() ?? sections[0].Title ?? "Untitled" : "Untitled";
		var id = sections.Select(x => x.ID).ToDelimittedString(", ");
		var keywords = LocalNotionHelper.CombineMultiPageKeyWords(sections.Select(x => x.Keywords)).ToArray();

		// Render each section in their individual contexts
		var content = sections.Select(x => RenderCmsItemPart(x.ID, CMSPageType.Section)).ToDelimittedString(Environment.NewLine);

		// load framing 
		var ambientTokens = FetchFramingTokens(cmsItem.HeaderID, cmsItem.MenuID, cmsItem.FooterID, cmsItem.InternalID);

		using (EnterRenderingContext(new PageRenderingContext { Themes = ["cms"], AmbientTokens = ambientTokens, RenderOutputPath = Repository.Paths.GetResourceTypeFolderPath(LocalNotionResourceType.CMS, FileSystemPathType.Absolute)})) {
			IsPartialRendering = false;
			return RenderPageInternal(title, keywords, cmsItem.Description, content, sections.Min(x => x.CreatedOn), sections.Min(x => x.LastEditedOn), "cms-sectioned-page", id);
		}
	}

	protected virtual string RenderArticlesPage(CMSItem cmsItem) {
		var contentNode = Repository.CMSDatabase.GetContent(cmsItem.Slug);
		var articleNodes = 
			cmsItem
				.Parts
				.Select(Repository.GetPage)
				.Where(x => !x.CMSProperties.PageType.IsIn(CMSPageType.Header, CMSPageType.NavBar, CMSPageType.Footer, CMSPageType.Internal))
				.GroupBy(x => Tools.Url.StripAnchorTag(x.CMSProperties.CustomSlug))
				.Select(g => Repository.CMSDatabase.GetContent(g.Key))
				.OrderBy(x => x.Sequence)
				.ThenBy(x => x.CreatedOn)
				.ThenByDescending(x => x.LastEditedOn)
				.ToArray();
		
		// load framing
		var ambientTokens = FetchFramingTokens(cmsItem.HeaderID, cmsItem.MenuID, cmsItem.FooterID, cmsItem.InternalID);

		using (EnterRenderingContext(new PageRenderingContext { Themes = ["cms_articles"], AmbientTokens = ambientTokens, RenderOutputPath  = Repository.Paths.GetResourceTypeFolderPath(LocalNotionResourceType.CMS, FileSystemPathType.Absolute) })) {
			IsPartialRendering = false;
			var root = contentNode.GetLogicalContentRoot();

			var allNode = new CMSContentNode {
				Parent = null,
				Slug = root.Slug,
				TitleOverride = root.Title.ToUpperInvariant(),
			};

			return RenderPageInternal(
				contentNode.Title,
				contentNode.Keywords.ToArray(),
				contentNode.Summary,
				RenderTemplate(
					"articles",
					new RenderTokens() {
						["category_root"] = RenderCategoryTree(allNode, 1),
						["categories"] = root.Children.Where(x => x.Type == CMSContentType.Book).Select(x => RenderCategoryTree(x, 1)).ToDelimittedString(Environment.NewLine),
						["summaries"] = articleNodes.Select((x, i) => RenderSummary(x, i % 2 == 1)).ToDelimittedString(Environment.NewLine)
					}
				),
				articleNodes.Min(x => x.CreatedOn), 
				articleNodes.Min(x => x.LastEditedOn),
				"cms-articles",
				string.Empty
			);
		}

		string RenderCategoryTree(CMSContentNode category, int indentLevel) {
			return RenderTemplate(
				category.Slug.Equals(contentNode.Slug) ? "articles_category_active" : "articles_category",
				new RenderTokens {
					["indent_level"] = indentLevel,
					["slug"] = LocalNotionHelper.SanitizeSlug(category.Slug),
					["title"] = category.Title,
					["children"] = category
									.Children
									.Where(x => x.Type == CMSContentType.Book)
									.Select(x => RenderCategoryTree(x, indentLevel + 1))
									.ToDelimittedString(Environment.NewLine)
				}
			);
		}

		string RenderSummary(CMSContentNode articleNode, bool alternateRow)  {
			var primaryPage = articleNode.Content.First();
			// TODO:
			// figure out images to use here
			var backgroundImage = TryGetPageFeature(primaryPage, null, null, null, out var url) ? url : string.Empty;
			var thumbnailImage = articleNode.Thumbnail.Type switch {
				ThumbnailType.Image => articleNode.Thumbnail.Data.ToNullWhenWhitespace() ?? string.Empty,
				_ => null
			};

			// do not use a background image if same as thumbnail
			if (!string.IsNullOrWhiteSpace(backgroundImage) && !string.IsNullOrWhiteSpace(thumbnailImage) && Tools.FileSystem.DoPathsReferToSameFileName(backgroundImage, thumbnailImage)) {
				// background and thumbnail same, use cover for background
				backgroundImage = TryGetPageFeature(primaryPage, null, true, null, out url) ? url : string.Empty;
				if (!string.IsNullOrWhiteSpace(backgroundImage) && !string.IsNullOrWhiteSpace(thumbnailImage) && Tools.FileSystem.DoPathsReferToSameFileName(backgroundImage, thumbnailImage)) {
					backgroundImage = string.Empty;
				}
			}

			// Get all the info from node
			return RenderTemplate(
				!alternateRow ? "articles_summary" : "articles_summary_alt",
				new RenderTokens {
					["feature"] =
						RenderTemplate(
							"articles_summary_feature",
							new RenderTokens {
								["url"] = backgroundImage,
								["thumbnail"] = articleNode.Thumbnail.Type switch {
									ThumbnailType.Emoji => RenderTemplate(
										"articles_summary_thumbnail_emoji",
										new RenderTokens {
											["emoji"] = articleNode.Thumbnail.Data
										}
									),
									ThumbnailType.Image => RenderTemplate(
										"articles_summary_thumbnail_image",
										new RenderTokens {
											["url"] = thumbnailImage
										}
									),
									_ => string.Empty
								}
							}
						),
					["title"] = articleNode.Title.ToNullWhenWhitespace() ?? string.Empty,
					["created_on"] = articleNode.CreatedOn.ToString("yyyy-MM-dd"),
					["created_on_formatted"] = articleNode.CreatedOn.ToString("D"),
					["summary"] = articleNode.Summary ?? string.Empty,
					["slug"] = LocalNotionHelper.SanitizeSlug(articleNode.Slug),
				}
			);
		}
	}

	protected virtual string RenderGalleryPage(CMSItem cmsItem) {
		var contentNode = Repository.CMSDatabase.GetContent(cmsItem.Slug);
		var galleryPages = contentNode.Children.SelectMany(x => x.Content).Where(CMSHelper.IsPublicContent).ToArray();
		string galleryTitle;
		if (galleryPages.Any()) {
			var samplePage = galleryPages.First();
			galleryTitle = samplePage.CMSProperties.Root ?? samplePage.Title ?? Constants.DefaultResourceTitle;
		} else {
			galleryTitle = Constants.DefaultResourceTitle;
		}

		var galleryID = Tools.Text.ToCasing(TextCasing.KebabCase, galleryTitle, FirstCharacterPolicy.HtmlDomObj);

		var keywords = LocalNotionHelper.CombineMultiPageKeyWords(galleryPages.Select(x => x.Keywords)).ToArray();

		// load framing
		var ambientTokens = FetchFramingTokens(cmsItem.HeaderID, cmsItem.MenuID, cmsItem.FooterID, cmsItem.InternalID);

		using (EnterRenderingContext(new PageRenderingContext { Themes = ["cms_gallery"], AmbientTokens = ambientTokens, RenderOutputPath = Repository.Paths.GetResourceTypeFolderPath(LocalNotionResourceType.CMS, FileSystemPathType.Absolute) })) {
			IsPartialRendering = false;
			return RenderPageInternal(
				galleryTitle,
				keywords,
				cmsItem.Description,
				RenderTemplate(
					"gallery",
					new RenderTokens {
						["gallery_id"] = galleryID,
						["badges"] = galleryPages
										.SelectMany(GetPageBadges)
										.DistinctBy(x => x.BadgeName)
										.Select(x => RenderBadges(x.BadgeTitle, x.BadgeName))
										.ToDelimittedString(Environment.NewLine),

						["cards"] = galleryPages
										.Select(RenderCard)
										.ToDelimittedString(Environment.NewLine)
					}
				),
				galleryPages.Min(x => x.CreatedOn),
				galleryPages.Min(x => x.LastEditedOn),
				"cms-gallery",
				string.Empty
			);
		}

		string RenderBadges(string badgeTitle, string badgeName) {
			return RenderTemplate(
				"gallery_badge",
				new RenderTokens {
					["gallery_id"] = galleryID,
					["badge_name"] = badgeName,
					["badge_title"] = badgeTitle,
				}
			);
		}

		string RenderCard(LocalNotionPage card) {
			return RenderTemplate(
				"gallery_card",
				new RenderTokens {
					["gallery_id"] = galleryID,
					["badges"] = GetPageBadges(card).Select(x => x.BadgeName).ToDelimittedString(" "),
					["cover"] = TryGetPageFeature(card, null, null, null, out var url) ?
								RenderTemplate("gallery_card_cover", new RenderTokens { ["title"] = card.Title, ["url"] = url }) :
								string.Empty,
					["url"] = LocalNotionHelper.SanitizeSlug(card.CMSProperties?.CustomSlug ?? (card.Renders.TryGetValue(RenderType.HTML, out var renderEntry) ? renderEntry.Slug : "/")),
					["title"] = card.Title,
					["summary"] = card.CMSProperties?.Summary ?? string.Empty,
				}
			);
		}

		IEnumerable<(string BadgeTitle, string BadgeName)> GetPageBadges(LocalNotionPage page) {

			if (page.CMSProperties is null) {
				yield break;
			}

			if (!string.IsNullOrWhiteSpace(page.CMSProperties.Category1)) {
				yield return (BadgeTitle: page.CMSProperties.Category1, BadgeName: galleryID + "-" + Tools.Text.ToCasing(TextCasing.KebabCase, page.CMSProperties.Category1, FirstCharacterPolicy.HtmlDomObj));
			}

			if (!string.IsNullOrWhiteSpace(page.CMSProperties.Category2)) {
				yield return (BadgeTitle: page.CMSProperties.Category2, BadgeName: galleryID + "-" + Tools.Text.ToCasing(TextCasing.KebabCase, page.CMSProperties.Category2, FirstCharacterPolicy.HtmlDomObj));
			}

			if (!string.IsNullOrWhiteSpace(page.CMSProperties.Category3)) {
				yield return (BadgeTitle: page.CMSProperties.Category3, BadgeName: galleryID + "-" + Tools.Text.ToCasing(TextCasing.KebabCase, page.CMSProperties.Category3, FirstCharacterPolicy.HtmlDomObj));
			}

			if (!string.IsNullOrWhiteSpace(page.CMSProperties.Category4)) {
				yield return (BadgeTitle: page.CMSProperties.Category4, BadgeName: galleryID + "-" + Tools.Text.ToCasing(TextCasing.KebabCase, page.CMSProperties.Category4, FirstCharacterPolicy.HtmlDomObj));
			}

			if (!string.IsNullOrWhiteSpace(page.CMSProperties.Category5)) {
				yield return (BadgeTitle: page.CMSProperties.Category5, BadgeName: galleryID + "-" + Tools.Text.ToCasing(TextCasing.KebabCase, page.CMSProperties.Category5, FirstCharacterPolicy.HtmlDomObj));
			}
		}
	}

	protected virtual string RenderCmsItemPart(string partID, CMSPageType partType, IDictionary<string, object> ambientTokens = null) {
		var page = Repository.GetPage(partID);
		var visualGraph = Repository.GetEditableResourceGraph(page.ID);
		var visualObjects = Repository.LoadObjects(visualGraph);
		(IsPartialRendering, var theme) = partType switch {
			CMSPageType.Header => (true, "cms_header"),
			CMSPageType.NavBar => (true, "cms_navbar"),
			CMSPageType.Page => (false, "cms"),
			CMSPageType.Section => (true, "cms_section"),
			CMSPageType.Gallery => (false, "cms_gallery"),
			CMSPageType.Footer => (true, "cms_footer"),
			_ => throw new ArgumentOutOfRangeException(nameof(partType), partType, null)
		};

		var pageThemes = new[] { theme }.Union(page.CMSProperties.Themes ?? []).ToList();

		// Ugly hack: return empty if has empty theme
		if (pageThemes.Contains("empty"))
			return string.Empty;

		using (EnterRenderingContext(new PageRenderingContext {
			       Themes = pageThemes.ToArray(),
			       AmbientTokens = ambientTokens ?? new Dictionary<string, object>(),
			       Resource = page,
			       RenderOutputPath = Repository.Paths.GetResourceTypeFolderPath(LocalNotionResourceType.CMS, FileSystemPathType.Absolute),
			       PageGraph = visualGraph,
			       PageObjects = visualObjects,
		       })) {
			return Render(RenderingContext.PageGraph);
		}
	}

	protected virtual Dictionary<string, object> FetchFramingTokens(string headerID, string navBarID, string footerID, string internalID) {
		var tokens = new Dictionary<string, object>() {
			["html_body_start"] = string.Empty,
			["html_body_end"] = string.Empty,
			["html_head_start"] = string.Empty,
			["html_head_end"] = string.Empty,
			["google_tag"] = string.Empty,
			["page_header"] = string.Empty,
			["page_navbar"] = string.Empty,
			["page_footer"] = string.Empty,
		};
 
		// get tags from internal page
		if (!internalID.IsNullOrWhiteSpace()) {
			foreach(var block in Repository.GetEditableResourceGraph(internalID).VisitAll()) {
				var notionObj = Repository.GetObject(block.ObjectID);
				if (notionObj is CodeBlock codeBlock) {
					var caption = codeBlock.Code.Caption.ToPlainText();
					if (!string.IsNullOrWhiteSpace(caption)) {
						tokens[caption.Trim()] = codeBlock.Code.RichText.ToPlainText();
					}
				}
			}
		}

		// get the content-based tags
		var cmsDatabase = Repository.GetDatabase(Repository.CMSDatabaseID);
		tokens["site_icon_url"] = cmsDatabase.Thumbnail.Type == ThumbnailType.Image ? cmsDatabase.Thumbnail.Data : string.Empty;
		tokens["color"] = "default";

		if (!headerID.IsNullOrWhiteSpace()) {
			tokens["page_header"] = RenderCmsItemPart(headerID, CMSPageType.Header);
		} 

		if (!navBarID.IsNullOrWhiteSpace()) {
			tokens["page_navbar"] = RenderCmsItemPart(navBarID, CMSPageType.NavBar);
		} 
		
		if (!footerID.IsNullOrWhiteSpace()) {
			tokens["page_footer"] = RenderCmsItemPart(footerID, CMSPageType.Footer);
		}

		return tokens;
	}

	protected bool TryGetPageFeature(LocalNotionPage page, bool? featureImportant, bool? coverImportant, bool? thumbnailImportant, out string url) {
		var useFeature = featureImportant ?? page.CMSProperties.Tags.Contains(Constants.TagUseFirstImageAsFeature);
		var useCover = coverImportant ?? page.CMSProperties.Tags.Contains(Constants.TagUseCoverAsFeature);
		var useThumbnail = thumbnailImportant ?? page.CMSProperties.Tags.Contains(Constants.TagUseThumbnailAsFeature);
		url = null;
		if (useFeature) {
			if (!GetFirstImage(out url))
				if (!GetCover(out url))
					if (!GetThumbnail(out url))
						return false;
			return true;
		}

		if (useCover) {
			if (!GetCover(out url))
				if (!GetThumbnail(out url))
					return false;
			return true;
		}

		if (useThumbnail) {
			if (!GetThumbnail(out url))
				return false;
			return true;
		}

		// default
		if (!GetFirstImage(out url))
			if (!GetCover(out url))
				if (!GetThumbnail(out url))
					return false;
		return true;


		bool GetFirstImage(out string url) {
			if (!string.IsNullOrWhiteSpace(page.FeatureImageID)) {
				var imageBlock = Repository.GetObject(page.FeatureImageID) as ImageBlock;
				if (imageBlock != null) {
					url = GetFileUrl(imageBlock.Image, out _);
					return true;
				}
			}
			url = null;
			return false;
		}

		bool GetCover(out string url) {
			if (!string.IsNullOrWhiteSpace(page.Cover)) {
				url = page.Cover;
				return true;
			}
			url = null;
			return false;
		}

		bool GetThumbnail(out string url) {
			if (page.Thumbnail.Type == ThumbnailType.Image) {
				url = page.Thumbnail.Data;
				return true;
			}
			url = null;
			return false;
		}

	}


}
