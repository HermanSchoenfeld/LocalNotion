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
using System.Threading.Tasks;
using AngleSharp.Html.Parser;
using LocalNotion.Core;
using Notion.Client;
using NUnit.Framework;
using Sphere10.Framework;
using R = Sphere10.Framework.Web.VisualRenderer;

namespace LocalNotion.Core.Tests;

[TestFixture]
[Category("Integration")]
[FixtureLifeCycle(LifeCycle.InstancePerTestCase)]
[Parallelizable(ParallelScope.Children)]
public class CmsRenderModelBuilderTests {
	private string _fixtureRoot;
	private CMSLocalNotionRepository _repository;

	[SetUp]
	public void SetUp() {
		_fixtureRoot = Path.Combine(Path.GetTempPath(), "localnotion-cms-projection-" + Guid.NewGuid().ToString("N"));
		Directory.CreateDirectory(_fixtureRoot);
	}

	[TearDown]
	public void TearDown() {
		try {
			_repository?.Dispose();
		} finally {
			var fullPath = Path.GetFullPath(_fixtureRoot);
			if (!fullPath.StartsWith(Path.GetFullPath(Path.GetTempPath()), StringComparison.OrdinalIgnoreCase))
				throw new InvalidOperationException("Fixture path escaped its temporary parent.");
			if (Directory.Exists(fullPath))
				Directory.Delete(fullPath, true);
		}
	}

	[Test]
	public async Task ArticleFeatureProjectsOnlyTheExplicitCoverFitModifier([Values(false, true)] bool selected) {
		await CreateDocument(LocalNotionMode.Online, "/");
		var item = _repository.GetCMSItem("docs");
		var first = _repository.GetPage(item.Parts[0]);
		var second = _repository.GetPage(item.Parts[1]);
		first.CMSProperties.Themes = selected ? ["header_small", "header_cover_contain", "table_scroll"] : ["header_small", "table_scroll"];
		second.CMSProperties.Themes = ["header_small"];
		var document = new CmsRenderModelBuilder(_repository, Path.Combine(_fixtureRoot, "cms", "export")).Build(item);
		var summaries = Templates(document).Where(block => block.Template is "articles_summary" or "articles_summary_alt").ToArray();
		var firstFeature = (R.TemplateBlock)summaries[0].Slots["feature"];
		var secondFeature = (R.TemplateBlock)summaries[1].Slots["feature"];
		Assert.That(firstFeature.Themes, Is.EqualTo(selected ? new[] { "cms_articles", "header_cover_contain" } : Array.Empty<string>()));
		Assert.That(secondFeature.Themes, Is.Empty, "Another article must retain its existing feature crop.");
		Assert.That(summaries.SelectMany(summary => summary.Themes), Is.Empty, "The modifier is scoped to the feature widget.");
		Assert.That(document.Themes, Is.EqualTo(new[] { "cms_articles" }), "Collection framing remains independent of article page Themes.");
		Assert.That(first.CMSProperties.Themes, Is.EqualTo(selected
			? new[] { "header_small", "header_cover_contain", "table_scroll" }
			: new[] { "header_small", "table_scroll" }), "Projection must preserve authored selections.");

		// The real renderer must enter and leave the feature's Theme scope without changing its sibling.
		var themesDirectory = Path.Combine(_fixtureRoot, "feature-overrides");
		var modifierDirectory = Path.Combine(themesDirectory, "header_cover_contain");
		Directory.CreateDirectory(modifierDirectory);
		await File.WriteAllTextAsync(Path.Combine(modifierDirectory, ".config.json"), "{\"type\":\"html\"}");
		await File.WriteAllTextAsync(Path.Combine(modifierDirectory, "articles_summary_feature.html"), "<div data-test-feature-fit=\"contain\" data-feature-url=\"{url}\">{thumbnail}</div>");
		var renderer = new R.HtmlRenderer(new R.ThemeCatalog(new R.ThemeOptions { ThemesDirectory = themesDirectory }));
		var html = renderer.Render(document, new R.RenderOptions { Environment = R.RenderEnvironment.Online }).Html;
		var parsed = new HtmlParser().ParseDocument(html);
		Assert.That(parsed.QuerySelectorAll("[data-test-feature-fit=contain]"), Has.Length.EqualTo(selected ? 1 : 0));
		Assert.That(parsed.QuerySelectorAll(".article-summary"), Has.Length.EqualTo(2));
		if (!selected)
			Assert.That(html, Is.EqualTo(new R.HtmlRenderer().Render(document, new R.RenderOptions { Environment = R.RenderEnvironment.Online }).Html),
				"Installing the modifier must leave an unselected collection's full HTML unchanged.");
	}

	[Test]
	public async Task NativeChildModifiersComeFromInternalWithoutInheritingSectionLayout([Values(false, true)] bool nativeChild) {
		// Use real repository graphs so the inherited Internal token and native parent are projected together.
		await CreateDocument(LocalNotionMode.Online, "/");
		var item = _repository.GetCMSItem("docs/first");
		var page = _repository.GetPage(item.Parts[0]);
		var parent = AddArticle(page.ParentResourceID, "Parent section", "docs#parent", 3);
		parent.CMSProperties.PageType = CMSPageType.Section;
		parent.CMSProperties.Themes = ["business1", "shape_lineup"];
		page.CMSProperties.Themes = [];
		page.CMSProperties.Tags = [Constants.TagHideEmbeddedPages];
		if (nativeChild)
			page.ParentResourceID = parent.ID;
		var source = new Page { Id = page.ID };
		var paragraph = new global::Notion.Client.ParagraphBlock { Id = Guid.NewGuid().ToString(), Paragraph = new() { RichText = [Run("Independent destination body")] } };
		_repository.AddObject(source);
		_repository.AddObject(paragraph);
		_repository.AddResourceGraph(new NotionObjectGraph { ObjectID = source.Id, Children = [new() { ObjectID = paragraph.Id }] });
		var internalId = Guid.NewGuid().ToString();
		var setting = new global::Notion.Client.CodeBlock { Id = Guid.NewGuid().ToString(), Code = new() {
			Language = "plain text", Caption = [Run("embedded_page_themes")], RichText = [Run("h3_typed, h3_color_aux, h3_typed,,")]
		} };
		_repository.AddObject(setting);
		_repository.AddResourceGraph(new NotionObjectGraph { ObjectID = internalId, Children = [new() { ObjectID = setting.Id }] });
		item.InternalID = internalId;
		var document = new CmsRenderModelBuilder(_repository, Path.Combine(_fixtureRoot, "cms", "export")).Build(item);
		Assert.That(document.Themes, Is.EqualTo(nativeChild
			? new[] { "cms", "h3_typed", "h3_color_aux" }
			: new[] { "cms" }));
		Assert.That(document.HideEmbeddedPages, Is.True);
		Assert.That(document.RenderFrame, Is.True, "A hidden reference must not hide the independently rendered child document.");
		Assert.That(document.Children.OfType<R.ParagraphBlock>().Count(), Is.EqualTo(1));
		Assert.That(document.Tokens["embedded_page_themes"], Is.EqualTo("h3_typed, h3_color_aux, h3_typed,,"), "Source settings remain intact.");
	}

	[Test]
	public async Task PageScopedInternalModifiersDoNotLeakToOtherPagesOrDatasourceRows([Values(false, true)] bool nativeChild) {
		await CreateDocument(LocalNotionMode.Online, "/");
		var firstItem = _repository.GetCMSItem("docs/first");
		var secondItem = _repository.GetCMSItem("docs/second");
		var first = _repository.GetPage(firstItem.Parts[0]);
		var second = _repository.GetPage(secondItem.Parts[0]);
		var parent = AddArticle(first.ParentResourceID, "Parent section", "docs#parent", 3);
		parent.CMSProperties.PageType = CMSPageType.Section;
		foreach (var page in new[] { first, second }) {
			if (nativeChild)
				page.ParentResourceID = parent.ID;
			AddSourcePage(page);
		}
		var caption = $"embedded_page_themes:{Guid.Parse(first.ID):D}";
		SetInternal(firstItem, (caption, "link_button, link_button,,"));
		secondItem.InternalID = firstItem.InternalID;
		var builder = new CmsRenderModelBuilder(_repository, Path.Combine(_fixtureRoot, "cms", "export"));
		var firstDocument = builder.Build(firstItem);
		var secondDocument = builder.Build(secondItem);
		Assert.That(firstDocument.Themes, Is.EqualTo(nativeChild ? new[] { "cms", "link_button" } : new[] { "cms" }));
		Assert.That(secondDocument.Themes, Is.EqualTo(new[] { "cms" }), "Sharing an Internal source must not share another child's scoped Theme selection.");
		Assert.That(firstDocument.Tokens[caption], Is.EqualTo("link_button, link_button,,"), "The authored setting remains intact.");
	}

	[Test]
	public async Task CommonAndPageScopedInternalModifiersPreserveOrderAndNormalizePageIDs([Values("D", "N")] string pageIdFormat) {
		await CreateDocument(LocalNotionMode.Online, "/");
		var item = _repository.GetCMSItem("docs/first");
		var parent = AddArticle(_repository.CMSDatabaseID, "Parent section", "docs#parent", 3);
		parent.CMSProperties.PageType = CMSPageType.Section;
		var page = AddArticle(parent.ID, "Native child", "docs/native", 4, pageIdFormat);
		page.CMSProperties.Themes = ["h2_color_primary"];
		item.Parts = [page.ID];
		AddSourcePage(page);
		var caption = $"embedded_page_themes:{Guid.Parse(page.ID):D}";
		SetInternal(item, ("embedded_page_themes", "h3_typed,h3_color_aux,h3_typed"), (caption, "h3_color_aux, link_button, h3_typed,,link_button"));
		var document = new CmsRenderModelBuilder(_repository, Path.Combine(_fixtureRoot, "cms", "export")).Build(item);
		Assert.That(document.Themes, Is.EqualTo(new[] { "cms", "h2_color_primary", "h3_typed", "h3_color_aux", "link_button" }));
		Assert.That(document.Tokens[caption], Is.EqualTo("h3_color_aux, link_button, h3_typed,,link_button"));
	}

	[TestCase("@HideEmbeddedPages", true)]
	[TestCase("HideEmbeddedPages", false)]
	[TestCase("hide_embedded_pages", false)]
	public async Task HideEmbeddedPagesTagAppliesToSectionWithoutHidingSectionContent(string tagName, bool hidesEmbeddedPages) {
		await CreateDocument(LocalNotionMode.Online, "/");
		var item = _repository.GetCMSItem("docs/first");
		var page = _repository.GetPage(item.Parts[0]);
		page.CMSProperties.PageType = CMSPageType.Section;
		page.CMSProperties.Tags = [tagName];
		var source = new Page { Id = page.ID };
		_repository.AddObject(source);
		_repository.AddResourceGraph(new NotionObjectGraph { ObjectID = source.Id });
		item.ItemType = CMSItemType.SectionedPage;
		var document = new CmsRenderModelBuilder(_repository, Path.Combine(_fixtureRoot, "cms", "export")).Build(item);
		var section = (R.DocumentBlock)document.Children.Single();
		Assert.That(section.HideEmbeddedPages, Is.EqualTo(hidesEmbeddedPages));
		Assert.That(section.Themes, Does.Not.Contain(Constants.TagHideEmbeddedPages));
		Assert.That(section.Themes, Does.Contain("cms_section"));
		Assert.That(section.RenderFrame, Is.False);
		Assert.That(document.RenderFrame, Is.True);
	}

	[TestCase("@HideEmbeddedPages")]
	[TestCase("HideEmbeddedPages")]
	[TestCase("hide_embedded_pages")]
	public async Task ThemeSelectionDoesNotActAsAControlTag(string themeName) {
		await CreateDocument(LocalNotionMode.Online, "/");
		var item = _repository.GetCMSItem("docs/first");
		var page = _repository.GetPage(item.Parts[0]);
		page.CMSProperties.Themes = [themeName];
		page.CMSProperties.Tags = [];
		_repository.AddObject(new Page { Id = page.ID });
		_repository.AddResourceGraph(new NotionObjectGraph { ObjectID = page.ID });
		var document = new CmsRenderModelBuilder(_repository, Path.Combine(_fixtureRoot, "cms", "export")).Build(item);
		Assert.That(document.HideEmbeddedPages, Is.False, "A Theme name must not be interpreted as a behavior Tag; old selections need explicit CMS migration.");
		Assert.That(document.Themes, Does.Contain(themeName), "Projection preserves authored Theme names without silently moving them between fields.");
	}

	[Test]
	public async Task ArticleProjectionPreservesSlugAndProvidesResolvedUrl(
		[Values("articles_category", "articles_category_active", "articles_summary", "articles_summary_alt")] string templateName,
		[Values("/", "https://example.test/content/")] string baseUrl
	) {
		var document = await CreateDocument(LocalNotionMode.Online, baseUrl);
		var template = Templates(document).Single(block => block.Template == templateName);
		var expectedSlug = ExpectedSlug(templateName);
		Assert.That(template.Tokens["slug"], Is.EqualTo(expectedSlug), "Legacy themes prepend '/' to this slug.");
		Assert.That(template.Tokens["url"], Is.EqualTo(baseUrl.TrimEnd('/') + "/" + expectedSlug), "Resolved URLs retain the configured hosting prefix.");
	}

	[Test]
	public async Task LegacyArticleOverridesKeepRootRelativeSlugLinks(
		[Values("articles_category", "articles_category_active", "articles_summary", "articles_summary_alt")] string templateName,
		[Values("/", "https://example.test/content/")] string baseUrl
	) {
		var document = await CreateDocument(LocalNotionMode.Online, baseUrl);
		var template = Templates(document).Single(block => block.Template == templateName);
		var themesDirectory = Path.Combine(_fixtureRoot, "overrides");
		var themeDirectory = Path.Combine(themesDirectory, "cms_articles");
		Directory.CreateDirectory(themeDirectory);
		// This is the pre-extraction theme's slug contract, including its explicit slash.
		var templatePath = Path.Combine(themeDirectory, templateName + ".html");
		const string legacyTemplate = "<a href=\"/{slug}\">{title}</a>";
		await File.WriteAllTextAsync(templatePath, legacyTemplate);
		var renderer = new R.HtmlRenderer(new R.ThemeCatalog(new R.ThemeOptions { ThemesDirectory = themesDirectory }));
		var html = renderer.Render(Fragment(template), new R.RenderOptions { Environment = R.RenderEnvironment.Online }).Html;
		var links = new HtmlParser().ParseDocument(html).QuerySelectorAll("a[href]");
		Assert.That(links, Has.Length.EqualTo(1));
		Assert.That(links[0].GetAttribute("href"), Is.EqualTo("/" + ExpectedSlug(templateName)));
		Assert.That(await File.ReadAllTextAsync(templatePath), Is.EqualTo(legacyTemplate), "Existing template files must remain unchanged.");
	}

	[TestCase(null, "Local Notion")]
	[TestCase("", "Local Notion")]
	[TestCase(" ", "Local Notion")]
	[TestCase("\t\r\n", "Local Notion")]
	[TestCase("Sphere10", "Sphere10")]
	[TestCase("  Named & Co.  ", "  Named & Co.  ")]
	public async Task AuthorUsesLegacyDefaultOnlyWhenMissing(string author, string expectedAuthor) {
		var document = await CreateDocument(LocalNotionMode.Online, "/", author);
		Assert.That(document.Author, Is.EqualTo(expectedAuthor));
		var html = new R.HtmlRenderer().Render(document, new R.RenderOptions { Environment = R.RenderEnvironment.Online }).Html;
		var parsed = new HtmlParser().ParseDocument(html);
		Assert.That(parsed.QuerySelector("meta[name=author]")?.GetAttribute("content"), Is.EqualTo(expectedAuthor));
		Assert.That(_repository.GetCMSItem("docs").Author, Is.EqualTo(author), "Rendering defaults must not mutate the stored author.");
	}

	[TestCaseSource(nameof(EmbeddedLinkCases))]
	public async Task EmbeddedArticleTemplatesUseResolvedUrls(string templateName, LocalNotionMode mode, string baseUrl, string expectedUrl) {
		var document = await CreateDocument(mode, baseUrl);
		var template = Templates(document).Single(block => block.Template == templateName);
		var options = new R.RenderOptions {
			Environment = mode == LocalNotionMode.Online ? R.RenderEnvironment.Online : R.RenderEnvironment.Offline
		};
		var html = new R.HtmlRenderer().Render(Fragment(template), options).Html;
		var links = new HtmlParser().ParseDocument(html).QuerySelectorAll("a[href]");
		Assert.That(links, Has.Length.EqualTo(1));
		Assert.That(links.Select(link => link.GetAttribute("href")), Is.All.EqualTo(expectedUrl));
	}

	private async Task<R.DocumentBlock> CreateDocument(LocalNotionMode mode, string baseUrl, string author = null) {
		var databaseId = Guid.NewGuid().ToString();
		_repository = (CMSLocalNotionRepository)await LocalNotionRepository.CreateNew(
			_fixtureRoot, cmsDatabaseID: databaseId,
			pathProfile: new LocalNotionPathProfile { Mode = mode, BaseUrl = baseUrl }, logger: new NoOpLogger()
		);
		_repository.AddObject(new Database { Id = databaseId });
		_repository.AddResourceGraph(new NotionObjectGraph { ObjectID = databaseId });
		_repository.AddResource(new LocalNotionDatabase { ID = databaseId, Name = "cms", Title = "CMS", PrimaryDataSourceID = Guid.NewGuid().ToString("N") });
		var first = AddArticle(databaseId, "First article", "docs/first", 1);
		var second = AddArticle(databaseId, "Second article", "docs/second", 2);
		var items = new[] {
			new CMSItem { ItemType = CMSItemType.CategoryPage, Slug = "", Title = "All articles", Parts = [first.ID, second.ID], RenderPath = "cms/export/index.html" },
			new CMSItem { ItemType = CMSItemType.CategoryPage, Slug = "docs", Title = "Docs", Parts = [first.ID, second.ID], RenderPath = "cms/export/docs.html" },
			new CMSItem { ItemType = CMSItemType.Page, Slug = "docs/first", Title = first.Title, Parts = [first.ID], RenderPath = "cms/export/first-article.html" },
			new CMSItem { ItemType = CMSItemType.Page, Slug = "docs/second", Title = second.Title, Parts = [second.ID], RenderPath = "cms/export/second-article.html" }
		};
		items[1].Author = author;
		foreach (var item in items)
			_repository.AddOrUpdateCMSItem(item);
		return new CmsRenderModelBuilder(_repository, Path.Combine(_fixtureRoot, "cms", "export")).Build(items[1]);
	}

	private void AddSourcePage(LocalNotionPage page) {
		_repository.AddObject(new Page { Id = page.ID });
		_repository.AddResourceGraph(new NotionObjectGraph { ObjectID = page.ID });
	}

	private void SetInternal(CMSItem item, params (string Caption, string Themes)[] settings) {
		var internalId = Guid.NewGuid().ToString("D");
		var blocks = settings.Select(setting => new global::Notion.Client.CodeBlock {
			Id = Guid.NewGuid().ToString("D"),
			Code = new() { Language = "plain text", Caption = [Run(setting.Caption)], RichText = [Run(setting.Themes)] }
		}).ToArray();
		foreach (var block in blocks)
			_repository.AddObject(block);
		_repository.AddResourceGraph(new NotionObjectGraph { ObjectID = internalId, Children = blocks.Select(block => new NotionObjectGraph { ObjectID = block.Id }).ToArray() });
		item.InternalID = internalId;
	}

	private LocalNotionPage AddArticle(string databaseId, string title, string slug, int sequence, string idFormat = "D") {
		var pageId = Guid.NewGuid().ToString(idFormat);
		var page = new LocalNotionPage {
			ID = pageId, Name = title, Title = title, ParentResourceID = databaseId, Keywords = [],
			CreatedOn = new DateTime(2026, 1, 1), LastEditedOn = new DateTime(2026, 1, 2)
		};
		_repository.AddResource(page);
		page.CMSProperties = new CMSProperties {
			PageType = CMSPageType.Page, CustomSlug = slug, Root = "Docs", Category1 = "Articles",
			Status = CMSPageStatus.Published, Themes = [], Tags = [], Summary = "Article summary", Sequence = sequence
		};
		return page;
	}

	private static R.DocumentBlock Fragment(R.TemplateBlock template) => new() {
		RenderFrame = false, ShowPageHeader = false, Themes = ["cms_articles"], Children = [template]
	};

	private static RichTextText Run(string text) => new() { Text = new() { Content = text }, PlainText = text };

	private static IEnumerable<R.TemplateBlock> Templates(R.VisualNode node) {
		if (node is R.TemplateBlock template) {
			yield return template;
			foreach (var nested in template.Slots.Values.SelectMany(Templates))
				yield return nested;
		}
		var children = node switch {
			R.DocumentBlock document => document.Children,
			R.GroupBlock group => group.Children,
			_ => Array.Empty<R.VisualNode>()
		};
		foreach (var nested in children.SelectMany(Templates))
			yield return nested;
	}

	private static string ExpectedSlug(string templateName) => templateName switch {
		"articles_category" => "",
		"articles_category_active" => "docs",
		"articles_summary" => "docs/first",
		"articles_summary_alt" => "docs/second",
		_ => throw new ArgumentOutOfRangeException(nameof(templateName))
	};

	private static IEnumerable<TestCaseData> EmbeddedLinkCases() {
		foreach (var (templateName, slug, file) in new[] {
			("articles_category", "", "index.html"),
			("articles_category_active", "docs", "docs.html"),
			("articles_summary", "docs/first", "first-article.html"),
			("articles_summary_alt", "docs/second", "second-article.html")
		}) {
			yield return new TestCaseData(templateName, LocalNotionMode.Online, "/", "/" + slug);
			yield return new TestCaseData(templateName, LocalNotionMode.Online, "https://example.test/content/", "https://example.test/content/" + slug);
			yield return new TestCaseData(templateName, LocalNotionMode.Offline, "", file);
		}
	}
}
