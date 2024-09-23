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
			case CMSItemType.ArticleCategory:
				return RenderArticlesPage(cmsItem);
			case CMSItemType.GalleryPage:
				Logger.Warning($"Gallery rendering not implemented: {cmsItem.Title} ({cmsItem.Slug})");
				return string.Empty;
			default:
				throw new ArgumentOutOfRangeException();
		}
	}

	protected virtual string RenderPage(CMSItem cmsItem)  {
		var ambientTokens = FetchFramingTokens(cmsItem.HeaderID, cmsItem.MenuID, cmsItem.FooterID);
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

		using (EnterRenderingContext(new PageRenderingContext { Themes = ["cms"], AmbientTokens = ambientTokens,  RenderOutputPath  = Repository.Paths.GetResourceTypeFolderPath(LocalNotionResourceType.CMS, FileSystemPathType.Absolute)})) {
			IsPartialRendering = false;
			return RenderPageInternal(title, keywords, content, sections.Min(x => x.CreatedOn), sections.Min(x => x.LastEditedOn), "cms-sectioned-page", id);
		}
	}

	protected virtual string RenderArticlesPage(CMSItem cmsItem) {
		var contentNode = Repository.CMSDatabase.GetContent(cmsItem.Slug);
		var articles = contentNode.Visit(x => x.Children).SelectMany(x => x.Content).Where(CMSHelper.IsPublicContent).ToArray();
		var title = articles.Length > 0 ? articles[0].Title : "Untitled";
		var keywords = LocalNotionHelper.CombineMultiPageKeyWords(articles.Select(x => x.Keywords)).ToArray();

		
		// load framing 
		var ambientTokens = FetchFramingTokens(cmsItem.HeaderID, cmsItem.MenuID, cmsItem.FooterID);

		using (EnterRenderingContext(new PageRenderingContext { Themes = ["cms"], AmbientTokens = ambientTokens, RenderOutputPath  = Repository.Paths.GetResourceTypeFolderPath(LocalNotionResourceType.CMS, FileSystemPathType.Absolute) })) {
			IsPartialRendering = false;
			var root = contentNode.GetLogicalContentRoot();

			var allNode = new CMSContentNode {
				Parent = null,
				Slug = root.Slug,
				Title = root.Title.ToUpperInvariant(),
			};

			return RenderPageInternal(
				title,
				keywords,
				RenderTemplate(
					"articles",
					new RenderTokens() {
						["category_root"] = RenderCategoryTree(allNode, 1),
						["categories"] = root.Children.Select(x => RenderCategoryTree(x, 1)).ToDelimittedString(Environment.NewLine),
						["summaries"] = articles.Select((x, i) => RenderSummary(x, i % 2 == 1)).ToDelimittedString(Environment.NewLine)
					}
				),
				articles.Min(x => x.CreatedOn), 
				articles.Min(x => x.LastEditedOn),
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
									.Where(x => x.IsCategoryNode)
									.Select(x => RenderCategoryTree(x, indentLevel + 1))
									.ToDelimittedString(Environment.NewLine)
				}
			);
		}

		string RenderSummary(LocalNotionPage article, bool alternateRow)  {

			return RenderTemplate(
				!alternateRow ? "articles_summary" : "articles_summary_alt",
				new RenderTokens {
					["image"] = article.Thumbnail.Type switch {
						ThumbnailType.Emoji => RenderEmoji(article.Thumbnail.Data),
						ThumbnailType.Image => RenderThumbnail(article.Thumbnail.Data),
						ThumbnailType.None => string.Empty,
						_ => string.Empty
					},
					["title"] = article.Title.ToNullWhenWhitespace() ?? string.Empty,
					["created_on"] = article.CreatedOn.ToString("yyyy-MM-dd"),
					["created_on_formatted"] = article.CreatedOn.ToString("D"),
					["summary"] = article.CMSProperties.Summary ?? string.Empty,
					["slug"] = LocalNotionHelper.SanitizeSlug(article.CMSProperties.CustomSlug),
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

	protected virtual string RenderCmsItemPart(string partID, CMSPageType partType, IDictionary<string, object> ambientTokens = null) {
		var page = Repository.GetPage(partID);
		var visualGraph = Repository.GetEditableResourceGraph(page.ID);
		var visualObjects = Repository.LoadObjects(visualGraph);
		(IsPartialRendering, var theme) = partType switch {
			CMSPageType.Header => (true, "cms_header"),
			CMSPageType.NavBar => (true, "cms_navbar"),
			CMSPageType.Page => (false, "cms"),
			CMSPageType.Section => (true, "cms_section"),
			CMSPageType.Gallery => (false, "cms"),
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

		return tokens;
	}

}
