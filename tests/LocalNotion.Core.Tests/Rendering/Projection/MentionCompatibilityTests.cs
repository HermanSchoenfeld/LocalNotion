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
using Newtonsoft.Json;
using Notion.Client;
using NUnit.Framework;
using R = Sphere10.Framework.Web.VisualRenderer;

namespace LocalNotion.Core.Tests;

[TestFixture]
[Category("Unit")]
[Parallelizable(ParallelScope.Children)]
public class MentionCompatibilityTests {

	[TestCase("link_mention", ProjectionPurpose.Html)]
	[TestCase("link_mention", ProjectionPurpose.Text)]
	[TestCase("link_preview", ProjectionPurpose.Html)]
	[TestCase("link_preview", ProjectionPurpose.Text)]
	[TestCase("future_mention", ProjectionPurpose.Html)]
	[TestCase("future_mention", ProjectionPurpose.Text)]
	public void ExternalMentionsRetainTheirLabelWithoutRequiringNotionObjectIDs(string mentionType, ProjectionPurpose purpose) {
		// Exercise the SDK's real rich-text converter, including the unmodelled external payload.
		var block = JsonConvert.DeserializeObject<IBlock>($$"""
			{
			  "object": "block", "id": "mention-block", "type": "paragraph",
			  "paragraph": { "rich_text": [{
			    "type": "mention", "plain_text": "External document",
			    "href": "https://example.invalid/document",
			    "mention": { "type": "{{mentionType}}", "{{mentionType}}": {
			      "href": "https://example.invalid/document", "title": "External document"
			    } }
			  }] }
			}
			""");
		var mention = (RichTextMention)((ParagraphBlock)block).Paragraph.RichText.Single();
		Assert.That(mention.Mention.GetObjectID(), Is.Null, "An external mention is not a Notion object to fetch.");

		var resource = new LocalNotionPage { ID = Guid.NewGuid().ToString(), Name = "mentions", Title = "Mentions" };
		var page = new Page { Id = resource.ID };
		var graph = new NotionObjectGraph { ObjectID = page.Id, Children = [new() { ObjectID = block.Id, Children = [] }] };
		var objects = new Dictionary<string, IObject> { [page.Id] = page, [block.Id] = block };
		var root = Path.Combine(Path.GetTempPath(), "localnotion-mentions-" + Guid.NewGuid().ToString("N"));
		Directory.CreateDirectory(root);
		using var cleanup = Tools.Scope.ExecuteOnDispose(() => Directory.Delete(root, recursive: true));
		var repository = new FixtureRepository(new PathResolver(root, new LocalNotionPathProfile()));
		repository.ResourceMap[resource.ID] = resource;
		var document = new NotionRenderModelBuilder(repository).Build(resource, graph, objects, root, purpose: purpose);
		Assert.That(new R.TextRenderer().Render(document), Does.Contain("External document"));
		if (purpose == ProjectionPurpose.Html)
			Assert.That(new R.HtmlRenderer().Render(document).Html, Does.Contain("External document"));
	}
}
