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
using System.Threading;
using System.Threading.Tasks;
using AngleSharp.Html.Parser;
using LocalNotion.Core;
using Sphere10.Framework.Web.VisualRenderer;
using Notion.Client;
using NUnit.Framework;
using Sphere10.Framework;
using CoreMode = LocalNotion.Core.RenderMode;
using HtmlRendererEngine = Sphere10.Framework.Web.VisualRenderer.HtmlRenderer;
using R = Sphere10.Framework.Web.VisualRenderer;

namespace LocalNotion.Core.Tests;

[TestFixture]
[Category("Integration")]
[FixtureLifeCycle(LifeCycle.InstancePerTestCase)]
[Parallelizable(ParallelScope.Children)]
public class RenderingIntegrationTests {
	private string _fixtureRoot = null;

	[SetUp]
	public void SetUp() {
		_fixtureRoot = Path.Combine(Path.GetTempPath(), "localnotion-rendering-" + Guid.NewGuid().ToString("N"));
		Directory.CreateDirectory(_fixtureRoot);
	}

	[TearDown]
	public void TearDown() {
		if (!Directory.Exists(_fixtureRoot))
			return;
		var expectedParent = Path.GetFullPath(Path.GetTempPath()).TrimEnd(Path.DirectorySeparatorChar);
		Assert.That(Path.GetDirectoryName(Path.GetFullPath(_fixtureRoot)), Is.EqualTo(expectedParent), "Only the isolated fixture directory may be removed.");
		Assert.That(Path.GetFileName(_fixtureRoot), Does.StartWith("localnotion-rendering-"));
		Directory.Delete(_fixtureRoot, recursive: true);
	}

	[Test]
	public async Task FormatsHeadWithoutChangingItsMetadataOrAssetOrder() {
		const string source = "<!DOCTYPE html><html><head><meta charset='utf-8'><title>A &amp; B</title><link rel='stylesheet' href='/motion.css'><script src='/app.js'></script></head><body><p>Body</p></body></html>";
		var formatted = await FormatCmsDocument(source);
		var document = new HtmlParser().ParseDocument(formatted);
		Assert.That(formatted, Does.Contain("\n\t<head>\n\t\t<meta"));
		Assert.That(document.Title, Is.EqualTo("A & B"));
		Assert.That(document.Head.Children[2].GetAttribute("href"), Is.EqualTo("/motion.css"));
		Assert.That(document.Head.Children[3].GetAttribute("src"), Is.EqualTo("/app.js"));
	}

	[Test]
	public async Task PreservesCodeDiagramsRawTextAndInlineSpacing() {
		const string source = """
			<!DOCTYPE html><html><head><style>.x::before { content: "a  b"; }
			.x { white-space: pre-wrap; }</style><script>const x = "a  b";
			if (1 < 2) { console.log(x); }</script></head><body>
			<p>Use <code>ObjectSpace</code> with <strong>care</strong>.</p>
			<pre><code>if (x &lt; y) {
			  Save("a  b");
			}</code></pre><pre class="ln-mermaid-source">flowchart LR
			  A["a  b"] --&gt; B
			</pre><textarea>  first
			  second</textarea></body></html>
			""";
		var parser = new HtmlParser();
		var before = parser.ParseDocument(source);
		var after = parser.ParseDocument(await FormatCmsDocument(source));
		foreach (var selector in new[] { "style", "script", "p", "p code", "pre code", ".ln-mermaid-source", "textarea" })
			Assert.That(after.QuerySelector(selector).TextContent, Is.EqualTo(before.QuerySelector(selector).TextContent), selector);
	}

	[Test]
	public async Task Repository_RendersWithDeployedThemesAndOverrides([Values] bool objectFolders) {
		var rootValue = NewDirectory("repository-" + objectFolders);
		var profile = new LocalNotionPathProfile { UsePageIDFolders = objectFolders, UseDatabaseIDFolders = objectFolders };
		R.DocumentBlock detached;
		string htmlValue;
		using (var repository = await LocalNotionRepository.CreateNew(rootValue, pathProfile: profile)) {
			var themesValue = repository.Paths.GetInternalResourceFolderPath(InternalResourceType.Themes, FileSystemPathType.Absolute);
			Assert.That(File.Exists(Path.Combine(themesValue, "default", "paragraph.html")), Is.True, "Repository creation deploys built-in themes.");
			var first = AddPage(repository, "Identical title", [Paragraph("Hello <world> {contents}")]);
			var second = AddPage(repository, "Identical title", [Paragraph("Second page")]);
			global::Notion.Client.ParagraphBlock linkValue = Paragraph("Forward target", "/p/" + second.ID.Replace("-", "") + "#target-anchor");
			Append(repository, first.ID, linkValue);
			var fileValue = new LocalNotionFile { ID = ID(), Title = "pixel.png", ParentResourceID = first.ID };
			repository.AddResource(fileValue);
			var imageSource = Path.Combine(_fixtureRoot, ID() + ".png");
			File.WriteAllBytes(imageSource, Convert.FromBase64String("iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVQIHWP4z8DwHwAFgAI/ScLbtAAAAABJRU5ErkJggg=="));
			repository.ImportResourceRender(fileValue.ID, RenderType.File, imageSource);
			var imageValue = new ImageBlock {
				Id = ID(),
				Parent = new PageParent { PageId = first.ID },
				Image = new UploadedFile {
					File = new UploadedFile.Info { Url = LocalNotionRenderLink.GenerateUrl(fileValue.ID, RenderType.File) },
					Caption = [Text("Image caption")]
				}
			};
			Append(repository, first.ID, imageValue);
			var manager = new RenderingManager(repository);
			manager.PrepareRenderPaths([first.ID, second.ID]);
			var firstReserved = first.Renders[RenderType.HTML].LocalPath;
			var secondReserved = second.Renders[RenderType.HTML].LocalPath;
			Assert.That(firstReserved, Is.Not.EqualTo(secondReserved), "same-title render destinations differ");
			var firstPath = manager.RenderLocalResource(first.ID, RenderType.HTML, CoreMode.ReadOnly);
			var secondPath = manager.RenderLocalResource(second.ID, RenderType.HTML, CoreMode.Editable);
			Assert.That(first.Renders[RenderType.HTML].LocalPath, Is.EqualTo(firstReserved), "first destination remains reserved path");
			Assert.That(second.Renders[RenderType.HTML].LocalPath, Is.EqualTo(secondReserved), "second destination remains reserved path");
			htmlValue = File.ReadAllText(firstPath);
			var parsed = new HtmlParser().ParseDocument(htmlValue);
			Assert.That(parsed.Body.TextContent, Does.Contain("Hello <world> {contents}"), "source text encoded once and braces preserved");
			var target = parsed.QuerySelectorAll("a").Single(element => element.TextContent.Contains("Forward target")).GetAttribute("href");
			var expected = Path.GetRelativePath(Path.GetDirectoryName(firstPath), secondPath).Replace('\\', '/') + "#target-anchor";
			Assert.That(target, Is.EqualTo(expected), "forward link resolves to collision-safe filename and anchor");
			Assert.That(parsed.QuerySelectorAll("img").Any(element => element.GetAttribute("src")?.Contains("pixel.png") == true), Is.True, "uploaded file handle projected to asset URL");
			Assert.That(htmlValue, Does.Not.Contain("localnotion://"), "no unresolved source handles in HTML");
			ValidateLocalAssets(firstPath);
			Assert.That(Directory.Exists(themesValue), Is.True, "Repository themes remain available to all render types.");
			var repeated = manager.RenderLocalResource(first.ID, RenderType.HTML, CoreMode.ReadOnly);
			Assert.That(File.ReadAllText(repeated), Is.EqualTo(htmlValue), "repeated render is deterministic");
			detached = new NotionRenderModelBuilder(repository).Build(first.ID, Path.GetDirectoryName(firstPath));
			var sourceBefore = Tools.Json.WriteToString(repository.GetObject(first.ID));
			_ = new HtmlRendererEngine().Render(detached);
			Assert.That(Tools.Json.WriteToString(repository.GetObject(first.ID)), Is.EqualTo(sourceBefore), "projection/render leaves source object unchanged");
			var overrideDirectory = Path.Combine(themesValue, "default");
			Directory.CreateDirectory(overrideDirectory);
			var overrideFile = Path.Combine(overrideDirectory, "paragraph.html");
			File.WriteAllText(overrideFile, "<p class=\"fixture-override\">{contents}{children}</p>");
			manager.RenderLocalResource(first.ID, RenderType.HTML, CoreMode.ReadOnly);
			Assert.That(File.ReadAllText(firstPath), Does.Contain("fixture-override"), "single file override used without disk config");
			File.Delete(overrideFile);
			manager.RenderLocalResource(first.ID, RenderType.HTML, CoreMode.ReadOnly);
			Assert.That(File.ReadAllText(firstPath), Does.Not.Contain("fixture-override"), "removed override exposes embedded template on next render");
			Assert.That(File.Exists(overrideFile), Is.False, "Rendering uses the embedded fallback without redeploying themes for each page.");
			await repository.SaveAsync();
		}
		var detachedResult = new HtmlRendererEngine().Render(detached);
		Assert.That(detachedResult.Html, Does.Contain("Forward target"), "model renders after repository disposed");
		using var reopened = await LocalNotionRepository.Open(rootValue);
		Assert.That(File.Exists(Path.Combine(rootValue, ".localnotion", "themes", "default", "paragraph.html")), Is.True, "Reopening restores missing built-in files.");
	}

	[Test]
	public async Task ResourceRendering_PreservesTemplateWhitespace() {
		using var repository = await LocalNotionRepository.CreateNew(NewDirectory("template-whitespace"));
		var page = AddPage(repository, "Whitespace", [Paragraph("Content")]);
		var themeDirectory = Path.Combine(repository.Paths.GetInternalResourceFolderPath(InternalResourceType.Themes, FileSystemPathType.Absolute), "default");
		Directory.CreateDirectory(themeDirectory);
		const string template = "<p style=\"white-space: pre-wrap\">before\n  {contents}\n  after</p>";
		File.WriteAllText(Path.Combine(themeDirectory, "paragraph.html"), template);
		var output = new RenderingManager(repository).RenderLocalResource(page.ID, RenderType.HTML, CoreMode.ReadOnly);
		Assert.That(File.ReadAllText(output), Does.Contain("<p style=\"white-space: pre-wrap\">before\n  Content\n  after</p>"),
			"Source rendering must preserve template whitespace, as the original rendering manager did.");
	}

	[Test]
	public async Task CmsRenderingRespectsCleanHtmlBuildSetting([Values] bool suppressFormatting) {
		using var repository = await CreateCmsRepository("clean-html");
		var page = AddPage(repository, "Formatting", [Paragraph("Body")], repository.CMSDatabaseID);
		page.CMSProperties = new CMSProperties { PageType = CMSPageType.Page, CustomSlug = "formatting", Status = CMSPageStatus.Published, Themes = [], Tags = [] };
		var item = new CMSItem { ItemType = CMSItemType.Page, Slug = "formatting", Title = "Formatting", Parts = [page.ID] };
		repository.AddOrUpdateCMSItem(item);
		var themes = repository.Paths.GetInternalResourceFolderPath(InternalResourceType.Themes, FileSystemPathType.Absolute);
		var cmsTheme = Path.Combine(themes, "cms");
		var readonlyTemplates = Path.Combine(cmsTheme, "readonly");
		Directory.CreateDirectory(readonlyTemplates);
		const string rawMarkup = "<p data-test='raw'>\n   Raw  text\n</p><table><tr><td>x</td></tr></table>";
		File.WriteAllText(Path.Combine(readonlyTemplates, "page.offline.html"), "<!DOCTYPE html><html><head><title>Formatting</title></head><body>" + rawMarkup + "{contents}</body></html>");
		if (suppressFormatting)
			File.WriteAllText(Path.Combine(cmsTheme, ".config.json"), """{"type":"html","base":"default","traits":"suppress_formatting"}""");
		new RenderingManager(repository).RenderCMSItem(item);
		var html = File.ReadAllText(Path.GetFullPath(item.RenderPath, repository.Paths.GetRepositoryPath(FileSystemPathType.Absolute)));
		if (!suppressFormatting) {
			Assert.That(html, Does.Contain("<tbody>").And.Contain("data-test=\"raw\""), "The optional formatter parses and normalizes complete CMS output.");
			return;
		}
		Assert.That(html, Does.Contain(rawMarkup).And.Not.Contain("<tbody>"), "Without the cleaning pass, generated markup must remain untouched.");
	}

	[TestCase(CMSPageStatus.Draft, false)]
	[TestCase(CMSPageStatus.QA, false)]
	[TestCase(CMSPageStatus.Hidden, false)]
	[TestCase(CMSPageStatus.Published, true)]
	public async Task UnpublishedCmsPageStillRendersItsSource(CMSPageStatus status, bool scheduled) {
		using var repository = await CreateCmsRepository("unpublished");
		var page = AddPage(repository, "Untitled", [Paragraph("Draft source content")], repository.CMSDatabaseID);
		page.CMSProperties = new CMSProperties {
			PageType = CMSPageType.Page,
			CustomSlug = "drafts/untitled",
			Status = status,
			PublishOn = scheduled ? DateTimeOffset.UtcNow.AddDays(1) : null,
			Themes = [],
			Tags = []
		};
		var manager = new RenderingManager(repository);
		Assert.That(() => manager.PrepareRenderPaths([page.ID]), Throws.Nothing);
		var path = manager.RenderLocalResource(page.ID, RenderType.HTML, CoreMode.ReadOnly);
		Assert.That(File.ReadAllText(path), Does.Contain("Draft source content"));
		Assert.That(repository.CMSItems.Any(item => item.Parts.Contains(page.ID)), Is.False, "Source rendering must not publish a draft or scheduled CMS page.");
	}

	[Test]
	public async Task UnpublishingCmsPageRemovesItsPublishedOutputAndKeepsItsSourceRenderable() {
		using var repository = await CreateCmsRepository("unpublishing");
		var page = AddPage(repository, "Article", [Paragraph("Article source content")], repository.CMSDatabaseID);
		page.CMSProperties = new CMSProperties { PageType = CMSPageType.Page, CustomSlug = "docs/article", Status = CMSPageStatus.Published, Themes = [], Tags = [] };
		var manager = new RenderingManager(repository);
		manager.RenderLocalResource(page.ID, RenderType.HTML, CoreMode.ReadOnly);
		Assert.That(repository.ContainsCmsItem("docs/article"), Is.True);
		var item = repository.CMSItems.Single(item => item.Slug == "docs/article");
		manager.RenderCMSItem(item);
		var publishedPath = Path.GetFullPath(item.RenderPath, repository.Paths.GetRepositoryPath(FileSystemPathType.Absolute));
		Assert.That(File.Exists(publishedPath), Is.True);

		page.CMSProperties.Status = CMSPageStatus.Hidden;
		var sourcePath = manager.RenderLocalResource(page.ID, RenderType.HTML, CoreMode.ReadOnly);
		Assert.That(File.ReadAllText(sourcePath), Does.Contain("Article source content"));
		Assert.That(repository.CMSItems.Any(cmsItem => cmsItem.Parts.Contains(page.ID)), Is.False);
		Assert.That(File.Exists(publishedPath), Is.False, "A withdrawn CMS page must not retain its published HTML.");
	}

	[Test]
	public async Task Cms_RendersCompositionAndTracksRenamedOutputs() {
		var rootValue = NewDirectory("cms");
		var databaseId = ID();
		using var repository = (CMSLocalNotionRepository)await LocalNotionRepository.CreateNew(rootValue, cmsDatabaseID: databaseId);
		var databaseValue = new LocalNotionDatabase { ID = databaseId, PrimaryDataSourceID = ID(), Name = "cms-db", Title = "CMS", Keywords = [] };
		repository.AddObject(new Database { Id = databaseId, Title = [Text("CMS")], Description = [], Parent = new WorkspaceParent() });
		repository.AddResourceGraph(new NotionObjectGraph { ObjectID = databaseId });
		repository.AddResource(databaseValue);
		// Set CMS properties after insertion to prepare an explicit synthetic content hierarchy without running the sync orchestrator.
		var home = AddPage(repository, "Home title", [Paragraph("Home body")], databaseId);
		var section1 = AddPage(repository, "Section one", [Paragraph("Section one body")], databaseId);
		var section2 = AddPage(repository, "Section two", [Paragraph("Section two body")], databaseId);
		var article = AddPage(repository, "Article title <em data-cms-untrusted>literal</em>", [Paragraph("Article body")], databaseId);
		article.Cover = "https://example.invalid/image'\\\r\n\f.png?x=1&y=2";
		var galleryValue = AddPage(repository, "Gallery title", [Paragraph("Gallery body")], databaseId);
		var hiddenValue = AddPage(repository, "Hidden gallery", [Paragraph("Hidden gallery body")], databaseId);
		var headerValue = AddPage(repository, "Header", [Paragraph("Header marker")], databaseId);
		var menu = AddPage(repository, "Menu", [Paragraph("Menu marker")], databaseId);
		var footerValue = AddPage(repository, "Footer", [Paragraph("Footer marker")], databaseId);
		var internalPage = AddPage(repository, "Internal", [new global::Notion.Client.CodeBlock { Id = ID(), Code = new global::Notion.Client.CodeBlock.Info {
			Language = "html", Caption = [Text("html_head_start")], RichText = [Text("<meta name=\"fixture-internal\" content=\"present\">")]
		} }], databaseId);
		var manager = new RenderingManager(repository);
		manager.PrepareRenderPaths(repository.Resources.OfType<LocalNotionEditableResource>().Select(resource => resource.ID));
		home.CMSProperties = Cms(CMSPageType.Page, "home", "Home");
		section1.CMSProperties = Cms(CMSPageType.Section, "sections#one", "Sections");
		section2.CMSProperties = Cms(CMSPageType.Section, "sections#two", "Sections");
		article.CMSProperties = Cms(CMSPageType.Page, "docs/article", "Docs");
		galleryValue.CMSProperties = Cms(CMSPageType.Gallery, "gallery/card", "Gallery");
		hiddenValue.CMSProperties = Cms(CMSPageType.Gallery, "gallery/hidden", "Gallery");
		hiddenValue.CMSProperties.Status = CMSPageStatus.Hidden;
		headerValue.CMSProperties = Cms(CMSPageType.Header, "framing/header", "Framing");
		menu.CMSProperties = Cms(CMSPageType.NavBar, "framing/menu", "Framing");
		footerValue.CMSProperties = Cms(CMSPageType.Footer, "framing/footer", "Framing");
		internalPage.CMSProperties = Cms(CMSPageType.Internal, "framing/internal", "Framing");
		var homeItem = Item(CMSItemType.Page, "home", "Home", [home.ID]);
		var sectionItem = Item(CMSItemType.SectionedPage, "sections", "Sections", [section1.ID, section2.ID]);
		var articleItem = Item(CMSItemType.Page, "docs/article", "Article", [article.ID]);
		var categoryItem = Item(CMSItemType.CategoryPage, "docs", "Docs", [article.ID]);
		var galleryItem = Item(CMSItemType.GalleryPage, "gallery", "Gallery", [galleryValue.ID]);
		var items = new[] { homeItem, sectionItem, articleItem, categoryItem, galleryItem };
		foreach (var itemValue in items) {
			itemValue.HeaderID = headerValue.ID;
			itemValue.MenuID = menu.ID;
			itemValue.FooterID = footerValue.ID;
			itemValue.InternalID = internalPage.ID;
			repository.AddOrUpdateCMSItem(itemValue);
		}
		manager.PrepareRenderPaths([], RenderType.HTML, items);
		foreach (var itemValue in items)
			manager.RenderCMSItem(itemValue);
		foreach (var itemValue in items) {
			var pathValue = Path.GetFullPath(itemValue.RenderPath, rootValue);
			var contentValue = File.ReadAllText(pathValue);
			Assert.That(contentValue, Does.Contain("Header marker").And.Contain("Menu marker").And.Contain("Footer marker"), "CMS framing projected for " + itemValue.ItemType);
			Assert.That(contentValue, Does.Contain("fixture-internal"), "CMS internal markup preserved for " + itemValue.ItemType);
			Assert.That(itemValue.Dirty, Is.False, "successful CMS render clears dirty flag");
			ValidateLocalAssets(pathValue);
		}
		string Read(CMSItem itemValue) => File.ReadAllText(Path.GetFullPath(itemValue.RenderPath, rootValue));
		Assert.That(Read(sectionItem), Does.Contain("Section one body").And.Contain("Section two body"), "sectioned page contains both fragments");
		Assert.That(Read(categoryItem), Does.Contain("Article title"), "category page contains article summary");
		Assert.That(Read(galleryItem), Does.Contain("Gallery title").And.Not.Contain("Hidden gallery"), "gallery contains only public cards");
		var categoryDocument = new HtmlParser().ParseDocument(Read(categoryItem));
		Assert.That(categoryDocument.QuerySelector("[data-cms-untrusted]"), Is.Null, "CMS source scalars cannot inject markup");
		Assert.That(categoryDocument.Body.TextContent.Contains("Article title <em data-cms-untrusted>literal</em>") && categoryDocument.Body.TextContent.Contains("Summary marker <span data-cms-untrusted>literal & text</span> {contents}"), Is.True, "CMS source scalars preserve literal text exactly");
		Assert.That(categoryDocument.QuerySelectorAll("[style]").Any(element => element.GetAttribute("style").Contains("url('https://example.invalid/image%27%5C%0D%0A%0C.png?x=1&y=2')")), Is.True, "CMS feature URL preserves CSS string boundaries");
		var articleLinks = categoryDocument.QuerySelectorAll("a").Where(element => element.TextContent.Contains("Read more") || element.TextContent.Contains("Article title")).ToArray();
		Assert.That(articleLinks.Length > 0 && articleLinks.All(element => File.Exists(Path.GetFullPath(element.GetAttribute("href"), Path.GetDirectoryName(Path.GetFullPath(categoryItem.RenderPath, rootValue))))), Is.True, "CMS summary links resolve from final CMS directory");
		Assert.That(Directory.Exists(Path.Combine(rootValue, ".localnotion", "themes")), Is.True, "CMS repositories deploy their source themes.");
		await repository.SaveAsync();
		Assert.That(repository.RequiresSave, Is.False, "CMS render metadata persisted");
		// Resource-render imports replace CMS item instances after destination reservation.
		var currentHomePath = Path.GetFullPath(homeItem.RenderPath, rootValue);
		var previousHomePath = Path.Combine(Path.GetDirectoryName(currentHomePath), "previous-home.html");
		File.Move(currentHomePath, previousHomePath);
		homeItem.RenderPath = Path.GetRelativePath(rootValue, previousHomePath).Replace(Path.DirectorySeparatorChar, '/');
		homeItem.Title = "Renamed home";
		var renameManager = new RenderingManager(repository);
		renameManager.PrepareRenderPaths([], RenderType.HTML, [homeItem]);
		var renamedHomePath = Path.GetFullPath(homeItem.RenderPath, rootValue);
		Assert.That(renamedHomePath != previousHomePath && File.Exists(previousHomePath), Is.True, "CMS rename retains old output until success");
		renameManager.RenderLocalResource(home.ID, RenderType.HTML, CoreMode.ReadOnly);
		var freshHome = repository.GetCMSItem(homeItem.Slug);
		Assert.That(freshHome, Is.Not.SameAs(homeItem), "resource import replaces CMS item instance");
		renameManager.RenderCMSItem(freshHome);
		Assert.That(!File.Exists(previousHomePath) && File.ReadAllText(renamedHomePath).Contains("Home body"), Is.True, "CMS rename cleans old output across item replacement");
		await repository.SaveAsync();
		Assert.That(repository.RequiresSave, Is.False, "CMS rename metadata persisted");
		R.DocumentBlock detached = new CmsRenderModelBuilder(repository, Path.Combine(rootValue, "cms")).Build(sectionItem);
		repository.Dispose();
		Assert.That(new HtmlRendererEngine().Render(detached).Html, Does.Contain("Section two body"), "CMS visual document renders without repository");

		CMSItem Item(CMSItemType type, string slugValue, string titleValue, string[] partsValue) => new() { ItemType = type, Slug = slugValue, Title = titleValue, Parts = partsValue, Dirty = true };
		CMSProperties Cms(CMSPageType type, string slugValue, string group) => new() { PageType = type, CustomSlug = slugValue, Root = group, Category1 = "Example", Status = CMSPageStatus.Published, Themes = [], Tags = [], Summary = "Summary marker <span data-cms-untrusted>literal & text</span> {contents}" };
	}

	[Test]
	public async Task OnlineRendering_UsesResolvedUrlsAndConfiguredThemeCdn() {
		var rootValue = NewDirectory("online");
		const string baseUrlValue = "https://example.invalid/site";
		using var repository = await LocalNotionRepository.CreateNew(rootValue, pathProfile: new LocalNotionPathProfile { Mode = LocalNotionMode.Online, BaseUrl = baseUrlValue });
		var first = AddPage(repository, "Hosted source", [Paragraph("Hosted body")]);
		var second = AddPage(repository, "Hosted target", [Paragraph("Hosted destination")]);
		second.CMSProperties = new CMSProperties { PageType = CMSPageType.Page, CustomSlug = "guides/custom-target", Status = CMSPageStatus.Published };
		Append(repository, first.ID, Paragraph("Hosted link", "/p/" + second.ID.Replace("-", "") + "#target-anchor"));
		var manager = new RenderingManager(repository);
		manager.PrepareRenderPaths([first.ID, second.ID]);
		var htmlPath = manager.RenderLocalResource(first.ID, RenderType.HTML, CoreMode.ReadOnly);
		var parsed = new HtmlParser().ParseDocument(File.ReadAllText(htmlPath));
		Assert.That(parsed.QuerySelectorAll("a").Single(element => element.TextContent.Contains("Hosted link")).GetAttribute("href") == baseUrlValue + "/guides/custom-target#target-anchor", Is.True, "hosted links resolve custom slugs and anchors before rendering");
		Assert.That(parsed.QuerySelectorAll("script[src]").Any(element => element.GetAttribute("src").StartsWith("https://cdn.jsdelivr.net/", StringComparison.Ordinal)), Is.True, "Online assets retain their configured CDN even with deployed themes.");
		Assert.That(Directory.Exists(Path.Combine(rootValue, ".localnotion", "themes")), Is.True, "Hosted repositories also deploy their source themes.");
		var overrides = Path.Combine(rootValue, ".localnotion", "themes", "default", "resources", "local-notion", "js");
		Directory.CreateDirectory(overrides);
		const string overrideScript = "window.rendererIntegrationOverride = true;";
		File.WriteAllText(Path.Combine(overrides, "ln.js"), overrideScript);
		htmlPath = manager.RenderLocalResource(first.ID, RenderType.HTML, CoreMode.ReadOnly);
		parsed = new HtmlParser().ParseDocument(File.ReadAllText(htmlPath));
		var overriddenScript = parsed.QuerySelectorAll("script[src]").Single(element => element.GetAttribute("src").EndsWith("/local-notion/js/ln.js", StringComparison.Ordinal));
		Assert.That(overriddenScript.GetAttribute("src"), Is.EqualTo("https://cdn.jsdelivr.net/gh/sphere10/cdn/local-notion/themes/default/resources/local-notion/js/ln.js"),
			"Changing a deployed asset must not discard the theme's configured online URL.");
		Assert.That(File.ReadAllText(htmlPath), Does.Not.Contain("/.localnotion/render-assets/"));

		var assetUrls = parsed.QuerySelectorAll("script[src],link[rel=stylesheet][href]")
			.Select(element => element.GetAttribute(element.LocalName == "script" ? "src" : "href")).ToArray();
		Assert.That(assetUrls, Has.None.Contains(".localnotion/render-assets/"), "Local theme files must not disable configured CDN URLs.");
		Assert.That(assetUrls, Does.Contain("https://cdn.jsdelivr.net/gh/sphere10/cdn/local-notion/themes/default/resources/local-notion/js/ln.js"));
		Assert.That(File.ReadAllText(Path.Combine(overrides, "ln.js")), Is.EqualTo(overrideScript), "Online rendering preserves the local file used by offline renders.");
	}

	[Test]
	public async Task MermaidAssetsArePublishedOutsidePrivateRepositoryFolders([Values] bool online, [Values] bool cms) {
		const string baseUrl = "https://example.invalid/site";
		const string source = "flowchart LR\n    Author --> Website";
		var profile = new LocalNotionPathProfile { Mode = online ? LocalNotionMode.Online : LocalNotionMode.Offline, BaseUrl = baseUrl };
		using var repository = cms
			? await CreateCmsRepository("mermaid-assets", pathProfile: profile)
			: await LocalNotionRepository.CreateNew(NewDirectory("mermaid-assets"), pathProfile: profile);
		var root = repository.Paths.GetRepositoryPath(FileSystemPathType.Absolute);
		var page = AddPage(repository, "Mermaid", [new global::Notion.Client.CodeBlock {
			Id = ID(),
			Code = new global::Notion.Client.CodeBlock.Info { Language = "mermaid", RichText = [Text(source)], Caption = [Text("Publication flow")] }
		}], cms ? ((CMSLocalNotionRepository)repository).CMSDatabaseID : null);
		var manager = new RenderingManager(repository);
		string htmlPath;
		if (cms) {
			page.CMSProperties = new CMSProperties { PageType = CMSPageType.Page, Status = CMSPageStatus.Published, Themes = [], Tags = [] };
			var item = new CMSItem { ItemType = CMSItemType.Page, Slug = "mermaid", Title = "Mermaid", Parts = [page.ID] };
			((CMSLocalNotionRepository)repository).AddOrUpdateCMSItem(item);
			manager.RenderCMSItem(item);
			htmlPath = Path.GetFullPath(item.RenderPath, root);
		} else {
			htmlPath = manager.RenderLocalResource(page.ID, RenderType.HTML, CoreMode.ReadOnly);
		}

		// The renderer's local runtime must remain reachable while the private repository tree stays denied.
		var document = new HtmlParser().ParseDocument(File.ReadAllText(htmlPath));
		Assert.That(document.QuerySelector("[data-ln-mermaid] code").TextContent, Is.EqualTo(source));
		Assert.That(manager.AssetsDirectory, Is.EqualTo(Path.Combine(root, "render-assets")));
		Assert.That(Directory.Exists(Path.Combine(root, ".localnotion", "render-assets")), Is.False);
		var urls = document.QuerySelectorAll("script[src],link[rel=stylesheet][href]")
			.Select(element => element.GetAttribute(element.LocalName == "script" ? "src" : "href"))
			.Where(url => url.Contains("/resources/mermaid/", StringComparison.Ordinal)).ToArray();
		Assert.That(urls, Has.Length.EqualTo(3), "The document loads the stylesheet, pinned runtime and initializer.");
		foreach (var url in urls) {
			Assert.That(url, Does.Not.Contain(".localnotion"));
			string assetPath;
			if (online) {
				Assert.That(url, Does.StartWith(baseUrl + "/render-assets/"));
				assetPath = Path.GetFullPath(Uri.UnescapeDataString(url[(baseUrl.Length + 1)..]), root);
			} else {
				Assert.That(Uri.TryCreate(url, UriKind.Absolute, out _), Is.False);
				assetPath = Path.GetFullPath(Uri.UnescapeDataString(url), Path.GetDirectoryName(htmlPath));
			}
			Assert.That(Path.GetRelativePath(manager.AssetsDirectory, assetPath), Does.Not.StartWith(".."));
			var deployedSource = Path.Combine(repository.Paths.GetInternalResourceFolderPath(InternalResourceType.Themes, FileSystemPathType.Absolute), ".mermaid", "resources", "mermaid", Path.GetFileName(assetPath));
			Assert.That(File.ReadAllBytes(assetPath), Is.EqualTo(File.ReadAllBytes(deployedSource)), "Published runtime bytes match the deployed renderer resource.");
		}
	}

	[Test]
	public async Task Cancellation_PreventsOrStopsPathReservation([Values] bool cancelDuringReservation) {
		var rootValue = NewDirectory("cancellation");
		using var repository = await LocalNotionRepository.CreateNew(rootValue);
		var first = AddPage(repository, "First", [Paragraph("First page")]);
		var second = AddPage(repository, "Second", [Paragraph("Second page")]);
		var manager = new RenderingManager(repository);
		using var cancellation = new CancellationTokenSource();
		if (cancelDuringReservation) {
			IEnumerable<string> Batch() { yield return first.ID; cancellation.Cancel(); yield return second.ID; }
			Assert.That(
				() => manager.PrepareRenderPaths(Batch(), faultTolerant: true, cancellationTokenValue: cancellation.Token),
				Throws.InstanceOf<OperationCanceledException>(),
				"Fault tolerance must preserve cancellation during reservation."
			);
			Assert.That(first.Renders.ContainsKey(RenderType.HTML), Is.True, "The first destination was reserved before cancellation.");
			Assert.That(second.Renders, Is.Empty, "Reservation must stop before allocating the next destination.");
		} else {
			cancellation.Cancel();
			Assert.That(
				() => manager.PrepareRenderPaths([first.ID, second.ID], cancellationTokenValue: cancellation.Token),
				Throws.InstanceOf<OperationCanceledException>(),
				"An already-cancelled batch must propagate cancellation."
			);
			Assert.That(first.Renders, Is.Empty, "A cancelled batch must not create a first placeholder.");
			Assert.That(second.Renders, Is.Empty, "A cancelled batch must not create a second placeholder.");
		}
	}

	[Test]
	public async Task RenderBatchFreezesOverridesAndRefreshesAfterDisposal() {
		using var repository = await LocalNotionRepository.CreateNew(NewDirectory("theme-batch"));
		var page = AddPage(repository, "Batch page", [Paragraph("Body")]);
		var themes = repository.Paths.GetInternalResourceFolderPath(InternalResourceType.Themes, FileSystemPathType.Absolute);
		var template = Path.Combine(themes, "default", "paragraph.html");
		Directory.CreateDirectory(Path.GetDirectoryName(template));
		File.WriteAllText(template, "<p class=\"initial-override\">{contents}</p>");
		var manager = new RenderingManager(repository);
		using (manager.BeginBatch()) {
			var path = manager.RenderLocalResource(page.ID, RenderType.HTML, CoreMode.ReadOnly);
			Assert.That(File.ReadAllText(path), Does.Contain("initial-override"));
			File.WriteAllText(template, "<p class=\"changed-override\">{contents}</p>");
			path = manager.RenderLocalResource(page.ID, RenderType.HTML, CoreMode.ReadOnly);
			Assert.That(File.ReadAllText(path), Does.Contain("initial-override").And.Not.Contain("changed-override"));
		}
		var refreshed = manager.RenderLocalResource(page.ID, RenderType.HTML, CoreMode.ReadOnly);
		Assert.That(File.ReadAllText(refreshed), Does.Contain("changed-override"));
	}

	[Test]
	public async Task RenderBatchDoesNotRereadVerifiedAssetsAndNewBatchRestoresDeletedAssets() {
		using var repository = await LocalNotionRepository.CreateNew(NewDirectory("asset-batch"));
		var page = AddPage(repository, "Batch page", [Paragraph("Body")]);
		var manager = new RenderingManager(repository);
		string asset;
		byte[] original;
		using (manager.BeginBatch()) {
			manager.RenderLocalResource(page.ID, RenderType.HTML, CoreMode.ReadOnly);
			asset = Directory.EnumerateFiles(manager.AssetsDirectory, "ln.css", SearchOption.AllDirectories).Single();
			original = File.ReadAllBytes(asset);
			using (var lockedAsset = new FileStream(asset, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
				Assert.That(() => manager.RenderLocalResource(page.ID, RenderType.HTML, CoreMode.ReadOnly), Throws.Nothing);
			File.Delete(asset);
			manager.RenderLocalResource(page.ID, RenderType.HTML, CoreMode.ReadOnly);
			Assert.That(File.Exists(asset), Is.False, "Published assets are stable for the duration of a batch.");
		}
		using (manager.BeginBatch())
			manager.RenderLocalResource(page.ID, RenderType.HTML, CoreMode.ReadOnly);
		Assert.That(File.ReadAllBytes(asset), Is.EqualTo(original));
	}

	[Test]
	public async Task NewRenderBatchVerifiesPreviouslyPublishedAssetBytes() {
		using var repository = await LocalNotionRepository.CreateNew(NewDirectory("asset-verification"));
		var page = AddPage(repository, "Batch page", [Paragraph("Body")]);
		var manager = new RenderingManager(repository);
		using (manager.BeginBatch())
			manager.RenderLocalResource(page.ID, RenderType.HTML, CoreMode.ReadOnly);
		var asset = Directory.EnumerateFiles(manager.AssetsDirectory, "ln.css", SearchOption.AllDirectories).Single();
		var original = File.ReadAllBytes(asset);
		File.WriteAllText(asset, "modified asset");
		using (manager.BeginBatch())
			manager.RenderLocalResource(page.ID, RenderType.HTML, CoreMode.ReadOnly);
		Assert.That(File.ReadAllBytes(asset), Is.EqualTo(original));
	}

	[Test]
	public async Task NewRenderBatchRefreshesCmsDestinationsAndCleansPreviousOutput([Values] bool nested) {
		using var repository = await CreateCmsRepository("cms-batch-destinations");
		var page = AddPage(repository, "Article", [Paragraph("Article body")], repository.CMSDatabaseID);
		page.CMSProperties = new CMSProperties { PageType = CMSPageType.Page, CustomSlug = "docs/article", Status = CMSPageStatus.Published, Themes = [], Tags = [] };
		var item = new CMSItem { ItemType = CMSItemType.Page, Slug = "docs/article", Title = "Article", Parts = [page.ID], Dirty = true };
		repository.AddOrUpdateCMSItem(item);
		var manager = new RenderingManager(repository);
		using (manager.BeginBatch())
			manager.RenderCMSItem(item);
		var root = repository.Paths.GetRepositoryPath(FileSystemPathType.Absolute);
		var destination = Path.GetFullPath(item.RenderPath, root);
		var previous = Path.Combine(Path.GetDirectoryName(destination), "previous-article.html");
		File.Move(destination, previous);
		item.RenderPath = Path.GetRelativePath(root, previous).Replace('\\', '/');
		using (manager.BeginBatch()) {
			manager.PrepareRenderPaths([], cmsItems: [item]);
			Assert.That(Path.GetFullPath(item.RenderPath, root), Is.EqualTo(destination));
			Assert.That(File.Exists(previous), Is.True, "The previous output remains available until the replacement is rendered.");
			if (nested) {
				using (manager.BeginBatch())
					manager.PrepareRenderPaths([], cmsItems: [item]);
				Assert.That(Path.GetFullPath(item.RenderPath, root), Is.EqualTo(destination));
			}
			manager.RenderCMSItem(item);
		}
		Assert.That(File.ReadAllText(destination), Does.Contain("Article body"));
		Assert.That(File.Exists(previous), Is.False);
	}

	[Test]
	public async Task RenderBatchReusesSourceReadsAndRefreshesContent([Values] bool nested) {
		using var repository = await LocalNotionRepository.CreateNew(NewDirectory("source-batch"));
		var paragraph = Paragraph("Initial body");
		var page = AddPage(repository, "Source snapshot", [paragraph]);
		var counting = new ProjectionReadCountingRepository(repository);
		var manager = new RenderingManager(counting);
		using (manager.BeginBatch()) {
			manager.RenderLocalResource(page.ID, RenderType.HTML, CoreMode.ReadOnly);
			var objectReads = counting.ObjectReads;
			var graphReads = counting.GraphReads;
			Assert.That(objectReads, Is.GreaterThan(0));
			Assert.That(graphReads, Is.GreaterThan(0));
			paragraph.Paragraph.RichText = [Text("Changed body")];
			repository.UpdateObject(paragraph);
			Append(repository, page.ID, Paragraph("Appended body"));
			if (nested) {
				using (manager.BeginBatch())
					manager.RenderLocalResource(page.ID, RenderType.HTML, CoreMode.ReadOnly);
			}
			var path = manager.RenderLocalResource(page.ID, RenderType.HTML, CoreMode.ReadOnly);
			Assert.That(File.ReadAllText(path), Does.Contain("Initial body").And.Not.Contain("Changed body").And.Not.Contain("Appended body"));
			Assert.That(counting.ObjectReads, Is.EqualTo(objectReads), "Source objects are read once across renders and nested scopes.");
			Assert.That(counting.GraphReads, Is.EqualTo(graphReads), "The source graph is read once across renders and nested scopes.");
		}
		using (manager.BeginBatch()) {
			var path = manager.RenderLocalResource(page.ID, RenderType.HTML, CoreMode.ReadOnly);
			Assert.That(File.ReadAllText(path), Does.Contain("Changed body").And.Contain("Appended body").And.Not.Contain("Initial body"));
		}
	}

	[Test]
	public async Task RenderBatchSharesCmsSourceSnapshotAndReprojectsDestinationLinks() {
		using var repository = await CreateCmsRepository("cms-source-batch");
		var targetBlock = Paragraph("Target body");
		var target = AddPage(repository, "Target", [targetBlock], repository.CMSDatabaseID);
		var page = AddPage(repository, "Article", [Paragraph("Destination link", "/p/" + targetBlock.Id.Replace("-", ""))], repository.CMSDatabaseID);
		var headerBlock = Paragraph("Initial header");
		var header = AddPage(repository, "Header", [headerBlock], repository.CMSDatabaseID);
		page.CMSProperties = new CMSProperties { PageType = CMSPageType.Page, CustomSlug = "docs/article", Status = CMSPageStatus.Published, Themes = [], Tags = [] };
		header.CMSProperties = new CMSProperties { PageType = CMSPageType.Header, CustomSlug = "framing/header", Status = CMSPageStatus.Published, Themes = [], Tags = [] };
		var item = new CMSItem { ItemType = CMSItemType.Page, Slug = "docs/article", Title = "Article", Parts = [page.ID], HeaderID = header.ID, Dirty = true };
		repository.AddOrUpdateCMSItem(item);
		var manager = new RenderingManager(repository);
		var root = repository.Paths.GetRepositoryPath(FileSystemPathType.Absolute);
		string cmsPath;
		using (manager.BeginBatch()) {
			manager.PrepareRenderPaths([target.ID, page.ID, header.ID], cmsItems: [item]);
			var sourcePath = manager.RenderLocalResource(page.ID, RenderType.HTML, CoreMode.ReadOnly);
			manager.RenderLocalResource(header.ID, RenderType.HTML, CoreMode.ReadOnly);
			headerBlock.Paragraph.RichText = [Text("Changed header")];
			repository.UpdateObject(headerBlock);
			manager.RenderCMSItem(item);
			cmsPath = Path.GetFullPath(item.RenderPath, root);
			var html = File.ReadAllText(cmsPath);
			Assert.That(html, Does.Contain("Initial header").And.Not.Contain("Changed header"));
			var sourceLink = new HtmlParser().ParseDocument(File.ReadAllText(sourcePath)).QuerySelectorAll("a").Single(element => element.TextContent.Contains("Destination link")).GetAttribute("href");
			var cmsLink = new HtmlParser().ParseDocument(html).QuerySelectorAll("a").Single(element => element.TextContent.Contains("Destination link")).GetAttribute("href");
			var targetPath = Path.GetFullPath(target.Renders[RenderType.HTML].LocalPath, root);
			var anchor = "#" + targetBlock.Id.Replace("-", "");
			Assert.That(sourceLink, Is.EqualTo(Path.GetRelativePath(Path.GetDirectoryName(sourcePath), targetPath).Replace('\\', '/') + anchor));
			Assert.That(cmsLink, Is.EqualTo(Path.GetRelativePath(Path.GetDirectoryName(cmsPath), targetPath).Replace('\\', '/') + anchor));
			Assert.That(cmsLink, Is.Not.EqualTo(sourceLink));
		}
		using (manager.BeginBatch())
			manager.RenderCMSItem(item);
		Assert.That(File.ReadAllText(cmsPath), Does.Contain("Changed header").And.Not.Contain("Initial header"));
	}

	private async Task<CMSLocalNotionRepository> CreateCmsRepository(string name, NGinxSettings nginxSettings = null, LocalNotionPathProfile pathProfile = null) {
		var databaseId = ID();
		var repository = (CMSLocalNotionRepository)await LocalNotionRepository.CreateNew(NewDirectory(name), cmsDatabaseID: databaseId, nginxSettings: nginxSettings, pathProfile: pathProfile);
		repository.AddObject(new Database { Id = databaseId, Title = [Text("CMS")], Description = [], Parent = new WorkspaceParent() });
		repository.AddResourceGraph(new NotionObjectGraph { ObjectID = databaseId });
		repository.AddResource(new LocalNotionDatabase { ID = databaseId, PrimaryDataSourceID = ID(), Name = "CMS", Title = "CMS", Keywords = [] });
		return repository;
	}

	private static LocalNotionPage AddPage(ILocalNotionRepository repository, string titleValue, Block[] blocks, string parentValue = null) {
		var idValue = ID();
		var pageValue = new Page { Id = idValue, Parent = parentValue == null ? new WorkspaceParent() : new PageParent { PageId = parentValue }, CreatedTime = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc), LastEditedTime = new DateTime(2026, 1, 2, 0, 0, 0, DateTimeKind.Utc), Properties = new Dictionary<string, PropertyValue> { ["Name"] = new TitlePropertyValue { Id = "title", Title = [Text(titleValue)] } } };
		repository.AddObject(pageValue);
		foreach (var blockValue in blocks) { blockValue.Parent ??= new PageParent { PageId = idValue }; repository.AddObject(blockValue); }
		repository.AddResourceGraph(new NotionObjectGraph { ObjectID = idValue, Children = blocks.Select(blockValue => new NotionObjectGraph { ObjectID = blockValue.Id }).ToArray() });
		var local = new LocalNotionPage { ID = idValue, Name = "page-" + idValue, Title = titleValue, ParentResourceID = parentValue, Keywords = [], CreatedOn = pageValue.CreatedTime, LastEditedOn = pageValue.LastEditedTime };
		repository.AddResource(local);
		return local;
	}
	private static void Append(ILocalNotionRepository repository, string idValue, Block blockValue) {
		blockValue.Parent ??= new PageParent { PageId = idValue };
		repository.AddObject(blockValue);
		var graph = repository.GetEditableResourceGraph(idValue);
		graph.Children = graph.Children.Append(new NotionObjectGraph { ObjectID = blockValue.Id }).ToArray();
		repository.UpdateResourceGraph(graph);
	}
	private static global::Notion.Client.ParagraphBlock Paragraph(string textValue, string urlValue = null) => new() { Id = ID(), Paragraph = new global::Notion.Client.ParagraphBlock.Info { Color = global::Notion.Client.Color.Default, RichText = [Text(textValue, urlValue)] } };
	private static RichTextText Text(string value, string urlValue = null) => new() { PlainText = value, Text = new Text { Content = value, Link = urlValue == null ? null : new Link { Url = urlValue } }, Annotations = new Annotations { Color = global::Notion.Client.Color.Default } };
	private static string ID() => Guid.NewGuid().ToString();
	private string NewDirectory(string nameValue) { var pathValue = Path.Combine(_fixtureRoot, nameValue); Directory.CreateDirectory(pathValue); return pathValue; }
	private static void ValidateLocalAssets(string htmlPath) {
		var document = new HtmlParser().ParseDocument(File.ReadAllText(htmlPath));
		var urls = document.QuerySelectorAll("script[src],link[rel=stylesheet][href]").Select(element => element.GetAttribute(element.LocalName == "script" ? "src" : "href"));
		foreach (var urlValue in urls) {
			if (string.IsNullOrWhiteSpace(urlValue) || Uri.TryCreate(urlValue, UriKind.Absolute, out _))
				continue;
			var pathValue = Path.GetFullPath(Uri.UnescapeDataString(urlValue), Path.GetDirectoryName(htmlPath));
			Assert.That(File.Exists(pathValue), Is.True, "generated local CSS/JS exists: " + urlValue);
			Assert.That(File.ReadAllBytes(pathValue), Is.Not.Empty, "generated asset is nonempty: " + urlValue);
		}
	}

	private async Task<string> FormatCmsDocument(string source) {
		using var repository = await CreateCmsRepository("format-preservation");
		var page = AddPage(repository, "Formatting", [Paragraph("Body")], repository.CMSDatabaseID);
		page.CMSProperties = new CMSProperties { PageType = CMSPageType.Page, CustomSlug = "formatting", Status = CMSPageStatus.Published, Themes = [], Tags = [] };
		var item = new CMSItem { ItemType = CMSItemType.Page, Slug = "formatting", Title = "Formatting", Parts = [page.ID] };
		repository.AddOrUpdateCMSItem(item);
		var templates = Path.Combine(repository.Paths.GetInternalResourceFolderPath(InternalResourceType.Themes, FileSystemPathType.Absolute), "cms", "readonly");
		Directory.CreateDirectory(templates);
		File.WriteAllText(Path.Combine(templates, "page.offline.html"), source);
		new RenderingManager(repository).RenderCMSItem(item);
		return File.ReadAllText(Path.GetFullPath(item.RenderPath, repository.Paths.GetRepositoryPath(FileSystemPathType.Absolute)));
	}
}
