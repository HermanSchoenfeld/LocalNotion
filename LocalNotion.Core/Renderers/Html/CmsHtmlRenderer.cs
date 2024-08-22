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

	public new string HeaderHtmlSection { get => base.HeaderHtmlSection; set => base.HeaderHtmlSection = value; }

	public new string NavBarHtmlSection { get => base.NavBarHtmlSection; set => base.NavBarHtmlSection = value; }

	public new string FooterHtmlSection { get => base.FooterHtmlSection; set => base.FooterHtmlSection = value; }

	public bool IsPartialRendering { get; set; }

	protected override string Render(Page page)
		=> IsPartialRendering ? RenderPageContent(page) : base.Render(page);

	public string RenderCMSPageItem(string pageID, CMSPageType type) {
		if (Tools.Debugger.BreakConditionA && pageID == "957e39ad-3a63-43a0-91f3-8e1e132696a5") {
			Tools.Debugger.BreakConditionB= true;
		}
		var page = Repository.GetPage(pageID);
		var visualGraph = Repository.GetEditableResourceGraph(page.ID);
		var visualObjects = Repository.LoadObjects(visualGraph);
		IsPartialRendering = type switch {
			CMSPageType.Header => true,
			CMSPageType.Menu => true,
			CMSPageType.Page => false,
			CMSPageType.Section => true,
			CMSPageType.Gallery => false,
			CMSPageType.Footer => true,
			_ => throw new ArgumentOutOfRangeException(nameof(type), type, null)
		};

		using (EnterRenderingContext(new PageRenderingContext {
				Themes = new[] { !IsPartialRendering ? "cms" : "cms_section" }.Union(page.CMSProperties.Themes ?? []).ToArray(),
			       Resource = page,
			       RenderOutputPath = Repository.Paths.GetResourceTypeFolderPath(LocalNotionResourceType.CMS, FileSystemPathType.Absolute),
			       PageGraph = visualGraph,
			       PageObjects = visualObjects,
		       })) {
			return Render(RenderingContext.PageGraph);
		}
	}

	public string RenderSectionedPage(string[] pages)  {
		Tools.Debugger.BreakConditionA = true;
		var sections = pages.Select(Repository.GetPage).ToArray();
		var title = sections.Length > 0 ? sections[0].CMSProperties.Categories.LastOrDefault() ?? sections[0].Title ?? "Untitled" : "Untitled";
		var id = sections.Select(x => x.ID).ToDelimittedString(", ");
		var keywords = LocalNotionHelper.CombineMultiPageKeyWords(sections.Select(x => x.Keywords)).ToArray();

		// Render each section in their individual contexts
		var content = sections.Select(x => RenderCMSPageItem(x.ID, CMSPageType.Section)).ToDelimittedString(Environment.NewLine);

		using (EnterRenderingContext(new PageRenderingContext { Themes = ["cms"], RenderOutputPath  = Repository.Paths.GetResourceTypeFolderPath(LocalNotionResourceType.CMS, FileSystemPathType.Absolute)})) {
			IsPartialRendering = false;
			return RenderPageInternal(title, keywords, content, sections.Min(x => x.CreatedOn), sections.Min(x => x.LastEditedOn), "cms-sectioned-page", id);
		}
	}

	public string RenderArticlesPage(string slug) {
		var contentNode = Repository.CMSDatabase.GetContent(slug);
		var articles = contentNode.Visit(x => x.Children).SelectMany(x => x.Content).Where(CMSHelper.IsPublicContent).ToArray();
		var title = articles.Length > 0 ? articles[0].Title : "Untitled";
		var keywords = LocalNotionHelper.CombineMultiPageKeyWords(articles.Select(x => x.Keywords)).ToArray();

		using (EnterRenderingContext(new PageRenderingContext { Themes = ["cms"], RenderOutputPath  = Repository.Paths.GetResourceTypeFolderPath(LocalNotionResourceType.CMS, FileSystemPathType.Absolute) })) {
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

}
