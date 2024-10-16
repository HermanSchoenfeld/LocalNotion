using Hydrogen;
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
		var ambientTokens = FetchFramingTokens(string.Empty, cmsItem.MenuID, cmsItem.FooterID);
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
		var ambientTokens = FetchFramingTokens(cmsItem.HeaderID, cmsItem.MenuID, cmsItem.FooterID);

		using (EnterRenderingContext(new PageRenderingContext { Themes = ["cms"], AmbientTokens = ambientTokens, RenderOutputPath = Repository.Paths.GetResourceTypeFolderPath(LocalNotionResourceType.CMS, FileSystemPathType.Absolute)})) {
			IsPartialRendering = false;
			return RenderPageInternal(title, keywords, content, sections.Min(x => x.CreatedOn), sections.Min(x => x.LastEditedOn), "cms-sectioned-page", id);
		}
	}

	protected virtual string RenderArticlesPage(CMSItem cmsItem) {
		var contentNode = Repository.CMSDatabase.GetContent(cmsItem.Slug);
		var articleNodes = 
			cmsItem
				.Parts
				.Select(Repository.GetPage)
				.GroupBy(x => Tools.Url.StripAnchorTag(x.CMSProperties.CustomSlug))
				.Select(g => Repository.CMSDatabase.GetContent(g.Key))
				.ToArray();
		
		// load framing
		var ambientTokens = FetchFramingTokens(cmsItem.HeaderID, cmsItem.MenuID, cmsItem.FooterID);

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
				RenderTemplate(
					"articles",
					new RenderTokens() {
						["category_root"] = RenderCategoryTree(allNode, 1),
						["categories"] = root.Children.Select(x => RenderCategoryTree(x, 1)).ToDelimittedString(Environment.NewLine),
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
			// Get all the info from node (TODO: can refactor this out and remove CMSItem altogether)
			return RenderTemplate(
				!alternateRow ? "articles_summary" : "articles_summary_alt",
				new RenderTokens {
					["image"] = articleNode.Thumbnail.Type switch {
						ThumbnailType.Emoji => RenderEmoji(articleNode.Thumbnail.Data),
						ThumbnailType.Image => RenderThumbnail(articleNode.Thumbnail.Data),
						ThumbnailType.None => string.Empty,
						_ => string.Empty
					},
					["title"] = articleNode.Title.ToNullWhenWhitespace() ?? string.Empty,
					["created_on"] = articleNode.CreatedOn.ToString("yyyy-MM-dd"),
					["created_on_formatted"] = articleNode.CreatedOn.ToString("D"),
					["summary"] = articleNode.Summary ?? string.Empty,
					["slug"] = LocalNotionHelper.SanitizeSlug(articleNode.Slug),
				}
			);

			string RenderEmoji(string emoji) 
				=> RenderTemplate(
					"articles_summary_emoji",
					new RenderTokens {
						["emoji"] = emoji
					}
				);


			string RenderThumbnail(string url) 
				=> RenderTemplate(
					"articles_summary_thumbnail",
					new RenderTokens {
						["url"] = url.ToNullWhenWhitespace() ?? "https://via.placeholder.com/200"
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
		var ambientTokens = FetchFramingTokens(cmsItem.HeaderID, cmsItem.MenuID, cmsItem.FooterID);

		using (EnterRenderingContext(new PageRenderingContext { Themes = ["cms_gallery"], AmbientTokens = ambientTokens, RenderOutputPath  = Repository.Paths.GetResourceTypeFolderPath(LocalNotionResourceType.CMS, FileSystemPathType.Absolute) })) {
			IsPartialRendering = false;
			return RenderPageInternal(
				galleryTitle,
				keywords,
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
					["cover"] = TryGetGalleryCover(card, out var url) ? 
								RenderTemplate("gallery_card_cover", new RenderTokens { ["title"] = card.Title, ["url"] = url } ) : 
								string.Empty,
					["url"] =  LocalNotionHelper.SanitizeSlug(card.CMSProperties?.CustomSlug ?? (card.Renders.TryGetValue(RenderType.HTML, out var renderEntry) ? renderEntry.Slug : "/")),
					["title"] = card.Title,
					["summary"] =  card.CMSProperties?.Summary ?? string.Empty,
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
				yield return (BadgeTitle: page.CMSProperties.Category2, BadgeName: galleryID + "-" +Tools.Text.ToCasing(TextCasing.KebabCase, page.CMSProperties.Category2, FirstCharacterPolicy.HtmlDomObj));
			}

			if (!string.IsNullOrWhiteSpace(page.CMSProperties.Category3)) {
				yield return (BadgeTitle: page.CMSProperties.Category3, BadgeName: galleryID + "-" +Tools.Text.ToCasing(TextCasing.KebabCase, page.CMSProperties.Category3, FirstCharacterPolicy.HtmlDomObj));
			}

			if (!string.IsNullOrWhiteSpace(page.CMSProperties.Category4)) {
				yield return (BadgeTitle: page.CMSProperties.Category4, BadgeName: galleryID + "-" +Tools.Text.ToCasing(TextCasing.KebabCase, page.CMSProperties.Category4, FirstCharacterPolicy.HtmlDomObj));
			}

			if (!string.IsNullOrWhiteSpace(page.CMSProperties.Category5)) {
				yield return (BadgeTitle: page.CMSProperties.Category5, BadgeName: galleryID + "-" +Tools.Text.ToCasing(TextCasing.KebabCase, page.CMSProperties.Category5, FirstCharacterPolicy.HtmlDomObj));
			}

		}
		

		bool TryGetGalleryCover(LocalNotionPage page, out string url) {
			if (string.IsNullOrWhiteSpace(page.Cover)) {
				url = string.Empty;
				return false;
			}
			url = page.Cover;
			return true;
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

		using (EnterRenderingContext(new PageRenderingContext {
			       Themes = new[] { theme }.Union(page.CMSProperties.Themes ?? []).ToArray(),
			       AmbientTokens = ambientTokens ?? new Dictionary<string, object>(),
			       Resource = page,
			       RenderOutputPath = Repository.Paths.GetResourceTypeFolderPath(LocalNotionResourceType.CMS, FileSystemPathType.Absolute),
			       PageGraph = visualGraph,
			       PageObjects = visualObjects,
		       })) {
			return Render(RenderingContext.PageGraph);
		}
	}

	protected virtual Dictionary<string, object> FetchFramingTokens(string headerID, string navBarID, string footerID) {
		var tokens = new Dictionary<string, object>();
		if (!headerID.IsNullOrWhiteSpace()) {
			tokens["include://page_header.inc"] = RenderCmsItemPart(headerID, CMSPageType.Header);
		} else {
			tokens["include://page_header.inc"] = string.Empty;
		}

		if (!navBarID.IsNullOrWhiteSpace()) {
			tokens["include://page_navbar.inc"] = RenderCmsItemPart(navBarID, CMSPageType.NavBar);
		} else {
			tokens["include://page_navbar.inc"] = string.Empty;
		}
		
		if (!footerID.IsNullOrWhiteSpace()) {
			tokens["include://page_footer.inc"] = RenderCmsItemPart(footerID, CMSPageType.Footer);
		} else {
			tokens["include://page_footer.inc"] = string.Empty;
		}

		var cmsDatabase = Repository.GetDatabase(Repository.CMSDatabaseID);
		tokens["site_icon_url"] = cmsDatabase.Thumbnail.Type == ThumbnailType.Image ? cmsDatabase.Thumbnail.Data : string.Empty;

		return tokens;
	}

}
