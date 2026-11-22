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
using AngleSharp.Html.Parser;
using LocalNotion.Core;
using Newtonsoft.Json;
using Notion.Client;
using NUnit.Framework;
using R = Sphere10.Framework.Web.VisualRenderer;

namespace LocalNotion.Core.Tests;

[TestFixture]
[Category("Unit")]
[Parallelizable(ParallelScope.Children)]
public class CmsDatabaseAuthenticationProjectionTests {
	private record DatabaseFixture(FixtureRepository Repository, LocalNotionDatabase Resource, NotionObjectGraph Graph);

	[TestCase(false)]
	[TestCase(true)]
	public void RegisteredCmsDatabaseOmitsAuthenticationFromModelAndHtml(bool compactRegistrationID) {
		var (repository, resource, graph) = Fixture();
		repository.CMSDatabaseIdentifier = compactRegistrationID ? resource.ID.Replace("-", "").ToUpperInvariant() : resource.ID;
		var before = JsonConvert.SerializeObject(new { resource, graph, repository.ObjectMap });
		var document = new NotionRenderModelBuilder(repository).Build(resource, graph, repository.ObjectMap, Path.GetTempPath());
		var database = (R.DatabaseBlock)document.Children.Single();
		Assert.That(database.Columns.Select(column => column.Key), Is.EqualTo(new[] { "Item", "Summary", "Count" }));
		Assert.That(database.Rows.Select(row => row.Cells.Length), Is.EqualTo(new[] { 3, 3 }));
		Assert.That(((R.NumberValue)database.Rows[0].Cells[2]).Number, Is.EqualTo(10));
		Assert.That(database.Rows[1].Cells[2], Is.TypeOf<R.EmptyValue>(), "Filtering must preserve alignment and absent-property cells.");
		var html = new R.HtmlRenderer().Render(document with { RenderFrame = false, ShowPageHeader = false }).Html;
		var parsed = new HtmlParser().ParseDocument(html);
		Assert.That(parsed.QuerySelectorAll("thead th").Select(cell => cell.TextContent), Is.EqualTo(new[] { "Item", "Summary", "Count" }));
		Assert.That(html, Does.Not.Contain("private-user:private-password").And.Not.Contain("other-user:other-password").And.Not.Contain("Authentication"));
		Assert.That(parsed.Body.TextContent, Does.Contain("First public summary").And.Contain("Second public summary"));
		Assert.That(JsonConvert.SerializeObject(new { resource, graph, repository.ObjectMap }), Is.EqualTo(before), "The source and hosting credentials must remain unchanged.");
	}

	[TestCase(false)]
	[TestCase(true)]
	public void GenericDatabaseRetainsAuthenticationAsOrdinaryContent(bool anotherCmsIsRegistered) {
		var (repository, resource, graph) = Fixture();
		repository.CMSDatabaseIdentifier = anotherCmsIsRegistered ? Guid.NewGuid().ToString() : null;
		var document = new NotionRenderModelBuilder(repository).Build(resource, graph, repository.ObjectMap, Path.GetTempPath());
		var database = (R.DatabaseBlock)document.Children.Single();
		Assert.That(database.Columns.Select(column => column.Key), Is.EqualTo(new[] { "Item", "Authentication", "Summary", "Count" }));
		var html = new R.HtmlRenderer().Render(document with { RenderFrame = false, ShowPageHeader = false }).Html;
		Assert.That(html, Does.Contain("Authentication").And.Contain("private-user:private-password").And.Contain("First public summary"));
	}

	[Test]
	public void DatabaseTextProjectionRemainsIndependentOfRepository() {
		var (repository, resource, graph) = Fixture();
		var builder = new NotionRenderModelBuilder(new UnavailableRepository());
		var document = builder.Build(resource, graph, repository.ObjectMap, purpose: ProjectionPurpose.Text);
		var database = (R.DatabaseBlock)document.Children.Single();
		Assert.That(database.Columns.Select(column => column.Key), Is.EqualTo(new[] { "Item", "Authentication", "Summary", "Count" }));
		Assert.That(new R.TextRenderer().Render(document), Is.Empty, "The existing database text omission remains intact.");
	}

	private static DatabaseFixture Fixture() {
		var repository = new FixtureRepository(new PathResolver(Path.GetTempPath(), new LocalNotionPathProfile()));
		var resource = new LocalNotionDatabase { ID = Guid.NewGuid().ToString(), Title = "CMS", Name = "cms" };
		var database = new Database { Id = resource.ID };
		var first = new Page {
			Id = Guid.NewGuid().ToString(),
			Properties = new Dictionary<string, PropertyValue> {
				["Item"] = new TitlePropertyValue { Title = [Text("First")] },
				[Constants.AuthenticationPropertyName] = new RichTextPropertyValue { RichText = [Text("private-user:private-password")] },
				["Summary"] = new RichTextPropertyValue { RichText = [Text("First public summary")] },
				["Count"] = new NumberPropertyValue { Number = 10 }
			}
		};
		var second = new Page {
			Id = Guid.NewGuid().ToString(),
			Properties = new Dictionary<string, PropertyValue> {
				["Summary"] = new RichTextPropertyValue { RichText = [Text("Second public summary")] },
				[Constants.AuthenticationPropertyName] = new RichTextPropertyValue { RichText = [Text("other-user:other-password")] },
				["Item"] = new TitlePropertyValue { Title = [Text("Second")] }
			}
		};
		var graph = new NotionObjectGraph { ObjectID = database.Id, Children = [new() { ObjectID = first.Id }, new() { ObjectID = second.Id }] };
		repository.ResourceMap[resource.ID] = resource;
		repository.GraphMap[resource.ID] = graph;
		foreach (var value in new IObject[] { database, first, second })
			repository.ObjectMap[value.Id] = value;
		foreach (var row in new[] { first, second }) {
			var page = new LocalNotionPage { ID = row.Id, Title = row == first ? "First" : "Second", Name = row == first ? "first" : "second" };
			page.Renders[RenderType.HTML] = new() { LocalPath = $"pages/{row.Id}/row.html", Slug = "row-" + row.Id };
			repository.ResourceMap[row.Id] = page;
		}
		return new DatabaseFixture(repository, resource, graph);
	}

	private static RichTextText Text(string value) => new() { PlainText = value, Text = new() { Content = value } };
}
