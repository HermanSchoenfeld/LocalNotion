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
using LocalNotion.Core;
using Newtonsoft.Json.Linq;
using Notion.Client;
using NUnit.Framework;
using Sphere10.Framework;

namespace LocalNotion.Core.Tests;

[TestFixture]
[Category("Integration")]
[Parallelizable(ParallelScope.Children)]
[FixtureLifeCycle(LifeCycle.InstancePerTestCase)]
public class RedirectTests {
	private record Move(string From, string To);

	private string _fixtureRoot;
	private CMSLocalNotionRepository _repository;

	[SetUp]
	public async Task SetUp() {
		_fixtureRoot = Path.Combine(Path.GetTempPath(), "localnotion-redirects-" + Guid.NewGuid().ToString("N"));
		Directory.CreateDirectory(_fixtureRoot);
		var databaseId = Guid.NewGuid().ToString();
		_repository = (CMSLocalNotionRepository)await LocalNotionRepository.CreateNew(_fixtureRoot, cmsDatabaseID: databaseId, logger: new NoOpLogger());
		_repository.AddObject(new Database { Id = databaseId, Title = [Text("CMS")], Description = [], Parent = new WorkspaceParent() });
		_repository.AddResourceGraph(new NotionObjectGraph { ObjectID = databaseId });
		_repository.AddResource(new LocalNotionDatabase { ID = databaseId, PrimaryDataSourceID = Guid.NewGuid().ToString(), Name = "cms", Title = "CMS", Keywords = [] });
	}

	[TearDown]
	public void TearDown() {
		_repository?.Dispose();
		if (!Directory.Exists(_fixtureRoot))
			return;
		Assert.That(Path.GetDirectoryName(Path.GetFullPath(_fixtureRoot)), Is.EqualTo(Path.GetFullPath(Path.GetTempPath()).TrimEnd(Path.DirectorySeparatorChar)));
		Assert.That(Path.GetFileName(_fixtureRoot), Does.StartWith("localnotion-redirects-"));
		Directory.Delete(_fixtureRoot, recursive: true);
	}

	[Test]
	public void ChangingAPageAddressKeepsTheOldAddress() {
		var page = AddPage(CMSPageType.Page, "articles/coding/old-name", "Old Name");

		_repository.UpdateResource(Moved(page, "articles/coding/new-name"));

		var redirect = _repository.Redirects.Single();
		Assert.That(redirect.From, Is.EqualTo("articles/coding/old-name"));
		Assert.That(redirect.To, Is.EqualTo("articles/coding/new-name"));
		Assert.That(redirect.Type, Is.EqualTo(RedirectType.Hard));
		Assert.That(redirect.ResourceID, Is.EqualTo(page.ID));
		Assert.That(redirect.CreatedOn, Is.EqualTo(DateTime.UtcNow.Date));
		Assert.That(_repository.ContainsCmsItem("articles/coding/new-name"), Is.True);
		Assert.That(_repository.ContainsCmsItem("articles/coding/old-name"), Is.False);
	}

	[Test]
	public void RefreshedPageAtANewAddressKeepsTheOldAddress() {
		// A pull removes a page and adds the refreshed copy under the same ID.
		var page = AddPage(CMSPageType.Page, "articles/write-a-serializer-in-c", "Write a Serializer in C#");
		_repository.RemoveResource(page.ID, false);

		AddPage(CMSPageType.Page, "articles/write-a-serializer-in-c-sharp", "Write a Serializer in C#", page.ID);

		Assert.That(Pairs(), Is.EqualTo(new[] { new Move("articles/write-a-serializer-in-c", "articles/write-a-serializer-in-c-sharp") }));
	}

	[Test]
	public void RemovedPageKeepsNoRedirect() {
		var page = AddPage(CMSPageType.Page, "articles/retired", "Retired");

		_repository.RemoveResource(page.ID, false);

		Assert.That(_repository.Redirects, Is.Empty);
	}

	[Test]
	public void UnchangedAddressKeepsNoRedirect() {
		var page = AddPage(CMSPageType.Page, "articles/stable", "Stable");
		_repository.RemoveResource(page.ID, false);

		AddPage(CMSPageType.Page, "articles/stable", "Stable", page.ID);

		Assert.That(_repository.Redirects, Is.Empty);
	}

	[Test]
	public void RedirectChainsCollapseToTheCurrentAddress() {
		var page = AddPage(CMSPageType.Page, "articles/first", "First");
		_repository.UpdateResource(Moved(page, "articles/second"));
		var original = _repository.Redirects.Single();
		original.CreatedOn = new DateTime(2025, 1, 1);

		_repository.UpdateResource(Moved(page, "articles/third"));

		Assert.That(Pairs(), Is.EquivalentTo(new[] { new Move("articles/first", "articles/third"), new Move("articles/second", "articles/third") }));
		Assert.That(_repository.Redirects.Single(redirect => redirect.From == "articles/first").CreatedOn, Is.EqualTo(new DateTime(2025, 1, 1)), "A retargeted redirect keeps its date.");
	}

	[Test]
	public void MovingBackReplacesTheReverseRedirect() {
		var page = AddPage(CMSPageType.Page, "articles/a", "A");
		_repository.UpdateResource(Moved(page, "articles/b"));

		_repository.UpdateResource(Moved(page, "articles/a"));

		Assert.That(Pairs(), Is.EqualTo(new[] { new Move("articles/b", "articles/a") }));
	}

	[Test]
	public void APageAtARedirectedAddressRemovesTheRedirect() {
		var page = AddPage(CMSPageType.Page, "articles/old", "Old");
		_repository.UpdateResource(Moved(page, "articles/new"));

		AddPage(CMSPageType.Page, "articles/old", "Another Old");

		Assert.That(_repository.Redirects, Is.Empty);
	}

	[Test]
	public void LiveAddressesAreNeverRedirected() {
		AddPage(CMSPageType.Page, "articles/live", "Live");
		AddPage(CMSPageType.Page, "articles/other", "Other");

		Assert.That(_repository.AddRedirect("articles/live", "articles/other"), Is.False);
		Assert.That(_repository.AddRedirect("/", "articles/other"), Is.False, "The home page is never redirected.");
		Assert.That(_repository.AddRedirect("articles/other", "/articles/other/"), Is.False, "An address is never redirected to itself.");
		Assert.That(_repository.Redirects, Is.Empty);
	}

	[Test]
	public void SectionsRedirectTheirPageOnceItHasNoSectionsLeft() {
		var first = AddPage(CMSPageType.Section, "services/old#first", "First");
		var second = AddPage(CMSPageType.Section, "services/old#second", "Second");

		_repository.UpdateResource(Moved(first, "services/new#first"));
		Assert.That(_repository.Redirects, Is.Empty, "The old page is still served by its other section.");

		_repository.UpdateResource(Moved(second, "services/new#second"));
		Assert.That(Pairs(), Is.EqualTo(new[] { new Move("services/old", "services/new") }));
	}

	[Test]
	public void CategoriesRedirectOnceTheirLastPageMoves() {
		var first = AddPage(CMSPageType.Page, "articles/old-category/first", "First");
		var second = AddPage(CMSPageType.Page, "articles/old-category/second", "Second");

		_repository.UpdateResource(Moved(first, "articles/new-category/first"));
		Assert.That(Pairs(), Is.EqualTo(new[] { new Move("articles/old-category/first", "articles/new-category/first") }));

		_repository.UpdateResource(Moved(second, "articles/new-category/second"));
		Assert.That(Pairs(), Is.EquivalentTo(new[] {
			new Move("articles/old-category/first", "articles/new-category/first"),
			new Move("articles/old-category/second", "articles/new-category/second"),
			new Move("articles/old-category", "articles/new-category")
		}));
	}

	[Test]
	public void GalleryCardsKeepTheirOldAddress() {
		var card = AddPage(CMSPageType.Gallery, "catalogue/old-card", "Old Card");

		_repository.UpdateResource(Moved(card, "catalogue/new-card"));

		Assert.That(Pairs(), Is.EqualTo(new[] { new Move("catalogue/old-card", "catalogue/new-card") }));
	}

	[Test]
	public void RetitledPageKeepsItsOldRawAddress() {
		// A page's raw export lives at an address derived from its title.
		var page = AddRawPage("Old Title");
		var oldSlug = page.Renders[RenderType.HTML].Slug;
		_repository.RemoveResource(page.ID, false);

		var refreshed = new LocalNotionPage { ID = page.ID, Name = "raw-" + page.ID, Title = "New Title", ParentResourceID = _repository.CMSDatabaseID, Keywords = [] };
		_repository.AddResource(refreshed);
		var importedPath = ImportRender(refreshed.ID);

		var newSlug = _repository.GetResource(page.ID).Renders[RenderType.HTML].Slug;
		Assert.That(oldSlug, Does.EndWith("old-title"));
		Assert.That(newSlug, Does.EndWith("new-title"));
		Assert.That(File.Exists(importedPath), Is.True);
		Assert.That(Pairs(), Is.EqualTo(new[] { new Move(oldSlug, newSlug) }));
	}

	[Test]
	public void ImportedRenderRemovesARedirectFromItsAddress() {
		var page = AddRawPage("Page");
		var slug = page.Renders[RenderType.HTML].Slug;
		_repository.RemoveResourceRender(page.ID, RenderType.HTML);
		Assert.That(_repository.AddRedirect(slug, "articles/elsewhere"), Is.True);

		ImportRender(page.ID);

		Assert.That(_repository.Redirects, Is.Empty);
	}

	[Test]
	public void ManualRedirectsReplaceAndRemove() {
		Assert.That(_repository.AddRedirect("/legacy/", "articles/current", RedirectType.Soft), Is.True);
		Assert.That(Pairs(), Is.EqualTo(new[] { new Move("legacy", "articles/current") }), "Addresses are stored site-relative without slashes.");
		Assert.That(_repository.Redirects.Single().Type, Is.EqualTo(RedirectType.Soft));

		Assert.That(_repository.RemoveRedirect("legacy"), Is.True);
		Assert.That(_repository.RemoveRedirect("legacy"), Is.False);
		Assert.That(_repository.Redirects, Is.Empty);
	}

	[Test]
	public async Task RedirectsPersistInTheRegistry() {
		var registryPath = _repository.Paths.GetRegistryFilePath(FileSystemPathType.Absolute);
		await _repository.SaveAsync();
		Assert.That(JObject.Parse(await File.ReadAllTextAsync(registryPath)).ContainsKey("redirects"), Is.False, "A registry without redirects keeps its shape.");

		_repository.AddRedirect("articles/old", "articles/new");
		await _repository.SaveAsync();
		var stored = (JObject)JObject.Parse(await File.ReadAllTextAsync(registryPath))["redirects"].Single();
		Assert.That(stored["from"].Value<string>(), Is.EqualTo("articles/old"));
		Assert.That(stored["to"].Value<string>(), Is.EqualTo("articles/new"));
		Assert.That(stored["type"].Value<string>(), Is.EqualTo("Hard"));
		Assert.That(stored.ContainsKey("created_on"), Is.True);
		_repository.Dispose();

		_repository = (CMSLocalNotionRepository)await LocalNotionRepository.Open(_fixtureRoot, new NoOpLogger());
		Assert.That(Pairs(), Is.EqualTo(new[] { new Move("articles/old", "articles/new") }));
		Assert.That(_repository.Redirects.Single().CreatedOn, Is.EqualTo(DateTime.UtcNow.Date));
	}

	private Move[] Pairs() => _repository.Redirects.Select(redirect => new Move(redirect.From, redirect.To)).ToArray();

	private LocalNotionPage AddPage(CMSPageType type, string slug, string title, string id = null) {
		id ??= Guid.NewGuid().ToString();
		if (!_repository.ContainsObject(id)) {
			_repository.AddObject(new Page {
				Id = id,
				Parent = new PageParent { PageId = _repository.CMSDatabaseID },
				Properties = new Dictionary<string, PropertyValue> { ["Name"] = new TitlePropertyValue { Id = "title", Title = [Text(title)] } }
			});
			_repository.AddResourceGraph(new NotionObjectGraph { ObjectID = id });
		}
		var page = new LocalNotionPage {
			ID = id,
			Name = "page-" + id,
			Title = title,
			ParentResourceID = _repository.CMSDatabaseID,
			Keywords = [title.ToLowerInvariant()],
			CMSProperties = Properties(type, slug)
		};
		_repository.AddResource(page);
		return page;
	}

	private LocalNotionPage AddRawPage(string title) {
		var page = new LocalNotionPage { ID = Guid.NewGuid().ToString(), Title = title, ParentResourceID = _repository.CMSDatabaseID, Keywords = [] };
		page.Name = "raw-" + page.ID;
		_repository.AddResource(page);
		ImportRender(page.ID);
		return (LocalNotionPage)_repository.GetResource(page.ID);
	}

	private string ImportRender(string resourceID) {
		var file = Path.Combine(_fixtureRoot, "render-" + Guid.NewGuid().ToString("N") + ".html");
		File.WriteAllText(file, "<html></html>");
		return _repository.ImportResourceRender(resourceID, RenderType.HTML, file);
	}

	private static LocalNotionPage Moved(LocalNotionPage page, string slug) => new() {
		ID = page.ID,
		Name = page.Name,
		Title = page.Title,
		ParentResourceID = page.ParentResourceID,
		Keywords = page.Keywords,
		CMSProperties = Properties(page.CMSProperties.PageType, slug)
	};

	private static CMSProperties Properties(CMSPageType type, string slug)
		=> new() { PageType = type, Status = CMSPageStatus.Published, CustomSlug = slug, Themes = [], Tags = [] };

	private static RichTextText Text(string text) => new() { PlainText = text, Text = new Text { Content = text } };
}
