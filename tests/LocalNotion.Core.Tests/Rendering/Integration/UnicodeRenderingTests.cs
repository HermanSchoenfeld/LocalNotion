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
using Notion.Client;
using NUnit.Framework;
using Sphere10.Framework;

namespace LocalNotion.Core.Tests;

[TestFixture]
[Category("Integration")]
[Parallelizable(ParallelScope.Children)]
public class UnicodeRenderingTests {

	[TestCase(LocalNotionMode.Online)]
	[TestCase(LocalNotionMode.Offline)]
	public async Task UnicodeArticlesRenderWithLinksPaginationAndHostingMappings(LocalNotionMode mode) {
		var root = Path.Combine(Path.GetTempPath(), "localnotion-unicode-rendering-" + Guid.NewGuid().ToString("N"));
		Directory.CreateDirectory(root);
		using var cleanup = Tools.Scope.ExecuteOnDispose(() => Directory.Delete(root, recursive: true));
		var databaseID = Guid.NewGuid().ToString();
		using var repository = (CMSLocalNotionRepository)await LocalNotionRepository.CreateNew(root, cmsDatabaseID: databaseID,
			pathProfile: new LocalNotionPathProfile { Mode = mode, BaseUrl = "https://example.invalid/" }, logger: new NoOpLogger());
		repository.AddObject(new Database { Id = databaseID });
		repository.AddResourceGraph(new NotionObjectGraph { ObjectID = databaseID });
		repository.AddResource(new LocalNotionDatabase {
			ID = databaseID, PrimaryDataSourceID = Guid.NewGuid().ToString(), Name = "example.invalid", Title = "CMS"
		});
		var pages = Enumerable.Range(1, 21).Select(index => AddPage(repository, databaseID, index)).ToArray();
		var listing = new CMSItem {
			ItemType = CMSItemType.CategoryPage, Slug = "مقالات", Title = "مقالات", Parts = pages.Select(page => page.ID).ToArray(), Dirty = true
		};
		var article = new CMSItem {
			ItemType = CMSItemType.Page, Slug = pages[0].CMSProperties.CustomSlug, Title = pages[0].Title, Parts = [pages[0].ID], Dirty = true
		};
		repository.AddOrUpdateCMSItem(listing);
		repository.AddOrUpdateCMSItem(article);
		var manager = new RenderingManager(repository);
		manager.PrepareRenderPaths(pages.Select(page => page.ID), cmsItems: [listing, article]);
		var rawPath = manager.RenderLocalResource(pages[0].ID, RenderType.HTML, RenderMode.ReadOnly);
		manager.RenderCMSItem(listing);
		manager.RenderCMSItem(article);

		// Verify actual default/CMS templates and Unicode filenames, not only model assignments.
		var parser = new HtmlParser();
		Assert.That(Path.GetFileName(rawPath), Does.Contain("توجيه"));
		Assert.That(pages[0].Renders[RenderType.HTML].Slug, Does.EndWith("/توجيه"));
		Assert.That(parser.ParseDocument(await File.ReadAllTextAsync(rawPath)).Body.TextContent, Does.Contain("توجيه"));
		Assert.That(listing.ListingRenderPaths, Has.Length.EqualTo(2));
		Assert.That(article.Slug, Is.EqualTo("مقالات/تشفير/توجيه"));
		var listingHtml = parser.ParseDocument(await File.ReadAllTextAsync(Path.Combine(root, listing.RenderPath)));
		var links = listingHtml.QuerySelectorAll("a[href]").Select(link => Uri.UnescapeDataString(link.GetAttribute("href"))).ToArray();
		if (mode == LocalNotionMode.Online) {
			Assert.That(links, Does.Contain("https://example.invalid/مقالات/تشفير/توجيه"));
			Assert.That(links, Does.Contain("https://example.invalid/مقالات?page=2"));
		} else {
			Assert.That(links, Does.Contain(Path.GetFileName(article.RenderPath)));
			Assert.That(links, Does.Contain(Path.GetFileName(listing.ListingRenderPaths[1])));
		}

		var generator = new NginxMappingsGenerator(repository);
		await generator.GenerateNGinxFolder();
		await generator.GenerateUrlMappingsFile();
		var mappings = await File.ReadAllTextAsync(NginxMappingsGenerator.CalculateMappingFile(repository));
		Assert.That(mappings, Does.Contain("location = /مقالات/تشفير/توجيه"));
		Assert.That(mappings, Does.Contain("location = \"/مقالات\""));
		Assert.That(mappings, Does.Contain("$arg_page = \"2\""));
		Assert.That(mappings, Does.Contain(listing.ListingRenderPaths[1]));
		var sitemap = new SitemapGenerator().Generate(repository);
		Assert.That(sitemap.Nodes.Select(node => node.Url), Does.Contain(new Uri("https://example.invalid/مقالات/تشفير/توجيه").AbsoluteUri));

		// Optional synthetic export supports real server checks without exposing a registry or source content.
		var export = TestContext.Parameters.Get("UnicodeHostingFixturePath", "");
		if (mode == LocalNotionMode.Online && !string.IsNullOrEmpty(export)) {
			Assert.That(Path.IsPathFullyQualified(export), Is.True);
			Assert.That(Directory.Exists(export), Is.False);
			foreach (var path in listing.ListingRenderPaths.Append(article.RenderPath).Append(pages[0].Renders[RenderType.HTML].LocalPath)) {
				var target = Path.Combine(export, path);
				Directory.CreateDirectory(Path.GetDirectoryName(target));
				File.Copy(Path.Combine(root, path), target);
			}
			await File.WriteAllTextAsync(Path.Combine(export, "ln-urls.conf"), mappings);
		}
	}

	private static LocalNotionPage AddPage(CMSLocalNotionRepository repository, string databaseID, int index) {
		var title = index == 1 ? "توجيه" : "Article " + index;
		var native = new Page {
			Id = Guid.NewGuid().ToString(), Parent = new DatabaseParent { DatabaseId = databaseID },
			CreatedTime = new DateTime(2026, 1, 1), LastEditedTime = new DateTime(2026, 1, 2),
			Properties = new Dictionary<string, PropertyValue> { ["Name"] = new TitlePropertyValue {
				Title = [new RichTextText { PlainText = title, Text = new() { Content = title } }]
			} }
		};
		var page = LocalNotionHelper.ParsePage(native);
		page.ParentResourceID = databaseID;
		repository.AddObject(native);
		repository.AddResourceGraph(new NotionObjectGraph { ObjectID = page.ID, Children = [] });
		repository.AddResource(page);
		page.CMSProperties = new CMSProperties {
			PageType = CMSPageType.Page, Root = "مقالات", Category1 = "تشفير", Status = CMSPageStatus.Published,
			Summary = "Article summary " + index, Sequence = index, Themes = [], Tags = []
		};
		page.CMSProperties.CustomSlug = CMSHelper.CalculatePageSlug(title, page.CMSProperties);
		return page;
	}
}
