// Copyright (c) Herman Schoenfeld 2018 - Present. All rights reserved. (https://sphere10.com/products/localnotion)
// Author: Herman Schoenfeld <herman@sphere10.com>
//
// Distributed under the GPLv3 software license, see the accompanying file LICENSE
// or visit https://github.com/HermanSchoenfeld/localnotion/blob/master/LICENSE
//
// This notice must not be removed when duplicating this file or its contents, in whole or in part.

using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Xml.Linq;
using NUnit.Framework;
using Sphere10.Framework;

namespace LocalNotion.Core.Tests;

[TestFixture]
[Parallelizable(ParallelScope.Children)]
[FixtureLifeCycle(LifeCycle.InstancePerTestCase)]
public class SitemapGeneratorTests {

	private string _root;
	private CMSLocalNotionRepository _repository;

	[TearDown]
	public void TearDown() {
		_repository?.Dispose();
		if (_root != null && Path.GetFullPath(_root).StartsWith(Path.GetFullPath(Path.GetTempPath()), StringComparison.OrdinalIgnoreCase))
			Directory.Delete(_root, recursive: true);
	}

	[TestCase("/", "example.invalid", "https://example.invalid/")]
	[TestCase("", "example.invalid", "https://example.invalid/")]
	[TestCase("/Docs/", "example.invalid", "https://example.invalid/Docs/")]
	[TestCase("/", "http://example.invalid:8080", "http://example.invalid:8080/")]
	[TestCase("https://published.invalid/Docs", "CMS", "https://published.invalid/Docs/")]
	[TestCase("http://published.invalid:8080/Docs/", "CMS", "http://published.invalid:8080/Docs/")]
	public async Task GeneratesAbsoluteHomepageAndNestedUrls(string baseUrl, string title, string expectedRoot) {
		await CreateRepository(baseUrl, title);
		AddItem("");
		AddItem("cases/telstra");
		var sitemap = new SitemapGenerator().Generate(_repository);
		Assert.That(sitemap.Nodes.Select(node => node.Url), Is.EqualTo(new[] { expectedRoot, expectedRoot + "cases/telstra" }));
		Assert.That(sitemap.Nodes.All(node => node.LastModified == null), Is.True);
	}

	[Test]
	public async Task SerializedSitemapContainsAbsoluteEncodedUnicodeUrls() {
		await CreateRepository();
		AddItem("");
		AddItem("مقالات/توجيه");
		Directory.CreateDirectory(Path.Combine(_root, "cms"));
		await new NginxMappingsGenerator(_repository).GenerateSiteMap();
		var document = XDocument.Load(Path.Combine(_root, "cms", "sitemap.xml"));
		XNamespace ns = "http://www.sitemaps.org/schemas/sitemap/0.9";
		Assert.That(document.Root.Name, Is.EqualTo(ns + "urlset"));
		Assert.That(document.Descendants(ns + "loc").Select(node => node.Value), Is.EqualTo(new[] {
			"https://example.invalid/", "https://example.invalid/%D9%85%D9%82%D8%A7%D9%84%D8%A7%D8%AA/%D8%AA%D9%88%D8%AC%D9%8A%D9%87"
		}));
	}

	[Test]
	public async Task PreservesLastModifiedAndExcludesPrivateAncestors() {
		await CreateRepository();
		var first = AddPage("public", new DateTime(2026, 11, 1));
		var second = AddPage("public", new DateTime(2026, 11, 2));
		AddItem("public", first.ID, second.ID);
		var privatePage = AddPage("private", new DateTime(2026, 11, 3), Constants.TagPrivate);
		var child = AddPage("private/child", new DateTime(2026, 11, 4));
		AddItem("private", privatePage.ID);
		AddItem("private/child", child.ID);
		var sitemap = new SitemapGenerator().Generate(_repository);
		Assert.That(sitemap.Nodes, Has.Length.EqualTo(1));
		Assert.That(sitemap.Nodes[0].Url, Is.EqualTo("https://example.invalid/public"));
		Assert.That(sitemap.Nodes[0].LastModified, Is.EqualTo(second.LastEditedOn));
	}

	private async Task CreateRepository(string baseUrl = "/", string title = "example.invalid") {
		_root = Path.Combine(Path.GetTempPath(), "localnotion-sitemap-" + Guid.NewGuid().ToString("N"));
		Directory.CreateDirectory(_root);
		var databaseID = Guid.NewGuid().ToString();
		_repository = (CMSLocalNotionRepository)await LocalNotionRepository.CreateNew(_root, cmsDatabaseID: databaseID, pathProfile: new LocalNotionPathProfile { BaseUrl = baseUrl }, logger: new NoOpLogger());
		_repository.AddResource(new LocalNotionDatabase {
			ID = databaseID, PrimaryDataSourceID = Guid.NewGuid().ToString(), Name = "example-invalid", Title = title
		});
	}

	private void AddItem(string slug, params string[] parts)
		=> _repository.AddOrUpdateCMSItem(new CMSItem { Slug = slug, Parts = parts });

	private LocalNotionPage AddPage(string slug, DateTime lastEdited, params string[] tags) {
		var page = new LocalNotionPage {
			ID = Guid.NewGuid().ToString(), ParentResourceID = _repository.CMSDatabaseID, Name = Guid.NewGuid().ToString(), Title = slug, LastEditedOn = lastEdited
		};
		_repository.AddResource(page);
		page.CMSProperties = new CMSProperties { CustomSlug = slug, PageType = CMSPageType.Page, Status = CMSPageStatus.Published, Tags = tags };
		return page;
	}
}
