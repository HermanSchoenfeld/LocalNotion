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
using LocalNotion.Core;
using Newtonsoft.Json;
using Notion.Client;
using NUnit.Framework;

namespace LocalNotion.Core.Tests;

[TestFixture]
[Category("Unit")]
[Parallelizable(ParallelScope.Children)]
public class BreadCrumbGeneratorTests {

	[Test]
	public void FramingRawRenderDoesNotChangeAncestorCategoryLabel(
		[Values(CMSPageType.Internal, CMSPageType.Header, CMSPageType.NavBar, CMSPageType.Footer)] CMSPageType framingType,
		[Values] bool hasRawRender
	) {
		// A raw export gives configuration a slug alias without making it public content.
		var fixture = CreateFixture();
		AddPage(fixture, "Gallery Properties", "tech", framingType, hasRawRender);

		var trail = Calculate(fixture).Trail;
		Assert.That(trail.Select(item => item.Text), Is.EqualTo(new[] { "Our Tech", "Dynamic Merkle-Trees" }));
		Assert.That(trail[0].Url, Is.EqualTo("/tech"));
		Assert.That(trail[1].Traits.HasFlag(BreadCrumbItemTraits.IsCurrentPage), Is.True);
	}

	[Test]
	public void RenderedPublicAncestorKeepsItsOwnTitle(
		[Values(CMSPageType.Page, CMSPageType.Gallery)] CMSPageType pageType,
		[Values] bool includeRenderedInternal
	) {
		// Configuration may be the first slug result, before the genuine published Page.
		var fixture = CreateFixture();
		if (includeRenderedInternal)
			AddPage(fixture, "Gallery Properties", "tech", CMSPageType.Internal, true);
		AddPage(fixture, "Technology Library", "tech", pageType, true);

		var trail = Calculate(fixture).Trail;
		Assert.That(trail.Select(item => item.Text), Is.EqualTo(new[] { "Technology Library", "Dynamic Merkle-Trees" }));
		Assert.That(trail[0].Url, Is.EqualTo("https://example.test/tech"));
	}

	[Test]
	public void FramingTitlesDoNotReplaceNestedCategoryLabels() {
		// Each unmatched public ancestor keeps the corresponding authored category label.
		var fixture = CreateFixture();
		var article = fixture.ResourceMap.Values.OfType<LocalNotionPage>().Single();
		article.CMSProperties.CustomSlug = "tech/cryptography/dynamic-merkle-trees";
		article.CMSProperties.Category1 = "Cryptography";
		AddPage(fixture, "Global Gallery Settings", "tech", CMSPageType.Internal, true);
		AddPage(fixture, "Cryptography Settings", "tech/cryptography", CMSPageType.Internal, true);

		var trail = Calculate(fixture).Trail;
		Assert.That(trail.Select(item => item.Text), Is.EqualTo(new[] { "Our Tech", "Cryptography", "Dynamic Merkle-Trees" }));
		Assert.That(trail.Select(item => item.Url), Is.EqualTo(new[] { "/tech", "/tech/cryptography", string.Empty }));
	}

	private static FixtureRepository CreateFixture() {
		var fixture = new FixtureRepository(new PathResolver(Path.GetTempPath(), new LocalNotionPathProfile {
			Mode = LocalNotionMode.Online,
			BaseUrl = "https://example.test"
		}));
		var database = new LocalNotionDatabase {
			ID = Guid.NewGuid().ToString(),
			Title = "CMS",
			Properties = JsonConvert.DeserializeObject<Dictionary<string, DataSourcePropertyConfig>>(
				JsonConvert.SerializeObject(new CMSDatabaseTemplate().CreateProperties())
			)
		};
		fixture.ResourceMap.Add(database.ID, database);
		AddPage(fixture, "Dynamic Merkle-Trees", "tech/dynamic-merkle-trees", CMSPageType.Gallery, false);
		return fixture;
	}

	private static LocalNotionPage AddPage(FixtureRepository fixture, string title, string slug, CMSPageType pageType, bool hasRawRender) {
		var page = new LocalNotionPage {
			ID = Guid.NewGuid().ToString(),
			Title = title,
			ParentResourceID = fixture.ResourceMap.Values.OfType<LocalNotionDatabase>().Single().ID,
			CMSProperties = new CMSProperties { PageType = pageType, Status = CMSPageStatus.Published, CustomSlug = slug, Root = "Our Tech" }
		};
		if (hasRawRender)
			page.Renders[RenderType.HTML] = new RenderEntry { LocalPath = "pages/" + page.ID + ".html", Slug = slug };
		fixture.ResourceMap.Add(page.ID, page);
		return page;
	}

	private static BreadCrumb Calculate(FixtureRepository fixture) {
		var repository = new BreadcrumbRepository(fixture);
		var article = fixture.ResourceMap.Values.OfType<LocalNotionPage>().First();
		return new BreadCrumbGenerator(repository, new OnlineLinkGenerator(repository)).CalculateBreadcrumb(article);
	}

	private sealed class BreadcrumbRepository(FixtureRepository fixture) : LocalNotionRepositoryDecorator(fixture) {
		public override IEnumerable<LocalNotionResource> Resources => fixture.ResourceMap.Values;

		public override bool TryGetResource(string resourceID, out LocalNotionResource resource) {
			resource = null;
			return resourceID != null && fixture.TryGetResource(resourceID, out resource);
		}
	}
}
