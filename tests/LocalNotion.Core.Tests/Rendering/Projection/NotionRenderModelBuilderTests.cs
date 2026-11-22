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
using Sphere10.Framework.Web.VisualRenderer;
using Newtonsoft.Json;
using Notion.Client;
using NUnit.Framework;
using Sphere10.Framework;
using R = Sphere10.Framework.Web.VisualRenderer;

namespace LocalNotion.Core.Tests;

[TestFixture]
[Category("Unit")]
[Parallelizable(ParallelScope.Children)]
[FixtureLifeCycle(LifeCycle.InstancePerTestCase)]
public class NotionRenderModelBuilderTests {
	private string _fixtureRoot;

	[SetUp]
	public void SetUp() {
		_fixtureRoot = Path.Combine(Path.GetTempPath(), "localnotion-projection-" + Guid.NewGuid().ToString("N"));
		Directory.CreateDirectory(_fixtureRoot);
	}

	[TearDown]
	public void TearDown() {
		if (Directory.Exists(_fixtureRoot))
			Directory.Delete(_fixtureRoot, recursive: true);
	}

	[Test]
	public void NativeTabsDeserializeAndProjectNestedContent([Values] ProjectionPurpose purpose) {
		var resource = Resource("Tabs");
		var page = new Page { Id = resource.ID };
		var tabs = JsonConvert.DeserializeObject<IBlock>("{\"object\":\"block\",\"id\":\"tabs\",\"type\":\"tab\",\"has_children\":true,\"tab\":{}}");
		var label = JsonConvert.DeserializeObject<IBlock>("{\"object\":\"block\",\"id\":\"panel\",\"type\":\"paragraph\",\"has_children\":true,\"paragraph\":{\"rich_text\":[{\"type\":\"text\",\"text\":{\"content\":\"Overview\"}}],\"icon\":{\"type\":\"emoji\",\"emoji\":\"📋\"}}}");
		var body = new global::Notion.Client.ParagraphBlock { Id = "body", Paragraph = new() { RichText = [Run("Panel content")] } };
		var repository = Repository();
		repository.ResourceMap[resource.ID] = resource;
		var document = new NotionRenderModelBuilder(purpose == ProjectionPurpose.Text ? new UnavailableRepository() : repository).Build(resource,
			Graph(page.Id, Graph(tabs.Id, Graph(label.Id, Graph(body.Id)))), Objects(page, tabs, label, body), Path.Combine(_fixtureRoot, "output"), purpose: purpose);
		Assert.That(tabs, Is.TypeOf<global::Notion.Client.TabBlock>());
		var group = (R.TabsBlock)document.Children.Single();
		var panel = (R.TabBlock)group.Children.Single();
		Assert.That(panel.Metadata.SourceId, Is.EqualTo("panel"));
		Assert.That(panel.Children, Has.Length.EqualTo(1));
		Assert.That(new TextRenderer().Render(document), Does.Contain("Overview").And.Contain("Panel content"));
		Assert.That(((global::Notion.Client.ParagraphBlock)label).Paragraph.Icon, Is.TypeOf<EmojiPageIcon>());
		if (purpose == ProjectionPurpose.Html) {
			Assert.That(panel.Icon.Emoji, Is.EqualTo("📋"));
			Assert.That(new HtmlRenderer().Render(document).Html, Does.Contain("data-ln-tabs").And.Contain("Panel content").And.Contain("Overview"));
		}
	}

	[Test]
	public void TextWithoutRepository() {
		var resourceValue = Resource("Early text");
		var pageValue = new Page { Id = resourceValue.ID };
		global::Notion.Client.ParagraphBlock paragraphValue = new global::Notion.Client.ParagraphBlock {
			Id = ID(),
			Paragraph = new() {
				RichText = [
					Run("Searchable content"),
					new RichTextMention { PlainText = "future mention", Mention = new() { Type = "new_mention" } },
					new RichTextMention { Mention = new() { Type = "page", Page = new() { Id = ID() } } },
					new RichTextMention { Mention = new() { Type = "page" }, PlainText = "missing payload" },
					new RichTextText { Text = new() { Content = "Source label", Link = new() { Url = "/p/" + ID() } } }
				]
			}
		};
		var imageValue = new ImageBlock { Id = ID(), Image = new UploadedFile { File = new() { Url = "https://example.invalid/not-downloaded.png" } } };
		var graphValue = Graph(pageValue.Id, Graph(paragraphValue.Id), Graph(imageValue.Id));
		var objectsValue = Objects(pageValue, paragraphValue, imageValue);
		// Every repository access on this decorator throws; even constructor initialization
		// must remain independent when the supplied in-memory graph is used for text.
		var builder = new NotionRenderModelBuilder(new UnavailableRepository());
		R.DocumentBlock document = builder.Build(resourceValue, graphValue, objectsValue, outputDirectory: null, purpose: ProjectionPurpose.Text);
		Assert.That(document.Children.Length, Is.EqualTo(2), "text projection has content without repository access");
		R.ParagraphBlock projected = (R.ParagraphBlock)document.Children[0];
		Assert.That(((TextInline)projected.Text[1]).Text, Is.EqualTo("future mention"), "unknown mention plain-text fallback");
		Assert.That(((ReferenceInline)projected.Text[2]).Reference.PlainTextOverride, Is.EqualTo(Environment.NewLine), "early references retain text omission");
		Assert.That(((TextInline)projected.Text[3]).Text, Is.EqualTo("missing payload"), "missing mention payload fallback");
		Assert.That(((TextInline)projected.Text[4]).PlainTextOverride, Does.StartWith("/p/"), "source link retained for text without resolution");
		R.DocumentBlock withoutRepository = new NotionRenderModelBuilder().Build(resourceValue, graphValue, objectsValue, purpose: ProjectionPurpose.Text);
		Assert.That(withoutRepository.Children.Length, Is.EqualTo(document.Children.Length), "null repository text projection");
	}

	[Test]
	public void RecursiveProjection() {
		var repositoryValue = Repository();
		var resourceValue = Resource("Recursive");
		var pageValue = new Page { Id = resourceValue.ID };
		var first = new BulletedListItemBlock { Id = ID(), HasChildren = true, BulletedListItem = new() { RichText = [Run("outer")] } };
		var nested = new NumberedListItemBlock { Id = ID(), NumberedListItem = new() { RichText = [Run("nested")] } };
		var second = new BulletedListItemBlock { Id = ID(), BulletedListItem = new() { RichText = [Run("second")] } };
		global::Notion.Client.DividerBlock divider = new global::Notion.Client.DividerBlock { Id = ID() };
		var third = new BulletedListItemBlock { Id = ID(), BulletedListItem = new() { RichText = [Run("separate list")] } };
		global::Notion.Client.TableRowBlock row = new global::Notion.Client.TableRowBlock { Id = ID(), TableRow = new() { Cells = [[Run("header")], [Run("value")]] } };
		global::Notion.Client.TableBlock tableValue = new global::Notion.Client.TableBlock { Id = ID(), HasChildren = true, Table = new() { TableWidth = 2, HasRowHeader = true, HasColumnHeader = false } };
		global::Notion.Client.CodeBlock codeValue = new global::Notion.Client.CodeBlock { Id = ID(), Code = new() { Language = "html", RichText = [Run("<em>trusted</em>")], Caption = [Run("{INJECT}")] } };
		var missing = Graph(ID());
		var graphValue = Graph(pageValue.Id, Graph(first.Id, Graph(nested.Id)), Graph(second.Id), Graph(divider.Id), Graph(third.Id), Graph(tableValue.Id, Graph(row.Id)), Graph(codeValue.Id), missing, Graph(second.Id));
		var objectsValue = Objects(pageValue, first, nested, second, divider, third, tableValue, row, codeValue);
		repositoryValue.ResourceMap[resourceValue.ID] = resourceValue;
		var before = JsonConvert.SerializeObject(new { resource = resourceValue, graph = graphValue, objects = objectsValue });
		R.DocumentBlock document = new NotionRenderModelBuilder(repositoryValue).Build(resourceValue, graphValue, objectsValue, Path.Combine(_fixtureRoot, "output"));
		R.ListBlock list = (R.ListBlock)document.Children[0];
		Assert.That(list.Type, Is.EqualTo(R.ListType.Bulleted));
		Assert.That(list.Items, Has.Length.EqualTo(2), "adjacent list items grouped");
		Assert.That(((R.ListBlock)list.Items[0].Children.Single()).Type, Is.EqualTo(R.ListType.Numbered), "nested list belongs to its item");
		Assert.That(document.Children[1], Is.TypeOf<R.DividerBlock>());
		Assert.That(document.Children[2], Is.TypeOf<R.ListBlock>(), "list groups stop at intervening content");
		R.TableBlock projectedTable = (R.TableBlock)document.Children[3];
		Assert.That(projectedTable.FirstRowIsHeader, Is.True);
		Assert.That(projectedTable.FirstColumnIsHeader, Is.False);
		Assert.That(projectedTable.Rows.Single().Cells, Has.Length.EqualTo(2), "table header compatibility and typed cells");
		Assert.That(document.Children[4] is R.RawHtmlBlock { Html: "<em>trusted</em>" }, Is.True, "explicit trusted source convention projected");
		Assert.That(document.Children[5] is R.UnsupportedBlock { Type: "missing" }, Is.True, "missing graph object becomes a diagnostic node");
		R.ListItemBlock repeated = ((R.ListBlock)document.Children[6]).Items.Single();
		Assert.That(repeated.Metadata.SourceId, Is.EqualTo(list.Items[1].Metadata.SourceId));
		Assert.That(repeated.Metadata.Anchor, Is.Not.EqualTo(list.Items[1].Metadata.Anchor), "repeated source occurrences have distinct DOM anchors");
		Assert.That(JsonConvert.SerializeObject(new { resource = resourceValue, graph = graphValue, objects = objectsValue }), Is.EqualTo(before), "projection does not mutate source data");

	}

	[Test]
	public void CyclicSourceGraphTerminates() {
		var resourceValue = Resource("Cyclic source");
		var pageValue = new Page { Id = resourceValue.ID };
		global::Notion.Client.ParagraphBlock cyclic = new global::Notion.Client.ParagraphBlock { Id = ID(), HasChildren = true, Paragraph = new() { RichText = [Run("cycle")] } };
		var cyclicNode = Graph(cyclic.Id);
		cyclicNode.Children = [cyclicNode];
		R.DocumentBlock cyclicDoc = new NotionRenderModelBuilder().Build(resourceValue, Graph(pageValue.Id, cyclicNode), Objects(pageValue, cyclic), purpose: ProjectionPurpose.Text);
		Assert.That(((R.ParagraphBlock)cyclicDoc.Children.Single()).Children.Single() is R.UnsupportedBlock { Type: "cyclic-reference" }, Is.True, "source graph cycle terminates with fallback");
	}

	[Test]
	public void DepthLimit() {
		var resourceValue = Resource("Deep source");
		var pageValue = new Page { Id = resourceValue.ID };
		var objectsValue = Objects(pageValue);
		var root = Graph(pageValue.Id);
		var parent = root;
		for (var index = 0; index < 160; index++) {
			var item = new BulletedListItemBlock { Id = ID(), HasChildren = true, BulletedListItem = new() { RichText = [Run("depth " + index)] } };
			objectsValue[item.Id] = item;
			var child = Graph(item.Id);
			parent.Children = [child];
			parent = child;
		}
		R.DocumentBlock document = new NotionRenderModelBuilder().Build(resourceValue, root, objectsValue, purpose: ProjectionPurpose.Text);
		var textValue = new Sphere10.Framework.Web.VisualRenderer.TextRenderer().Render(document);
		Assert.That(textValue, Does.Contain("depth 0").And.Not.Contain("depth 159"), "source depth truncation leaves enough budget for normalized visual list wrappers");
	}

	[Test]
	public void StoredGraphSafety() {
		var repositoryValue = Repository();
		var resourceValue = Resource("Stored malformed graph");
		var pageValue = new Page { Id = resourceValue.ID };
		global::Notion.Client.ParagraphBlock repeated = new global::Notion.Client.ParagraphBlock { Id = ID(), Paragraph = new() { RichText = [Run("repeated")] } };
		global::Notion.Client.ParagraphBlock cyclic = new global::Notion.Client.ParagraphBlock { Id = ID(), HasChildren = true, Paragraph = new() { RichText = [Run("cyclic")] } };
		var cyclicGraph = Graph(cyclic.Id);
		cyclicGraph.Children = [cyclicGraph];
		var repeatedGraph = Graph(repeated.Id);
		var graphValue = Graph(pageValue.Id, null, Graph(ID()), repeatedGraph, repeatedGraph, cyclicGraph);
		repositoryValue.ResourceMap[resourceValue.ID] = resourceValue;
		repositoryValue.GraphMap[resourceValue.ID] = graphValue;
		foreach (var source in new IObject[] { pageValue, repeated, cyclic })
			repositoryValue.ObjectMap[source.Id] = source;
		var builder = new NotionRenderModelBuilder(repositoryValue);
		R.DocumentBlock document = builder.Build(resourceValue.ID, _fixtureRoot);
		Assert.That(document.Children[0] is R.UnsupportedBlock { Type: "missing" } && document.Children[1] is R.UnsupportedBlock { Type: "missing" }, Is.True, "stored-resource loading permits missing graph nodes and objects for controlled fallback");
		Assert.That(document.Children[2], Is.TypeOf<R.ParagraphBlock>());
		Assert.That(document.Children[3], Is.TypeOf<R.ParagraphBlock>(), "stored-resource loading permits repeated source objects");
		Assert.That(((R.ParagraphBlock)document.Children[4]).Children.Single() is R.UnsupportedBlock { Type: "cyclic-reference" }, Is.True, "stored-resource loading terminates source graph cycles before mapping");
		Assert.That(graphValue.Children[2], Is.SameAs(graphValue.Children[3]));
		Assert.That(cyclicGraph, Is.SameAs(cyclicGraph.Children.Single()), "stored-resource loading does not rewrite graph identities");

	}

	[Test]
	public void StoredGraphDepthIsBounded() {
		var repositoryValue = Repository();
		var resourceValue = Resource("Stored deep graph");
		var pageValue = new Page { Id = resourceValue.ID };
		repositoryValue.ResourceMap[resourceValue.ID] = resourceValue;
		repositoryValue.ObjectMap[pageValue.Id] = pageValue;
		var builder = new NotionRenderModelBuilder(repositoryValue);
		var deepGraph = Graph(pageValue.Id);
		var parent = deepGraph;
		for (var index = 0; index < 10000; index++) {
			global::Notion.Client.ParagraphBlock block = new global::Notion.Client.ParagraphBlock { Id = ID(), HasChildren = true, Paragraph = new() { RichText = [Run("stored depth " + index)] } };
			repositoryValue.ObjectMap[block.Id] = block;
			var child = Graph(block.Id);
			parent.Children = [child];
			parent = child;
		}
		repositoryValue.GraphMap[resourceValue.ID] = deepGraph;
		var readsBefore = repositoryValue.ObjectReads;
		R.DocumentBlock deepDocument = builder.Build(resourceValue.ID, _fixtureRoot);
		var textValue = new Sphere10.Framework.Web.VisualRenderer.TextRenderer().Render(deepDocument);
		Assert.That(textValue, Does.Contain("stored depth 0").And.Not.Contain("stored depth 9999"), "stored-resource loading truncates deep graphs without recursive enumeration");
		Assert.That(repositoryValue.ObjectReads - readsBefore, Is.LessThan(300), "stored-resource loading reads only the bounded renderable graph prefix");
	}

	[Test]
	public void EmbedProjection() {
		var resourceValue = Resource("Embeds");
		var pageValue = new Page { Id = resourceValue.ID };
		var video = new EmbedBlock { Id = ID(), Embed = new() { Url = "https://www.youtube.com/watch?v=dQw4w9WgXcQ", Caption = [Run("Video caption")] } };
		var social = new EmbedBlock { Id = ID(), Embed = new() { Url = "https://x.com/example/status/123", Caption = [Run("Social caption")] } };
		R.DocumentBlock document = new NotionRenderModelBuilder(Repository()).Build(resourceValue, Graph(pageValue.Id, Graph(video.Id), Graph(social.Id)), Objects(pageValue, video, social), _fixtureRoot);
		Assert.That(document.Children[0] is R.MediaBlock { Provider: EmbedProvider.YouTube, ProviderId: "dQw4w9WgXcQ" }, Is.True, "video-sharing embed retains provider presentation");
		Assert.That(((TextInline)((R.MediaBlock)document.Children[0]).Caption.Single()).Text, Is.EqualTo("Video caption"), "video-sharing embed caption retained");
		Assert.That(document.Children[1] is R.MediaBlock { Provider: EmbedProvider.Twitter, Url: "https://twitter.com/example/status/123" }, Is.True, "X embed URL projected for compatible widget");
		Assert.That(((TextInline)((R.MediaBlock)document.Children[1]).Caption.Single()).Text, Is.EqualTo("Social caption"), "social embed caption retained");
	}

	[TestCase("c#", CodeLanguage.CSharp)]
	[TestCase("C++", CodeLanguage.CPlusPlus)]
	[TestCase("f#", CodeLanguage.FSharp)]
	[TestCase("objective-c", CodeLanguage.ObjectiveC)]
	[TestCase("vb.net", CodeLanguage.VbNet)]
	[TestCase("visual basic", CodeLanguage.VisualBasic)]
	[TestCase("llvm ir", CodeLanguage.LlvmIr)]
	[TestCase("JaVaScRiPt", CodeLanguage.Javascript)]
	[TestCase("assembly", CodeLanguage.Assembly)]
	[TestCase("ardunio", CodeLanguage.Ardunio)]
	[TestCase("arduino", CodeLanguage.Arduino)]
	[TestCase("elixer", CodeLanguage.Elixer)]
	[TestCase("elixir", CodeLanguage.Elixir)]
	[TestCase("livescript", CodeLanguage.LiveScript)]
	[TestCase("markup", CodeLanguage.Markup)]
	[TestCase("shell", CodeLanguage.Shell)]
	[TestCase("plain text", CodeLanguage.Text)]
	[TestCase("text", CodeLanguage.Text)]
	[TestCase("a-new-source-language", CodeLanguage.Text)]
	[TestCase("CSharp, Python", CodeLanguage.Text)]
	[TestCase("0", CodeLanguage.Text)]
	[TestCase("1", CodeLanguage.Text)]
	[TestCase("99999", CodeLanguage.Text)]
	[TestCase("-1", CodeLanguage.Text)]
	[TestCase("", CodeLanguage.Text)]
	[TestCase("   ", CodeLanguage.Text)]
	[TestCase(null, CodeLanguage.Text)]
	public void CodeLanguageProjection(string sourceLanguage, CodeLanguage expectedLanguage) {
		var resource = Resource("Code languages");
		var page = new Page { Id = resource.ID };
		global::Notion.Client.CodeBlock block = new global::Notion.Client.CodeBlock {
			Id = ID(),
			Code = new() { Language = sourceLanguage, RichText = [Run("source code")], Caption = [Run("caption")] }
		};
		var sourceBefore = JsonConvert.SerializeObject(block);
		R.DocumentBlock document = new NotionRenderModelBuilder().Build(
			resource, Graph(page.Id, Graph(block.Id)), Objects(page, block), purpose: ProjectionPurpose.Text
		);
		R.CodeBlock projected = (R.CodeBlock)document.Children.Single();
		Assert.That(JsonConvert.SerializeObject(block), Is.EqualTo(sourceBefore), "Projection must leave the source language, code and caption unchanged");
		Assert.That(projected.Language, Is.EqualTo(expectedLanguage));
		Assert.That(projected.Code, Is.EqualTo("source code"));
		Assert.That(((TextInline)projected.Caption.Single()).Text, Is.EqualTo("caption"));

		block.Code.Language = "python";
		Assert.That(projected.Language, Is.EqualTo(expectedLanguage), "The visual model must be detached from subsequent source mutations");
	}

	[Test]
	public void ResolvedLinks() {
		var repositoryValue = Repository();
		var source = Resource("Source");
		var target = Resource("Target");
		var asset = new LocalNotionFile { ID = ID(), Title = "cover.png" };
		source.Renders[RenderType.HTML] = new() { LocalPath = $"pages/{source.ID}/source.html", Slug = "source" };
		target.Renders[RenderType.HTML] = new() { LocalPath = $"pages/{target.ID}/target (2).html", Slug = "target-2" };
		asset.Renders[RenderType.File] = new() { LocalPath = $"files/{asset.ID}/cover.png", Slug = "cover.png" };
		repositoryValue.ResourceMap[source.ID] = source;
		repositoryValue.ResourceMap[target.ID] = target;
		repositoryValue.ResourceMap[asset.ID] = asset;
		var anchorValue = ID();
		repositoryValue.ObjectMap[anchorValue] = new global::Notion.Client.ParagraphBlock { Id = anchorValue };
		repositoryValue.Parents[anchorValue] = target;
		var output = Path.Combine(_fixtureRoot, "cms", "nested");
		var builder = new NotionRenderModelBuilder(repositoryValue);
		var expectedTarget = $"../../pages/{target.ID}/target (2).html";
		Assert.That(builder.ResolveReference(source, target.ID, output).Url, Is.EqualTo(expectedTarget), "reference is relative to actual CMS destination");
		Assert.That(builder.ResolveUrl(source, "/p/" + target.ID + "#" + anchorValue, output), Is.EqualTo(expectedTarget + "#" + anchorValue), "p URL and fragment resolved");
		Assert.That(builder.ResolveUrl(source, "/" + Guid.Parse(target.ID).ToString() + "#" + anchorValue, output), Is.EqualTo(expectedTarget + "#" + anchorValue), "hyphenated source URL resolved");
		Assert.That(builder.ResolveReference(source, anchorValue, output).Url, Is.EqualTo(expectedTarget + "#" + anchorValue), "block reference resolved through parent");
		Assert.That(builder.ResolveUrl(source, LocalNotionRenderLink.GenerateUrl(asset.ID, RenderType.File), output), Is.EqualTo($"../../files/{asset.ID}/cover.png"), "uploaded file handle resolved");
		Assert.That(builder.ResolveMediaUrl(source, new UploadedFile { File = new() { Url = "cover.png" } }, output), Is.EqualTo($"../../files/{asset.ID}/cover.png"), "legacy uploaded render slug resolves through resource metadata");
		var sourcePage = new Page { Id = source.ID };
		var uploaded = new FileBlock { Id = ID(), File = new UploadedFile { File = new() { Url = LocalNotionRenderLink.GenerateUrl(asset.ID, RenderType.File) } } };
		R.DocumentBlock fileDocument = builder.Build(source, Graph(source.ID, Graph(uploaded.Id)), Objects(sourcePage, uploaded), output);
		Assert.That(((R.MediaBlock)fileDocument.Children.Single()).FileName, Is.EqualTo("cover.png"), "uploaded file display name comes from resolved URL rather than resource handle");
		Assert.That(builder.ResolveUrl(source, "resource://malformed", output), Is.EqualTo(string.Empty), "malformed internal handle never leaves projection");
		Assert.That(builder.ResolveReference(source, ID(), output).IsAvailable, Is.False, "missing target is explicitly unavailable");
		Assert.That(builder.ResolveUrl(source, LocalNotionRenderLink.GenerateUrl(ID(), RenderType.File), output), Is.EqualTo(string.Empty), "unresolved internal handle never crosses rendering boundary");
		Assert.That(builder.ResolveUrl(source, "https://www.notion.so/Title-" + target.ID, output), Is.EqualTo(expectedTarget), "public source URL resolves locally");
	}

	[TestCase("../../files/image.png?size=1#preview")]
	[TestCase("https://example.invalid/a?q=1#b")]
	[TestCase("#local")]
	public void ResolvedLinks_PreserveExistingUrls(string url) {
		var builder = new NotionRenderModelBuilder(Repository());
		Assert.That(builder.ResolveUrl(Resource("Source"), url, Path.Combine(_fixtureRoot, "cms", "nested")), Is.EqualTo(url));
	}

	[Test]
	public void CanonicalSourceIds() {
		var repositoryValue = Repository();
		var source = Resource("Dashed source", Guid.NewGuid().ToString("D"));
		var target = Resource("Dashed target", Guid.NewGuid().ToString("D"));
		source.Renders[RenderType.HTML] = new() { LocalPath = $"pages/{source.ID}/source.html", Slug = "source" };
		target.Renders[RenderType.HTML] = new() { LocalPath = $"pages/{target.ID}/target.html", Slug = "target" };
		repositoryValue.ResourceMap[source.ID] = source;
		repositoryValue.ResourceMap[target.ID] = target;
		global::Notion.Client.ParagraphBlock block = new global::Notion.Client.ParagraphBlock { Id = Guid.NewGuid().ToString("D"), Paragraph = new() { RichText = [Run("canonical")] } };
		repositoryValue.ObjectMap[block.Id] = block;
		repositoryValue.Parents[block.Id] = target;
		var sourcePage = new Page { Id = source.ID };
		var targetPage = new Page { Id = target.ID };
		repositoryValue.ObjectMap[source.ID] = sourcePage;
		repositoryValue.ObjectMap[target.ID] = targetPage;
		repositoryValue.GraphMap[target.ID] = Graph(target.ID);
		var output = Path.Combine(_fixtureRoot, "cms");
		var builder = new NotionRenderModelBuilder(repositoryValue);
		var targetN = Guid.Parse(target.ID).ToString("N");
		var blockN = Guid.Parse(block.Id).ToString("N");
		var expected = $"../pages/{target.ID}/target.html";
		Assert.That(builder.ResolveUrl(source, "/p/" + targetN + "#" + block.Id, output), Is.EqualTo(expected + "#" + blockN), "compact URL resolves dashed stored resource and normalized block anchor");
		Assert.That(builder.ResolveReference(source, blockN, output).Url, Is.EqualTo(expected + "#" + blockN), "compact block id resolves dashed object and parent resource");
		Assert.That(builder.ResolveReference(source, Guid.Parse(source.ID).ToString("N"), output).Url, Is.EqualTo(string.Empty), "self link resolves actual stored source identity");
		R.DocumentBlock document = builder.Build(source, Graph(Guid.Parse(source.ID).ToString("N"), Graph(blockN)), repositoryValue.ObjectMap, output);
		Assert.That(document.Children.Single(), Is.TypeOf<R.ParagraphBlock>());
		Assert.That(document.Metadata.Anchor, Is.EqualTo(Guid.Parse(source.ID).ToString("N")), "graph lookup accepts compact ids while DOM anchors remain compact");
		Assert.That(builder.Build(targetN, output).Title, Is.EqualTo("Dashed target"), "resource-id Build accepts both stored and URL id representations");
	}

	[Test]
	public void DatabaseAlignment() {
		var repositoryValue = Repository();
		var resourceValue = new LocalNotionDatabase { ID = ID(), Title = "Database", Name = "database" };
		var databaseValue = new Database { Id = resourceValue.ID };
		var first = new Page { Id = ID(), Properties = new Dictionary<string, PropertyValue> { ["Name"] = new TitlePropertyValue { Title = [Run("First")] }, ["Count"] = new NumberPropertyValue { Number = 1 } } };
		var second = new Page { Id = ID(), Properties = new Dictionary<string, PropertyValue> { ["Count"] = new NumberPropertyValue { Number = 2 }, ["Name"] = new TitlePropertyValue { Title = [Run("Second")] }, ["Extra"] = new FormulaPropertyValue { Formula = new() { Type = "boolean", Boolean = true } } } };
		second.Properties["Selections"] = new MultiSelectPropertyValue { MultiSelect = [null, new SelectOption { Name = "Valid" }] };
		second.Properties["Related"] = new RelationPropertyValue { Relation = [null] };
		var graphValue = Graph(databaseValue.Id, Graph(first.Id), null, Graph(second.Id));
		repositoryValue.GraphMap[databaseValue.Id] = graphValue;
		foreach (var sourceObject in new IObject[] { databaseValue, first, second })
			repositoryValue.ObjectMap[sourceObject.Id] = sourceObject;
		repositoryValue.ResourceMap[resourceValue.ID] = resourceValue;
		foreach (var row in new[] { first, second }) {
			var rowResource = Resource(row.Id == first.Id ? "First" : "Second", row.Id);
			rowResource.Renders[RenderType.HTML] = new() { LocalPath = $"pages/{row.Id}/row.html", Slug = "row-" + row.Id };
			repositoryValue.ResourceMap[row.Id] = rowResource;
		}
		R.DocumentBlock document = new NotionRenderModelBuilder(repositoryValue).Build(resourceValue, graphValue, repositoryValue.ObjectMap, _fixtureRoot);
		R.DatabaseBlock projected = (R.DatabaseBlock)document.Children.Single();
		Assert.That(projected.Columns.Select(item => item.Key), Is.EqualTo(new[] { "Name", "Count", "Extra", "Selections", "Related" }), "column order materialized once");
		Assert.That(((NumberValue)projected.Rows[0].Cells[1]).Number, Is.EqualTo(1));
		Assert.That(((NumberValue)projected.Rows[1].Cells[1]).Number, Is.EqualTo(2), "rows align by property key despite source dictionary order");
		Assert.That(projected.Rows[0].Cells[2], Is.TypeOf<EmptyValue>(), "missing cell represented explicitly");
		Assert.That(projected.Rows[1].Cells[2] is ComputedValue { Value: BooleanValue { Boolean: true } }, Is.True, "evaluated formula result projected");
		Assert.That(((ReferenceValue)projected.Rows[1].Cells[0]).References.Single().Label, Is.EqualTo("Second"), "database title resolves reference label");
		Assert.That(document.PlainTextOverride, Is.EqualTo(string.Empty), "legacy text omission for database preserved");
		Assert.That(((ChoiceValue)projected.Rows[1].Cells[3]).Choices.Single().Label, Is.EqualTo("Valid"), "null option members are ignored without discarding valid choices");
		Assert.That(((ReferenceValue)projected.Rows[1].Cells[4]).References.Length, Is.EqualTo(0), "null relation entries remain harmless");
	}

	[TestCase("@HideEmbeddedPages", true)]
	[TestCase("HideEmbeddedPages", false)]
	[TestCase("hide_embedded_pages", false)]
	[TestCase("hideEmbeddedPages", false)]
	[TestCase("", false)]
	public void HideEmbeddedPagesSuppressesNativePageWidgetsAndRetainsAuthoredLinks(string tagName, bool hideEmbeddedPages) {
		// Native child ownership is distinct from a link to an independently published page.
		var repository = Repository();
		var source = Resource("Source");
		source.CMSProperties = new CMSProperties { Tags = tagName.Length > 0 ? [tagName] : [] };
		var sourcePage = new Page { Id = source.ID };
		var first = Resource("First destination");
		var second = Resource("Second destination");
		foreach (var target in new[] { first, second }) {
			target.Renders[RenderType.HTML] = new() { LocalPath = $"pages/{target.ID}/target.html", Slug = target.Name };
			repository.ResourceMap[target.ID] = target;
		}
		var page = new Page { Id = first.ID };
		var child = new ChildPageBlock { Id = second.ID, ChildPage = new() { Title = second.Title } };
		var link = new LinkToPageBlock { Id = ID(), LinkToPage = new LinkPageToPage { PageId = first.ID } };
		var paragraph = new global::Notion.Client.ParagraphBlock { Id = ID(), Paragraph = new() { RichText = [
			new RichTextText { Text = new() { Content = "Authored heading menu", Link = new() { Url = "/p/" + first.ID + "#heading" } } }
		] } };
		var document = new NotionRenderModelBuilder(repository).Build(source,
			Graph(source.ID, Graph(page.Id), Graph(child.Id), Graph(link.Id), Graph(paragraph.Id)), Objects(sourcePage, page, child, link, paragraph), _fixtureRoot);
		Assert.That(document.HideEmbeddedPages, Is.EqualTo(hideEmbeddedPages));
		Assert.That(document.Themes, Is.EqualTo(new[] { "default" }), "Control Tags must not be injected into the presentation Theme selection.");
		Assert.That(document.Children.OfType<R.ReferenceBlock>().Select(reference => reference.IsEmbeddedPage), Is.EqualTo(new[] { true, true, false }));
		var html = new R.HtmlRenderer().Render(document with { RenderFrame = false, ShowPageHeader = false }).Html;
		var parsed = new AngleSharp.Html.Parser.HtmlParser().ParseDocument(html);
		Assert.That(parsed.QuerySelectorAll("a[href]"), Has.Length.EqualTo(hideEmbeddedPages ? 2 : 4));
		Assert.That(html, Does.Contain("Authored heading menu").And.Contain("First destination"));
		Assert.That(html.Contains("Second destination"), Is.EqualTo(!hideEmbeddedPages));
		Assert.That(document.Children, Has.Length.EqualTo(4), "The behavior control must not delete source nodes or affect publication ownership.");
	}

	[Test]
	public void RenderingAfterSourceUnavailable() {
		var repositoryValue = Repository();
		var resourceValue = Resource("Detached render");
		var pageValue = new Page { Id = resourceValue.ID };
		var child = new ChildPageBlock { Id = ID(), ChildPage = new() { Title = "child" } };
		global::Notion.Client.ParagraphBlock paragraphValue = new global::Notion.Client.ParagraphBlock { Id = ID(), Paragraph = new() { RichText = [Run("Rendered from detached data")] } };
		var objectsValue = Objects(pageValue, paragraphValue, child);
		repositoryValue.ResourceMap[resourceValue.ID] = resourceValue;
		R.DocumentBlock document = new NotionRenderModelBuilder(repositoryValue).Build(resourceValue, Graph(pageValue.Id, Graph(paragraphValue.Id), Graph(child.Id)), objectsValue, _fixtureRoot);
		objectsValue.Clear();
		repositoryValue.ResourceMap.Clear();
		paragraphValue.Paragraph.RichText = [Run("MUTATED SOURCE")];
		var htmlValue = new Sphere10.Framework.Web.VisualRenderer.HtmlRenderer().Render(document).Html;
		var textValue = new Sphere10.Framework.Web.VisualRenderer.TextRenderer().Render(document);
		Assert.That(htmlValue, Does.Contain("Rendered from detached data").And.Not.Contain("MUTATED SOURCE"), "HTML rendering uses materialized values after source becomes unavailable");
		Assert.That(textValue, Is.EqualTo("Detached render" + Environment.NewLine + "detached render" + Environment.NewLine + "Rendered from detached data" + Environment.NewLine + Environment.NewLine + Environment.NewLine), "text renderer traverses typed model and preserves child-reference omission");
	}

	private FixtureRepository Repository() => new(new PathResolver(_fixtureRoot, new LocalNotionPathProfile()));
	private LocalNotionPage Resource(string titleValue, string idValue = null) => new() { ID = idValue ?? ID(), Title = titleValue, Name = titleValue.ToLowerInvariant(), CreatedOn = new DateTime(2020, 1, 1), LastEditedOn = new DateTime(2020, 1, 2) };
	private string ID() => Guid.NewGuid().ToString("N");
	private NotionObjectGraph Graph(string idValue, params NotionObjectGraph[] childrenValue) => new() { ObjectID = idValue, Children = childrenValue };
	private RichTextText Run(string textValue) => new() { PlainText = textValue, Text = new() { Content = textValue } };
	private Dictionary<string, IObject> Objects(params IObject[] objectsValue) => objectsValue.ToDictionary(item => item.Id);
}
