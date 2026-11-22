// Copyright (c) Herman Schoenfeld 2018 - Present. All rights reserved. (https://sphere10.com/products/localnotion)
// Author: Herman Schoenfeld <herman@sphere10.com>
//
// Distributed under the GPLv3 software license, see the accompanying file LICENSE
// or visit https://github.com/HermanSchoenfeld/localnotion/blob/master/LICENSE
//
// This notice must not be removed when duplicating this file or its contents, in whole or in part.

using System;
using System.Collections.Generic;
using System.Linq;
using LocalNotion.Core;
using Notion.Client;
using NUnit.Framework;

namespace LocalNotion.Core.Tests;

[TestFixture]
[Category("Unit")]
[Parallelizable(ParallelScope.Children)]
public class CMSContentNodeTests {
	[Test]
	public void SectionsUseSectionedPageRegardlessOfCount([Values(1, 2)] int sectionCount, [Values(false, true)] bool includeFraming) {
		var node = new CMSContentNode { Slug = "services/engineering" };
		for (var index = 0; index < sectionCount; index++)
			node.Content.Add(CreateResource(CMSPageType.Section, []));
		if (includeFraming) {
			node.Content.Add(CreateResource(CMSPageType.Header, []));
			node.Content.Add(CreateResource(CMSPageType.Footer, []));
		}

		Assert.That(node.Type, Is.EqualTo(CMSContentType.SectionedPage), "The first downloaded Section needs the same renderer theme as the complete group.");
	}

	[Test]
	public void SingleStandalonePageRetainsPageType([Values(CMSPageType.Page, CMSPageType.Gallery)] CMSPageType pageType) {
		var node = new CMSContentNode();
		node.Content.Add(CreateResource(pageType, []));

		Assert.That(node.Type, Is.EqualTo(CMSContentType.Page));
	}

	[Test]
	public void OrdinaryPageAndSectionAtSameSlugRemainInvalid([Values(false, true)] bool sectionFirst) {
		var node = new CMSContentNode { Slug = "services/engineering" };
		node.Content.Add(CreateResource(sectionFirst ? CMSPageType.Section : CMSPageType.Page, []));
		node.Content.Add(CreateResource(sectionFirst ? CMSPageType.Page : CMSPageType.Section, []));

		Assert.That(node.Type, Is.EqualTo(CMSContentType.None), "A colliding ordinary Page must be retired before its replacement Section group can be published.");
	}

	[Test]
	public void FramingPropertyParsingRetainsNativeTags(
		[Values(CMSPageType.Header, CMSPageType.NavBar, CMSPageType.Footer, CMSPageType.Internal)] CMSPageType pageType
	) {
		var page = CreatePage(pageType, [" " + Constants.TagUseParentHeader + " ", "featured"]);

		var properties = CMSHelper.ParseCMSProperties("settings", page);

		Assert.That(properties.Tags, Is.EqualTo(new[] { Constants.TagUseParentHeader, "featured" }));
		Assert.That(properties.Summary, Is.Empty, "Framing metadata still excludes article-summary content.");
	}

	[Test]
	public void GalleryRetainsOwnChildAndCategoryTags() {
		var gallery = CreateGallery([Constants.TagUseParentHeader, "featured"]);

		Assert.That(gallery.Type, Is.EqualTo(CMSContentType.Gallery));
		Assert.That(gallery.Tags, Is.EquivalentTo(new[] { "first", "second", Constants.TagUseParentHeader, "featured", "Catalogue" }));
		Assert.That(gallery.Header, Is.SameAs(gallery.Parent.Header));
	}

	[Test]
	public void GalleryHideHeaderTakesPrecedenceOverParentHeader() {
		var gallery = CreateGallery([Constants.TagUseParentHeader, Constants.TagHideHeader]);

		Assert.That(gallery.Parent.Header, Is.Not.Null);
		Assert.That(gallery.Header, Is.Null);
	}

	[Test]
	public void GalleryWithoutParentHeaderTagDoesNotInheritImplicitly() {
		var gallery = CreateGallery([]);

		Assert.That(gallery.Parent.Header, Is.Not.Null);
		Assert.That(gallery.Header, Is.Null);
	}

	[Test]
	public void GalleryParentHeaderTagWithoutParentReturnsNoHeader() {
		var gallery = CreateGallery([Constants.TagUseParentHeader]);
		gallery.Parent = null;

		Assert.That(gallery.Header, Is.Null);
	}

	private static CMSContentNode CreateGallery(string[] tags) {
		// Keep container control tags separate from the cards' filtering tags.
		var parent = new CMSContentNode();
		parent.Content.Add(CreateResource(CMSPageType.Header, []));
		var gallery = new CMSContentNode { Slug = "catalogue", Parent = parent };
		var settings = CreateResource(CMSPageType.Internal, tags);
		settings.CMSProperties.Root = "Catalogue";
		gallery.Content.Add(settings);
		foreach (var tag in new[] { "first", "second" }) {
			var card = new CMSContentNode { Slug = "catalogue/" + tag, Parent = gallery };
			card.Content.Add(CreateResource(CMSPageType.Gallery, [tag]));
			gallery.Children.Add(card);
		}
		parent.Children.Add(gallery);
		return gallery;
	}

	private static LocalNotionPage CreateResource(CMSPageType pageType, string[] tags) => new() {
		ID = Guid.NewGuid().ToString(),
		CMSProperties = new CMSProperties { PageType = pageType, Status = CMSPageStatus.Published, Tags = tags, Themes = [] }
	};

	private static Page CreatePage(CMSPageType pageType, string[] tags) {
		// Use native Notion property values so the parser-to-framing path is tested.
		var properties = new Dictionary<string, PropertyValue> {
			[Constants.TitlePropertyName] = new TitlePropertyValue { Title = [Text("Settings")] },
			[Constants.PageTypePropertyName] = new SelectPropertyValue { Select = new SelectOption { Name = pageType.ToString() } },
			[Constants.PublishOnPropertyName] = new DatePropertyValue(),
			[Constants.StatusPropertyName] = new StatusPropertyValue { Status = new StatusPropertyValue.Data { Name = "Published" } },
			[Constants.ThemesPropertyName] = new MultiSelectPropertyValue { MultiSelect = [] },
			[Constants.SlugPropertyName] = new RichTextPropertyValue { RichText = [] },
			[Constants.SequencePropertyName] = new NumberPropertyValue(),
			[Constants.RootCategoryPropertyName] = new SelectPropertyValue { Select = new SelectOption { Name = "Catalogue" } },
			[Constants.Category1PropertyName] = new SelectPropertyValue(),
			[Constants.Category2PropertyName] = new SelectPropertyValue(),
			[Constants.Category3PropertyName] = new SelectPropertyValue(),
			[Constants.Category4PropertyName] = new SelectPropertyValue(),
			[Constants.Category5PropertyName] = new SelectPropertyValue(),
			[Constants.TagsPropertyName] = new MultiSelectPropertyValue { MultiSelect = tags.Select(tag => new SelectOption { Name = tag }).ToList() },
			[Constants.SummaryPropertyName] = new RichTextPropertyValue { RichText = [Text("Not an article summary")] },
			[Constants.AuthenticationPropertyName] = new RichTextPropertyValue { RichText = [] },
			[Constants.CreatedByPropertyName] = new CreatedByPropertyValue(),
			[Constants.CreatedOnPropertyName] = new CreatedTimePropertyValue(),
			[Constants.EditedByPropertyName] = new LastEditedByPropertyValue(),
			[Constants.EditedOnPropertyName] = new LastEditedTimePropertyValue()
		};
		return new Page { Id = Guid.NewGuid().ToString(), Properties = properties };
	}

	private static RichTextText Text(string text) => new() { PlainText = text, Text = new Text { Content = text } };
}
