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
public class CmsArticlePaginationTests {

	private string _root;
	private CMSLocalNotionRepository _repository;
	private LocalNotionPage[] _articles;
	private CMSItem _listing;

	[SetUp]
	public void SetUp() {
		_root = Path.Combine(Path.GetTempPath(), "localnotion-article-pages-" + Guid.NewGuid().ToString("N"));
		Directory.CreateDirectory(_root);
	}

	[TearDown]
	public void TearDown() {
		try {
			_repository?.Dispose();
		} finally {
			if (!_root.StartsWith(Path.GetFullPath(Path.GetTempPath()), StringComparison.OrdinalIgnoreCase))
				throw new InvalidOperationException("Fixture escaped its temporary parent.");
			if (Directory.Exists(_root))
				Directory.Delete(_root, recursive: true);
		}
	}

	[TestCase(0, 1, 0, 0, 0)]
	[TestCase(20, 1, 20, 1, 20)]
	[TestCase(50, 1, 20, 1, 20)]
	[TestCase(50, 2, 20, 21, 40)]
	[TestCase(50, 3, 10, 41, 50)]
	public async Task ProjectionMaterializesOnlyTheRequestedSlice(int count, int number, int expectedCount, int start, int end) {
		await CreateRepository(count);
		var document = Builder().Build(_listing, number);
		var body = Templates(document).Single(template => template.Template == "articles");
		var summaries = Summaries(document);
		Assert.That(summaries, Has.Length.EqualTo(expectedCount));
		Assert.That(summaries.Select(summary => summary.Template), Is.EqualTo(Enumerable.Range(0, expectedCount)
			.Select(index => index % 2 == 0 ? "articles_summary" : "articles_summary_alt")));
		Assert.That(body.Tokens["range_start"], Is.EqualTo(start.ToString()));
		Assert.That(body.Tokens["range_end"], Is.EqualTo(end.ToString()));
		Assert.That(body.Tokens["total_count"], Is.EqualTo(count.ToString()));
		Assert.That(summaries.Select(summary => summary.Tokens["slug"]), Is.EqualTo(_articles.Skip((number - 1) * 20).Take(20).Select(page => page.CMSProperties.CustomSlug)));
		Assert.That(summaries.Select(summary => summary.Tokens["url"]), Is.EqualTo(_articles.Skip((number - 1) * 20).Take(20)
			.Select(page => "https://example.test/content/" + page.CMSProperties.CustomSlug)));
	}

	[Test]
	public async Task CategoriesUseTheSameBoundedListingAndOnlineQueryLinks() {
		await CreateRepository(41);
		var category = new CMSItem { ItemType = CMSItemType.CategoryPage, Slug = "articles/topic", Title = "Topic", Parts = _listing.Parts };
		_repository.AddOrUpdateCMSItem(category);
		var document = Builder().Build(category, 2);
		var links = Templates(document).Where(template => template.Template == "articles_page_link").ToArray();
		Assert.That(Summaries(document), Has.Length.EqualTo(20));
		Assert.That(links.Select(link => link.Tokens["url"]), Does.Contain("https://example.test/content/articles/topic"));
		Assert.That(links.Select(link => link.Tokens["url"]), Does.Contain("https://example.test/content/articles/topic?page=3"));
		Assert.That(_articles.Select(page => page.CMSProperties.CustomSlug), Is.All.Not.Contains("?page="));
	}

	[Test]
	public async Task ListingsRespectSequenceBeforeNewestCreationTime() {
		await CreateRepository(5);
		int?[] sequences = [2, 1, 1, null, 1];
		int[] days = [10, 1, 3, 20, 2];
		for (var index = 0; index < _articles.Length; index++) {
			_articles[index].CMSProperties.Sequence = sequences[index];
			_articles[index].CreatedOn = new DateTime(2026, 1, days[index]);
		}
		Assert.That(Summaries(Builder().Build(_listing, 1)).Select(summary => summary.Tokens["slug"]),
			Is.EqualTo(new[] { 2, 4, 1, 0, 3 }.Select(index => _articles[index].CMSProperties.CustomSlug)));
	}

	[Test]
	public async Task UnnumberedArticlesShowNewestCreationFirst() {
		await CreateRepository(3);
		int[] days = [1, 3, 2];
		for (var index = 0; index < _articles.Length; index++) {
			_articles[index].CMSProperties.Sequence = null;
			_articles[index].CreatedOn = new DateTime(2026, 1, days[index]);
		}
		Assert.That(Summaries(Builder().Build(_listing, 1)).Select(summary => summary.Tokens["slug"]),
			Is.EqualTo(new[] { 1, 2, 0 }.Select(index => _articles[index].CMSProperties.CustomSlug)));
	}

	[Test]
	public async Task MetadataEditsAndSourceEnumerationDoNotReorderCreationTies() {
		await CreateRepository(3);
		foreach (var article in _articles)
			article.CMSProperties.Sequence = null;
		var expected = _articles.Select(article => article.CMSProperties.CustomSlug).OrderBy(slug => slug, StringComparer.Ordinal).ToArray();
		Assert.That(Summaries(Builder().Build(_listing, 1)).Select(summary => summary.Tokens["slug"]), Is.EqualTo(expected));
		_articles[1].LastEditedOn = new DateTime(2026, 11, 12);
		_articles[0].LastEditedOn = new DateTime(2026, 10, 1);
		_listing.Parts = _listing.Parts.Reverse().ToArray();
		Assert.That(Summaries(Builder().Build(_listing, 1)).Select(summary => summary.Tokens["slug"]), Is.EqualTo(expected));
	}

	[TestCase(LocalNotionMode.Online)]
	[TestCase(LocalNotionMode.Offline)]
	public async Task RenamedNestedCollectionAndCategoryKeepIndependentArticleDestinations(LocalNotionMode mode) {
		await CreateRepository(41, mode, ["Fixed", "Fixed2", "Field Notes", "Device Setup"]);
		var category = new CMSItem { ItemType = CMSItemType.CategoryPage, Slug = "fixed/fixed2/field-notes/device-setup", Title = "Device Setup", Parts = _listing.Parts };
		_repository.AddOrUpdateCMSItem(category);
		var articleSlugs = _articles.Select(page => page.CMSProperties.CustomSlug).ToArray();
		var manager = new RenderingManager(_repository);
		using (manager.BeginBatch()) {
			manager.PrepareRenderPaths([], cmsItems: _repository.CMSItems);
			manager.RenderCMSItem(_listing);
			manager.RenderCMSItem(category);
		}
		foreach (var listing in new[] { _listing, category }) {
			var document = Builder().Build(listing, 2);
			var links = Templates(document).Where(template => template.Template == "articles_page_link").ToArray();
			Assert.That(document.Title, Is.EqualTo(listing.Title));
			Assert.That(Summaries(document), Has.Length.EqualTo(20));
			Assert.That(listing.ListingRenderPaths, Has.Length.EqualTo(3));
			var expectedNext = mode == LocalNotionMode.Online
				? "https://example.test/content/" + listing.Slug + "?page=3"
				: Path.GetFileName(listing.ListingRenderPaths[2]);
			Assert.That(links.Single(link => link.Tokens["label"] == "Next").Tokens["url"], Is.EqualTo(expectedNext));
			var expectedArticleUrls = _articles.Skip(20).Take(20).Select(page => mode == LocalNotionMode.Online
				? "https://example.test/content/" + page.CMSProperties.CustomSlug
				: Path.GetFileName(_repository.CMSItems.Single(item => item.Slug == page.CMSProperties.CustomSlug).RenderPath));
			Assert.That(Summaries(document).Select(summary => summary.Tokens["url"]), Is.EqualTo(expectedArticleUrls));
		}
		Assert.That(_listing.Slug, Is.EqualTo("fixed/fixed2/field-notes"));
		Assert.That(_articles.Select(page => page.CMSProperties.CustomSlug), Is.EqualTo(articleSlugs));
		Assert.That(articleSlugs, Is.All.StartsWith("fixed/fixed2/field-notes/device-setup/article-"));
		Assert.That(articleSlugs, Is.All.Not.Contains("?page="));
	}

	[TestCase(201)]
	[TestCase(1001)]
	public async Task PagerRemainsBoundedForLargePublications(int count) {
		await CreateRepository(count);
		var links = Templates(Builder().Build(_listing, 6)).Where(template => template.Template.StartsWith("articles_page_", StringComparison.Ordinal)).ToArray();
		Assert.That(links, Has.Length.LessThanOrEqualTo(11));
		Assert.That(links.Count(template => template.Template == "articles_page_gap"), Is.EqualTo(2));
		Assert.That(links.Count(template => template.Template == "articles_page_active"), Is.EqualTo(1));
		Assert.That(links.Where(template => template.Template == "articles_page_link").Select(template => template.Tokens["label"]),
			Does.Contain("Previous").And.Contain("Next").And.Contain("1").And.Contain(((count + 19) / 20).ToString()));
		var html = new R.HtmlRenderer().Render(Builder().Build(_listing, 6)).Html;
		Assert.That(new HtmlParser().ParseDocument(html).QuerySelectorAll(".article-summary"), Has.Length.EqualTo(20));
		Assert.That(html.Length, Is.LessThan(100000), "The output contains one summary slice and a bounded pager, not the complete publication.");
	}

	[TestCase(0)]
	[TestCase(4)]
	public async Task ProjectionRejectsPagesOutsideTheStaticListing(int pageNumber) {
		await CreateRepository(41);
		Assert.That(() => Builder().Build(_listing, pageNumber), Throws.TypeOf<ArgumentOutOfRangeException>());
	}

	[Test]
	public async Task RenderPublishesSeparateHtmlPagesAndOfflinePagerUsesReservedFiles() {
		await CreateRepository(41, LocalNotionMode.Offline);
		var manager = new RenderingManager(_repository);
		using (manager.BeginBatch()) {
			manager.PrepareRenderPaths([], cmsItems: _repository.CMSItems);
			manager.RenderCMSItem(_listing);
		}
		Assert.That(_listing.ListingRenderPaths, Has.Length.EqualTo(3));
		Assert.That(_listing.ListingRenderPaths[0], Is.EqualTo(_listing.RenderPath));
		Assert.That(_listing.ListingRenderPaths, Is.Unique);
		var documents = _listing.ListingRenderPaths.Select(path => new HtmlParser().ParseDocument(File.ReadAllText(Absolute(path)))).ToArray();
		Assert.That(documents.Select(document => document.QuerySelectorAll(".article-summary").Length), Is.EqualTo(new[] { 20, 20, 1 }));
		var second = Path.GetFileName(_listing.ListingRenderPaths[1]);
		Assert.That(documents[0].QuerySelectorAll(".pagination a").Select(link => link.GetAttribute("href")), Does.Contain(second));
		Assert.That(documents[1].QuerySelectorAll(".pagination a").Select(link => link.GetAttribute("href")), Does.Contain(Path.GetFileName(_listing.RenderPath)));
		Assert.That(_articles.Select(page => page.CMSProperties.CustomSlug), Is.All.Not.Contains("?page="));
		Assert.That(_listing.Dirty, Is.False);
	}

	[Test]
	public async Task SupplementaryFilesCannotOverwriteAnExistingArticleDestination() {
		await CreateRepository(21);
		var collision = new CMSItem { ItemType = CMSItemType.Page, Slug = "articles-page-2", Title = "Existing article", Parts = [_articles[0].ID] };
		_repository.AddOrUpdateCMSItem(collision);
		var manager = new RenderingManager(_repository);
		using (manager.BeginBatch()) {
			manager.PrepareRenderPaths([], cmsItems: new[] { _listing, collision });
			File.WriteAllText(Absolute(collision.RenderPath), "Keep this article");
			manager.RenderCMSItem(_listing);
		}
		Assert.That(_listing.ListingRenderPaths[1], Is.Not.EqualTo(collision.RenderPath));
		Assert.That(File.ReadAllText(Absolute(collision.RenderPath)), Is.EqualTo("Keep this article"));
	}

	[Test]
	public async Task SuccessfulShrinkAndRemovalRetireAllPreviousListingFiles() {
		await CreateRepository(41);
		var manager = new RenderingManager(_repository);
		using (manager.BeginBatch())
			manager.RenderCMSItem(_listing);
		var oldPaths = _listing.ListingRenderPaths.Select(Absolute).ToArray();
		_listing.Parts = _listing.Parts.Take(5).ToArray();
		_listing.Dirty = true;
		using (manager.BeginBatch())
			manager.RenderCMSItem(_listing);
		Assert.That(_listing.ListingRenderPaths, Has.Length.EqualTo(1));
		Assert.That(File.Exists(oldPaths[0]), Is.True);
		Assert.That(oldPaths.Skip(1).Any(File.Exists), Is.False);
		_repository.RemoveCmsItem(_listing.Slug);
		Assert.That(oldPaths.Any(File.Exists), Is.False);
	}

	[Test]
	public async Task LaterSliceProjectionFailurePreservesPublishedFilesAndManifest() {
		await CreateRepository(21);
		var manager = new RenderingManager(_repository);
		using (manager.BeginBatch())
			manager.RenderCMSItem(_listing);
		var paths = _listing.ListingRenderPaths.ToArray();
		var originals = paths.Select(path => File.ReadAllBytes(Absolute(path))).ToArray();
		_articles[0].CMSProperties.Summary = "Changed first page summary";
		_articles[20].Thumbnail = null;
		_listing.Dirty = true;
		using (manager.BeginBatch())
			Assert.That(() => manager.RenderCMSItem(_listing), Throws.TypeOf<NullReferenceException>());
		Assert.That(_listing.ListingRenderPaths, Is.EqualTo(paths));
		Assert.That(_listing.Dirty, Is.True);
		for (var index = 0; index < paths.Length; index++)
			Assert.That(File.ReadAllBytes(Absolute(paths[index])), Is.EqualTo(originals[index]));
	}

	[Test]
	[Platform("Win")]
	public async Task RetirementFailureKeepsTheCommittedReplacementManifest() {
		await CreateRepository(41);
		var manager = new RenderingManager(_repository);
		using (manager.BeginBatch())
			manager.RenderCMSItem(_listing);
		var obsolete = Absolute(_listing.ListingRenderPaths[2]);
		_listing.Parts = _listing.Parts.Take(21).ToArray();
		_articles[0].CMSProperties.Summary = "Committed replacement summary";
		_listing.Dirty = true;
		File.SetAttributes(obsolete, FileAttributes.ReadOnly);
		try {
			using (manager.BeginBatch())
				Assert.That(() => manager.RenderCMSItem(_listing), Throws.TypeOf<UnauthorizedAccessException>());
			Assert.That(_listing.ListingRenderPaths, Has.Length.EqualTo(2));
			Assert.That(_listing.Dirty, Is.False);
			Assert.That(_listing.ListingRenderPaths.All(path => File.Exists(Absolute(path))), Is.True);
			Assert.That(File.ReadAllText(Absolute(_listing.RenderPath)), Does.Contain("Committed replacement summary"));
		} finally {
			File.SetAttributes(obsolete, FileAttributes.Normal);
		}
	}

	[Test]
	public async Task OutputStagingFailurePreservesEveryPublishedPage() {
		await CreateRepository(21);
		var manager = new RenderingManager(_repository);
		using (manager.BeginBatch())
			manager.RenderCMSItem(_listing);
		var paths = _listing.ListingRenderPaths.ToArray();
		var originals = paths.Select(path => File.ReadAllBytes(Absolute(path))).ToArray();
		_articles[0].CMSProperties.Summary = "Changed first page summary";
		_listing.Dirty = true;
		using (var locked = new FileStream(Absolute(paths[1]), FileMode.Open, FileAccess.Read, FileShare.None))
			using (manager.BeginBatch())
				Assert.That(() => manager.RenderCMSItem(_listing), Throws.InstanceOf<IOException>());
		Assert.That(_listing.ListingRenderPaths, Is.EqualTo(paths));
		Assert.That(_listing.Dirty, Is.True);
		for (var index = 0; index < paths.Length; index++)
			Assert.That(File.ReadAllBytes(Absolute(paths[index])), Is.EqualTo(originals[index]));
		Assert.That(Directory.EnumerateFiles(Path.GetDirectoryName(Absolute(paths[0])), ".render-*"), Is.Empty);
	}

	[TestCase(false, false)]
	[TestCase(true, false)]
	[TestCase(false, true)]
	[TestCase(true, true)]
	public async Task ArticlesReceiveOneNativeBreadcrumbAndTocWithoutChangingSource(bool authoredBreadcrumb, bool authoredToc) {
		await CreateRepository(1);
		var headings = new IBlock[] {
			new HeadingOneBlock { Id = Guid.NewGuid().ToString("D"), Heading_1 = new() { RichText = [Text("Overview")] } },
			new HeadingTwoBlock { Id = Guid.NewGuid().ToString("D"), Heading_2 = new() { RichText = [Text("Details")] } },
			new HeadingThreeBlock { Id = Guid.NewGuid().ToString("D"), Heading_3 = new() { RichText = [Text("Example")] } }
		};
		var blocks = (authoredBreadcrumb
			? new IBlock[] { new global::Notion.Client.BreadcrumbBlock { Id = Guid.NewGuid().ToString("D"), Breadcrumb = new() } }
			: Array.Empty<IBlock>())
			.Concat(authoredToc
				? new IBlock[] { new global::Notion.Client.TableOfContentsBlock { Id = Guid.NewGuid().ToString("D"), TableOfContents = new() } }
				: Array.Empty<IBlock>())
			.Concat(headings).ToArray();
		SetContent(_articles[0], blocks);
		var item = _repository.CMSItems.Single(candidate => candidate.Slug == _articles[0].CMSProperties.CustomSlug);
		var document = Builder().Build(item);
		Assert.That(document.Children.OfType<R.BreadcrumbBlock>().Count(), Is.EqualTo(1));
		Assert.That(document.Children.OfType<R.TableOfContentsBlock>().Count(), Is.EqualTo(1));
		Assert.That(document.Children.OfType<R.HeadingBlock>().Select(heading => heading.Level), Is.EqualTo(new[] { 1, 2, 3 }));
		var html = new R.HtmlRenderer().Render(document).Html;
		var navigation = new HtmlParser().ParseDocument(html).QuerySelectorAll("nav")
			.Where(node => node.GetAttribute("aria-label") == "breadcrumb" || node.ClassList.Contains("ln-toc")).ToArray();
		Assert.That(navigation.Select(node => node.ClassList.Contains("ln-toc") ? "toc" : "breadcrumb"), Is.EqualTo(new[] { "breadcrumb", "toc" }));
		foreach (var authoredBlock in blocks.Where(block => block is global::Notion.Client.BreadcrumbBlock or global::Notion.Client.TableOfContentsBlock))
			Assert.That(navigation.Select(node => node.Id), Does.Contain(Guid.Parse(authoredBlock.Id).ToString("N")));
		foreach (var heading in document.Children.OfType<R.HeadingBlock>())
			Assert.That(html, Does.Contain("id=\"" + heading.Metadata.Anchor + "\""));
		Assert.That(_repository.GetEditableResourceGraph(_articles[0].ID).Children.Select(child => child.ObjectID), Is.EqualTo(blocks.Select(block => block.Id)));
		Assert.That(item.Slug, Is.EqualTo("articles/topic/article-1"));
	}

	[Test]
	public async Task GeneratedTocFollowsAuthoredBreadcrumbWithoutMovingItsIntroductoryContent() {
		await CreateRepository(1);
		var intro = new global::Notion.Client.ParagraphBlock { Id = Guid.NewGuid().ToString("D"), Paragraph = new() { RichText = [Text("Authored introduction")] } };
		var breadcrumb = new global::Notion.Client.BreadcrumbBlock { Id = Guid.NewGuid().ToString("D"), Breadcrumb = new() };
		var heading = new HeadingOneBlock { Id = Guid.NewGuid().ToString("D"), Heading_1 = new() { RichText = [Text("Overview")] } };
		SetContent(_articles[0], intro, breadcrumb, heading);
		var item = _repository.CMSItems.Single(candidate => candidate.ItemType == CMSItemType.Page);
		var document = Builder().Build(item);
		Assert.That(document.Children.Select(node => node.GetType()), Is.EqualTo(new[] {
			typeof(R.ParagraphBlock), typeof(R.BreadcrumbBlock), typeof(R.TableOfContentsBlock), typeof(R.HeadingBlock)
		}));
		Assert.That(document.Children.OfType<R.ParagraphBlock>().Single().Text.OfType<R.TextInline>().Single().Text, Is.EqualTo("Authored introduction"));
		Assert.That(document.Children.OfType<R.BreadcrumbBlock>().Single().Metadata.Anchor, Is.EqualTo(Guid.Parse(breadcrumb.Id).ToString("N")));
		Assert.That(_repository.GetEditableResourceGraph(_articles[0].ID).Children.Select(child => child.ObjectID), Is.EqualTo(new[] { intro.Id, breadcrumb.Id, heading.Id }));
	}

	[Test]
	public async Task UnlistedPagesAndArticlesWithoutHeadingsDoNotReceiveInventedTocs() {
		await CreateRepository(1);
		SetContent(_articles[0], new HeadingOneBlock { Id = Guid.NewGuid().ToString("D"), Heading_1 = new() { RichText = [Text("   ")] } });
		var item = _repository.CMSItems.Single(candidate => candidate.ItemType == CMSItemType.Page);
		var article = Builder().Build(item);
		Assert.That(article.Children.OfType<R.BreadcrumbBlock>().Count(), Is.EqualTo(1));
		Assert.That(article.Children.OfType<R.TableOfContentsBlock>(), Is.Empty);
		_listing.Parts = [];
		var normal = Builder().Build(item);
		Assert.That(normal.Children.OfType<R.BreadcrumbBlock>(), Is.Empty);
		Assert.That(normal.Children.OfType<R.TableOfContentsBlock>(), Is.Empty);
	}

	private async Task CreateRepository(int count, LocalNotionMode mode = LocalNotionMode.Online, string[] categories = null) {
		categories ??= ["Articles", "Topic"];
		var articleCategorySlug = string.Join("/", categories.Select(LocalNotionHelper.SanitizeSlug));
		var listingSlug = string.Join("/", categories.SkipLast(1).Select(LocalNotionHelper.SanitizeSlug));
		var databaseId = Guid.NewGuid().ToString("D");
		_repository = (CMSLocalNotionRepository)await LocalNotionRepository.CreateNew(_root, cmsDatabaseID: databaseId,
			pathProfile: new LocalNotionPathProfile { Mode = mode, BaseUrl = mode == LocalNotionMode.Online ? "https://example.test/content/" : "" }, logger: new NoOpLogger());
		_repository.AddObject(new Database { Id = databaseId });
		_repository.AddResourceGraph(new NotionObjectGraph { ObjectID = databaseId });
		_repository.AddResource(new LocalNotionDatabase { ID = databaseId, Name = "cms", Title = "CMS", PrimaryDataSourceID = Guid.NewGuid().ToString("N") });
		_articles = Enumerable.Range(1, count).Select(index => {
			var page = new LocalNotionPage {
				ID = Guid.NewGuid().ToString("D"), Name = "Article " + index, Title = "Article " + index, ParentResourceID = databaseId,
				Keywords = [], CreatedOn = new DateTime(2026, 1, 1), LastEditedOn = new DateTime(2026, 1, 2)
			};
			_repository.AddResource(page);
			page.CMSProperties = new CMSProperties {
				PageType = CMSPageType.Page, CustomSlug = articleCategorySlug + "/article-" + index, Root = categories[0],
				Category1 = categories.ElementAtOrDefault(1), Category2 = categories.ElementAtOrDefault(2), Category3 = categories.ElementAtOrDefault(3),
				Status = CMSPageStatus.Published, Themes = [], Tags = [], Summary = "Summary " + index, Sequence = index
			};
			SetContent(page);
			_repository.AddOrUpdateCMSItem(new CMSItem { ItemType = CMSItemType.Page, Slug = page.CMSProperties.CustomSlug, Title = page.Title, Parts = [page.ID], Dirty = true });
			return page;
		}).ToArray();
		_listing = new CMSItem { ItemType = CMSItemType.CategoryPage, Slug = count == 0 ? "" : listingSlug, Title = categories[^2], Parts = _articles.Select(page => page.ID).ToArray(), Dirty = true };
		_repository.AddOrUpdateCMSItem(_listing);
	}

	private void SetContent(LocalNotionPage page, params IBlock[] blocks) {
		if (!_repository.ContainsObject(page.ID))
			_repository.AddObject(new Page { Id = page.ID });
		foreach (var block in blocks)
			_repository.AddObject(block);
		var graph = new NotionObjectGraph { ObjectID = page.ID, Children = blocks.Select(block => new NotionObjectGraph { ObjectID = block.Id }).ToArray() };
		if (_repository.ContainsResourceGraph(page.ID))
			_repository.UpdateResourceGraph(graph);
		else
			_repository.AddResourceGraph(graph);
	}

	private CmsRenderModelBuilder Builder() => new CmsRenderModelBuilder(_repository, Path.Combine(_root, "cms"));

	private string Absolute(string path) => Path.GetFullPath(path, _root);

	private static RichTextText Text(string text) => new RichTextText { Text = new() { Content = text }, PlainText = text };

	private static R.TemplateBlock[] Summaries(R.DocumentBlock document)
		=> Templates(document).Where(template => template.Template is "articles_summary" or "articles_summary_alt").ToArray();

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
}
