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
using System.Threading.Tasks;
using Notion.Client;
using NUnit.Framework;

namespace LocalNotion.Core.Tests;

[TestFixture]
[Category("Unit")]
[Parallelizable(ParallelScope.Children)]
public class ResourceNameTests {

	[Test]
	public void CmsArticleRoutesPreserveCSharpLanguageName() {
		const string title = "How to write a custom binary serializer in C#";
		const string name = "how-to-write-a-custom-binary-serializer-in-c-sharp";
		const string parent = "articles/coding/sphere-10-framework/serialization";
		const string expected = parent + "/" + name;
		Assert.That(CMSHelper.CalculatePageSlug(title, new CMSProperties {
			PageType = CMSPageType.Page, Root = "Articles", Category1 = "Coding", Category2 = "Sphere 10 Framework", Category3 = "Serialization"
		}), Is.EqualTo(expected));
		Assert.That(CMSHelper.CalculateNestedPageSlug(CMSPageType.Page, parent, title), Is.EqualTo(expected));
		Assert.That(CMSHelper.CalculateNestedPageSlug(CMSPageType.Section, parent + "#section", title), Is.EqualTo(expected));
		Assert.That(CMSHelper.CalculateSlug(["Articles", "C#", title]), Is.EqualTo("articles/c-sharp/" + name));
		Assert.That(LocalNotionHelper.SanitizeSlug(expected), Is.EqualTo(expected));
	}

	[TestCase("📋")]
	[TestCase("---")]
	[TestCase("   ")]
	public void TitlesWithoutSlugCharactersUseStableDistinctNames(string title) {
		var firstID = Guid.NewGuid();
		var secondID = Guid.NewGuid();
		var first = LocalNotionHelper.CalculatePageName(firstID.ToString("D"), title);
		Assert.That(first, Is.Not.Empty);
		Assert.That(first, Is.EqualTo(LocalNotionHelper.CalculatePageName(firstID.ToString("N"), title)), "ID formatting must not change the fallback name.");
		Assert.That(first, Is.Not.EqualTo(LocalNotionHelper.CalculatePageName(secondID.ToString("D"), title)));
		Assert.That(LocalNotionHelper.SanitizeSlug(first), Is.EqualTo(first), "The fallback must survive render-slug normalization.");
	}

	[TestCase("Hello World", "hello-world")]
	[TestCase("123", "123")]
	public void ExistingResourceNamesStayUnchanged(string title, string expected)
		=> Assert.That(LocalNotionHelper.CalculatePageName(Guid.NewGuid().ToString(), title), Is.EqualTo(expected));

	[TestCase("بريد صادر ", "بريد-صادر")]
	[TestCase("توجيه", "توجيه")]
	[TestCase("日本語", "日本語")]
	[TestCase("Café", "café")]
	[TestCase("Cafe\u0301", "café")]
	[TestCase("Report توجيه", "report-توجيه")]
	[TestCase("𐐀", "𐐨")]
	public void ResourceNamesPreserveUnicode(string title, string expected) {
		var name = LocalNotionHelper.CalculatePageName(Guid.NewGuid().ToString(), title);
		Assert.That(name, Is.EqualTo(expected));
		Assert.That(LocalNotionHelper.SanitizeSlug(name), Is.EqualTo(expected));
	}

	[Test]
	public void CmsPathsPreserveUnicodeAtEveryLevel() {
		const string expected = "مقالات/تشفير/café";
		Assert.That(CMSHelper.CalculateSlug(["مقالات", "تشفير", "Café"]), Is.EqualTo(expected));
		Assert.That(LocalNotionHelper.SanitizeSlug("/مقالات/تشفير/Cafe\u0301/"), Is.EqualTo(expected));
		Assert.That(CMSHelper.CalculateNestedPageSlug(CMSPageType.Page, "مقالات/تشفير", "Café"), Is.EqualTo(expected));
		Assert.That(CMSHelper.CalculateNestedPageSlug(CMSPageType.Section, "مقالات/تشفير#section", "Café"), Is.EqualTo(expected));
		Assert.That(CMSHelper.CalculatePageSlug("Café", new CMSProperties {
			PageType = CMSPageType.Page, Root = "مقالات", Category1 = "تشفير"
		}), Is.EqualTo(expected));
	}

	[Test]
	[Category("Integration")]
	public async Task ArabicPageAndDatabaseCanBeSavedAndReopened() {
		var root = Path.Combine(Path.GetTempPath(), "localnotion-resource-names-" + Guid.NewGuid().ToString("N"));
		Directory.CreateDirectory(root);
		using var cleanup = Tools.Scope.ExecuteOnDispose(() => Directory.Delete(root, recursive: true));
		var database = LocalNotionHelper.ParseDatabase(new Database {
			Id = Guid.NewGuid().ToString(), Title = [Text("بريد صادر ")],
			DataSources = [new DataSourceReferenceResponse { DataSourceId = Guid.NewGuid().ToString() }]
		}, new DataSource { Properties = new Dictionary<string, DataSourcePropertyConfig>(), Description = [] });
		var page = LocalNotionHelper.ParsePage(new Page {
			Id = Guid.NewGuid().ToString(),
			Properties = new Dictionary<string, PropertyValue> { ["Name"] = new TitlePropertyValue { Title = [Text("توجيه")] } }
		});
		page.ParentResourceID = database.ID;
		using (var repository = await LocalNotionRepository.CreateNew(root)) {
			repository.AddResource(database);
			repository.AddResource(page);
			await repository.SaveAsync();
		}

		using var reopened = await LocalNotionRepository.Open(root);
		Assert.That(reopened.GetResource(database.ID).Title, Is.EqualTo("بريد صادر "));
		Assert.That(reopened.GetResource(page.ID).Title, Is.EqualTo("توجيه"));
		Assert.That(reopened.GetPage(page.ID).Name, Is.EqualTo(page.Name));
		Assert.That(reopened.GetPage(page.ID).ParentResourceID, Is.EqualTo(database.ID));
	}

	private static RichTextText Text(string value) => new() { PlainText = value, Text = new() { Content = value } };
}
