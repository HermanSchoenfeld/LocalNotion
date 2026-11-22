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
using Notion.Client;
using NUnit.Framework;
using Sphere10.Framework;

namespace LocalNotion.Core.Tests;

[TestFixture]
[Category("Integration")]
[Parallelizable(ParallelScope.Children)]
[FixtureLifeCycle(LifeCycle.InstancePerTestCase)]
public class CmsGalleryLifecycleTests {
	private string _fixtureRoot;
	private CMSLocalNotionRepository _repository;

	[SetUp]
	public async Task SetUp() {
		_fixtureRoot = Path.Combine(Path.GetTempPath(), "localnotion-cms-gallery-" + Guid.NewGuid().ToString("N"));
		Directory.CreateDirectory(_fixtureRoot);
		var databaseId = Guid.NewGuid().ToString();
		_repository = (CMSLocalNotionRepository)await LocalNotionRepository.CreateNew(_fixtureRoot, cmsDatabaseID: databaseId);
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
		Assert.That(Path.GetFileName(_fixtureRoot), Does.StartWith("localnotion-cms-gallery-"));
		Directory.Delete(_fixtureRoot, recursive: true);
	}

	[Test]
	public void RemovingOneCardRetainsGalleryAndOtherCard([Values] bool removeFirst) {
		// A published collection has two cards and its own inherited framing.
		AddPage(CMSPageType.Header, "", "Header");
		AddPage(CMSPageType.Internal, "catalogue", "Settings", [Constants.TagUseParentHeader]);
		var first = AddPage(CMSPageType.Gallery, "catalogue/first", "First");
		var second = AddPage(CMSPageType.Gallery, "catalogue/second", "Second");
		var removed = removeFirst ? first : second;
		var retained = removeFirst ? second : first;
		var original = _repository.GetCMSItem("catalogue");
		var headerId = original.HeaderID;
		var internalId = original.InternalID;
		var galleryPath = Publish("catalogue", "gallery.html");
		var removedPath = Publish(removed.CMSProperties.CustomSlug, "removed.html");
		var retainedItem = _repository.GetCMSItem(retained.CMSProperties.CustomSlug);
		var retainedPath = Publish(retainedItem.Slug, "retained.html");
		original.Dirty = retainedItem.Dirty = false;

		Assert.That(() => _repository.RemoveResource(removed.ID, false), Throws.Nothing);

		// The remaining collection stays routable until its dirty output is regenerated.
		Assert.That(_repository.ContainsResource(removed.ID), Is.False);
		Assert.That(_repository.ContainsCmsItem(removed.CMSProperties.CustomSlug), Is.False);
		Assert.That(File.Exists(removedPath), Is.False);
		Assert.That(_repository.TryGetCMSItem("catalogue", out var gallery), Is.True);
		Assert.That(gallery.Parts, Is.EqualTo(new[] { retained.ID }));
		Assert.That(gallery.Keywords, Is.EqualTo(new[] { retained.Title.ToLowerInvariant() }));
		Assert.That(gallery.Dirty, Is.True);
		Assert.That(gallery.HeaderID, Is.EqualTo(headerId));
		Assert.That(gallery.InternalID, Is.EqualTo(internalId));
		Assert.That(gallery.RenderPath, Is.EqualTo("cms/gallery.html"));
		Assert.That(File.ReadAllText(galleryPath), Is.EqualTo("published catalogue"));
		Assert.That(_repository.GetCMSItem(retainedItem.Slug), Is.SameAs(retainedItem));
		Assert.That(retainedItem.Dirty, Is.False);
		Assert.That(File.ReadAllText(retainedPath), Is.EqualTo("published " + retainedItem.Slug));
	}

	[Test]
	public void RemovingLastCardRemovesEmptyGalleryAndItsOutputs() {
		var card = AddPage(CMSPageType.Gallery, "catalogue/only", "Only");
		var galleryPath = Publish("catalogue", "gallery.html");
		var cardPath = Publish(card.CMSProperties.CustomSlug, "card.html");

		Assert.That(() => _repository.RemoveResource(card.ID, false), Throws.Nothing);

		Assert.That(_repository.ContainsResource(card.ID), Is.False);
		Assert.That(_repository.ContainsCmsItem(card.CMSProperties.CustomSlug), Is.False);
		Assert.That(_repository.ContainsCmsItem("catalogue"), Is.False);
		Assert.That(File.Exists(cardPath), Is.False);
		Assert.That(File.Exists(galleryPath), Is.False);
	}

	[Test]
	public void RemovingNestedCardRefreshesCategoryAndPreservesOtherGallery([Values] bool lastCard) {
		// The category contains this gallery and another independent collection.
		AddPage(CMSPageType.Page, "products/overview", "Overview");
		var removed = AddPage(CMSPageType.Gallery, "products/tools/removed", "Removed");
		var retained = lastCard ? null : AddPage(CMSPageType.Gallery, "products/tools/retained", "Retained");
		var unrelated = AddPage(CMSPageType.Gallery, "products/other/unrelated", "Unrelated");
		var category = _repository.GetCMSItem("products");
		var expectedParts = category.Parts.Where(x => x != removed.ID).ToArray();
		var categoryPath = Publish("products", "category.html");
		var toolsPath = Publish("products/tools", "tools.html");
		var unrelatedItem = _repository.GetCMSItem("products/other");
		var unrelatedPath = Publish(unrelatedItem.Slug, "other.html");
		category.Dirty = unrelatedItem.Dirty = false;

		Assert.That(() => _repository.RemoveResource(removed.ID, false), Throws.Nothing);

		// Container metadata is recalculated from the remaining native resources.
		var updatedCategory = _repository.GetCMSItem("products");
		Assert.That(updatedCategory.Parts, Is.EqualTo(expectedParts));
		Assert.That(updatedCategory.Keywords, Is.EqualTo(new[] { "overview" }), "Category metadata retains its authored overview instead of absorbing nested gallery cards.");
		Assert.That(updatedCategory.Dirty, Is.True);
		Assert.That(File.ReadAllText(categoryPath), Is.EqualTo("published products"));
		Assert.That(_repository.ContainsCmsItem("products/tools"), Is.EqualTo(!lastCard));
		if (lastCard)
			Assert.That(File.Exists(toolsPath), Is.False);
		else {
			Assert.That(_repository.GetCMSItem("products/tools").Parts, Is.EqualTo(new[] { retained.ID }));
			Assert.That(File.ReadAllText(toolsPath), Is.EqualTo("published products/tools"));
		}
		Assert.That(_repository.GetCMSItem(unrelatedItem.Slug), Is.SameAs(unrelatedItem));
		Assert.That(unrelatedItem.Dirty, Is.False);
		Assert.That(unrelatedItem.Parts, Is.EqualTo(new[] { unrelated.ID }));
		Assert.That(File.ReadAllText(unrelatedPath), Is.EqualTo("published products/other"));
	}

	private LocalNotionPage AddPage(CMSPageType type, string slug, string title, string[] tags = null) {
		var id = Guid.NewGuid().ToString();
		_repository.AddObject(new Page {
			Id = id,
			Parent = new PageParent { PageId = _repository.CMSDatabaseID },
			Properties = new Dictionary<string, PropertyValue> { ["Name"] = new TitlePropertyValue { Id = "title", Title = [Text(title)] } }
		});
		_repository.AddResourceGraph(new NotionObjectGraph { ObjectID = id });
		var page = new LocalNotionPage {
			ID = id,
			Name = "page-" + id,
			Title = title,
			ParentResourceID = _repository.CMSDatabaseID,
			Keywords = [title.ToLowerInvariant()],
			CMSProperties = new CMSProperties { PageType = type, Status = CMSPageStatus.Published, CustomSlug = slug, Themes = [], Tags = tags ?? [] }
		};
		_repository.AddResource(page);
		return page;
	}

	private string Publish(string slug, string filename) {
		var item = _repository.GetCMSItem(slug);
		item.RenderPath = "cms/" + filename;
		var path = Path.Combine(_fixtureRoot, item.RenderPath);
		Directory.CreateDirectory(Path.GetDirectoryName(path));
		File.WriteAllText(path, "published " + slug);
		return path;
	}

	private static RichTextText Text(string text) => new() { PlainText = text, Text = new Text { Content = text } };
}
