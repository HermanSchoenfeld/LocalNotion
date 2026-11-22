// Copyright (c) Herman Schoenfeld 2018 - Present. All rights reserved. (https://sphere10.com/products/localnotion)
// Author: Herman Schoenfeld <herman@sphere10.com>
//
// Distributed under the GPLv3 software license, see the accompanying file LICENSE
// or visit https://github.com/HermanSchoenfeld/localnotion/blob/master/LICENSE
//
// This notice must not be removed when duplicating this file or its contents, in whole or in part.

using LocalNotion.Core;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Notion.Client;
using NUnit.Framework;

namespace LocalNotion.Core.Tests;

[TestFixture]
[Category("Unit")]
public class NativeTabsTests {

	[Test]
	public void RequestPreservesNativeTabShapeAndLabelIcon() {
		var request = new TabBlockRequest {
			Tab = new() {
				Children = [new ParagraphBlockRequest {
					Paragraph = new() {
						RichText = [new RichTextText { Text = new() { Content = "Overview" } }],
						Icon = new EmojiPageIconRequest { Emoji = "📋" },
						Children = [new ParagraphBlockRequest { Paragraph = new() { RichText = [new RichTextText { Text = new() { Content = "Panel content" } }] } }]
					}
				}]
			}
		};
		var json = JObject.Parse(JsonConvert.SerializeObject(request));
		Assert.That((string)json["type"], Is.EqualTo("tab"));
		Assert.That((string)json.SelectToken("tab.children[0].type"), Is.EqualTo("paragraph"));
		Assert.That((string)json.SelectToken("tab.children[0].paragraph.icon.emoji"), Is.EqualTo("📋"));
		Assert.That((string)json.SelectToken("tab.children[0].paragraph.children[0].paragraph.rich_text[0].text.content"), Is.EqualTo("Panel content"));
	}

	[Test]
	public void TabResponseSurvivesCachedObjectRoundTrip() {
		var block = JsonConvert.DeserializeObject<IBlock>("""
			{"object":"block","id":"tabs","type":"tab","has_children":true,"tab":{}}
			""");
		var restored = JsonConvert.DeserializeObject<IBlock>(JsonConvert.SerializeObject(block));
		Assert.That(restored, Is.TypeOf<TabBlock>());
		Assert.That(restored.HasChildren, Is.True);
		Assert.That(restored.Id, Is.EqualTo("tabs"));
	}

	[Test]
	public void UploadedTabIconParticipatesInAttachmentLocalization() {
		var label = JsonConvert.DeserializeObject<IBlock>("""
			{"object":"block","id":"panel","type":"paragraph","paragraph":{"rich_text":[],"icon":{"type":"file","file":{"url":"https://example.invalid/icon.png"}}}}
			""");
		Assert.That(label.HasFileAttachment(), Is.True);
		var attachment = label.GetFileAttachment();
		Assert.That(attachment.GetUrl(), Is.EqualTo("https://example.invalid/icon.png"));
		attachment.SetUrl("media/icon.png");
		var restored = (ParagraphBlock)JsonConvert.DeserializeObject<IBlock>(JsonConvert.SerializeObject(label));
		Assert.That(((FilePageIcon)restored.Paragraph.Icon).File.Url, Is.EqualTo("media/icon.png"));
	}
}
