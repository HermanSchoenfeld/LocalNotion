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
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using Notion.Client;
using NUnit.Framework;

namespace LocalNotion.Core.Tests;

[TestFixture]
[Category("Unit")]
[Parallelizable(ParallelScope.Children)]
public class CMSDatabaseCreationTests {
	private static readonly Guid ParentPageID = Guid.Parse("33a865d7-7bed-48e8-880c-57aa191c55bf");

	[Test]
	public async Task CreatesOneEmptyDatabaseWithNotionWireFormat() {
		var transport = new RecordingNotionTransport();
		var database = await new CMSDatabaseCreator(transport.CreateClient(), new CMSDatabaseTemplate(), TimeSpan.Zero).CreateAsync(ParentPageID, "New website");

		Assert.That(transport.CreateCount, Is.EqualTo(1));
		Assert.That(transport.Endpoint, Is.EqualTo("/v1/databases"));
		Assert.That(database, Is.SameAs(transport.Response));
		Assert.That((string)transport.Body["parent"]["page_id"], Is.EqualTo(ParentPageID.ToString("D")));
		Assert.That((string)transport.Body["title"][0]["text"]["content"], Is.EqualTo("New website"));
		Assert.That((string)transport.Body["title"][0]["type"], Is.EqualTo("text"));
		Assert.That((bool?)transport.Body["is_inline"], Is.False);
		Assert.That(transport.Body["initial_data_source"], Is.Not.Null, "The API requires snake_case, not the C# camelCase member name.");
		Assert.That(transport.Body["initialDataSource"], Is.Null);
		Assert.That(transport.Body["children"], Is.Null, "The new database must have no copied rows or blocks.");
		Assert.That(transport.Body.SelectTokens("$..id"), Is.Empty, "Source property and option identifiers must never be copied.");
		Assert.That(transport.Body["initial_data_source"]["properties"].Count(), Is.EqualTo(21));
		Assert.That(transport.Body.SelectTokens("$..options").OfType<JArray>().All(x => x.Count <= 100), Is.True);
		Assert.That(transport.PageCreateCount, Is.EqualTo(1));
		Assert.That(transport.PageUpdateCount, Is.EqualTo(1));
		Assert.That(transport.ArchiveCount, Is.EqualTo(1));
		Assert.That(transport.SetupPageArchived, Is.True);
		Assert.That(transport.SchemaReadCount, Is.EqualTo(1));
		Assert.That(transport.QueryCount, Is.EqualTo(1));
		Assert.That(transport.PageBodies.SelectMany(x => x.SelectTokens("$.properties.*.multi_select")).OfType<JArray>().Select(x => x.Count), Is.EqualTo(new[] { 100, 35 }));
	}

	[Test]
	public async Task EmbeddedTemplateMatchesIndependentStagingSchemaFixture() {
		var transport = new RecordingNotionTransport();
		await new CMSDatabaseCreator(transport.CreateClient(), new CMSDatabaseTemplate(), TimeSpan.Zero).CreateAsync(ParentPageID, "New website");
		var actual = transport.SavedSchema;
		var fixture = JObject.Parse(await File.ReadAllTextAsync(Path.Combine(TestContext.CurrentContext.TestDirectory, "CMS", "Fixtures", "staging-cms-schema.json")));
		var expected = new JObject();
		foreach (var property in fixture["properties"]) {
			var type = (string)property["type"];
			var config = new JObject();
			if (property["format"] != null)
				config["format"] = property["format"].DeepClone();
			if (property["options"] != null)
				config["options"] = property["options"].DeepClone();
			expected[(string)property["name"]] = new JObject { ["type"] = type, [type] = config };
		}
		// Keep the dated fixture unchanged; behavior controls belong to Tags and banner presentation belongs to Themes.
		expected["Keywords"] = new JObject { ["type"] = "rich_text", ["rich_text"] = new JObject() };
		((JArray)expected["Tags"]["multi_select"]["options"]).Add(new JObject { ["name"] = "@HideEmbeddedPages", ["color"] = "default" });
		((JArray)expected["Themes"]["multi_select"]["options"]).Add(new JObject { ["name"] = "header_cover", ["color"] = "default" });
		var menuThemes = new[] { "navbar_refined", "navbar_refined_product_cards", "navbar_refined_compact", "navbar_refined_wide", "navbar_standard", "navbar_bg_aux" };
		foreach (var theme in menuThemes)
			((JArray)expected["Themes"]["multi_select"]["options"]).Add(new JObject { ["name"] = theme, ["color"] = "default" });
		Assert.That(JToken.DeepEquals(actual, expected), Is.True, "Names, property types, option order and colors must match the dated staging snapshot plus explicit behavior and presentation presets.");
		Assert.That(actual["Themes"]["multi_select"]["options"].Select(option => (string)option["name"]), Does.Not.Contain("@HideEmbeddedPages").And.Not.Contain("HideEmbeddedPages").And.Not.Contain("hide_embedded_pages"));
		Assert.That(actual["Themes"]["multi_select"]["options"].Select(option => (string)option["name"]), Does.Contain("header_cover"));
		var tagNames = actual["Tags"]["multi_select"]["options"].Select(option => (string)option["name"]).ToArray();
		Assert.That(tagNames, Does.Contain("@HideEmbeddedPages").And.Not.Contain("HideEmbeddedPages").And.Not.Contain("hide_embedded_pages").And.Not.Contain("header_cover"));
		Assert.That(tagNames.Where(name => !Regex.IsMatch(name, @"^@[A-Z][A-Za-z0-9]*$")), Is.Empty,
			"Every canonical Tag starts with one @ followed by PascalCase.");
		Assert.That(tagNames.Intersect(menuThemes), Is.Empty, "Menu presentation presets belong to Themes; existing behavior Tags such as @HideNavBar remain valid.");
		Assert.That(() => new Sphere10.Framework.Web.VisualRenderer.ThemeCatalog().CreateSession(["default", "header_cover", "header_large"],
			Sphere10.Framework.Web.VisualRenderer.RenderEnvironment.Online, Sphere10.Framework.Web.VisualRenderer.RenderMode.ReadOnly), Throws.Nothing);
	}

	[TestCase("Type", "select", 8)]
	[TestCase("Status", "select", 4)]
	[TestCase("Tags", "multi_select", 13)]
	[TestCase("Themes", "multi_select", 235)]
	public async Task PresetsAreComplete(string name, string type, int count) {
		var transport = new RecordingNotionTransport();
		await new CMSDatabaseCreator(transport.CreateClient(), new CMSDatabaseTemplate(), TimeSpan.Zero).CreateAsync(ParentPageID, "New website");
		var options = (JArray)transport.SavedSchema[name][type]["options"];
		Assert.That(options.Count, Is.EqualTo(count));
		Assert.That(options.Select(x => (string)x["name"]), Is.Unique);
		Assert.That(options.All(x => x["color"] != null && x["id"] == null), Is.True);
	}

	[TestCase("Item", "title")]
	[TestCase("Sequence", "number")]
	[TestCase("Publish On", "date")]
	[TestCase("Created By", "created_by")]
	[TestCase("Created On", "created_time")]
	[TestCase("Edited By", "last_edited_by")]
	[TestCase("Edited On", "last_edited_time")]
	[TestCase("Authentication", "rich_text")]
	[TestCase("Root", "rich_text")]
	[TestCase("Keywords", "rich_text")]
	public void RequiredPropertyUsesCorrectType(string name, string type) {
		var request = new CMSDatabaseTemplate().CreateRequest(ParentPageID, "New website");
		Assert.That(request.InitialDataSource.Properties[name].Type, Is.EqualTo(type));
	}

	[Test]
	public async Task CancelledBeforeCreationSendsNothing() {
		var transport = new RecordingNotionTransport();
		using var cancellation = new CancellationTokenSource();
		cancellation.Cancel();
		await Assert.ThatAsync(() => new CMSDatabaseCreator(transport.CreateClient(), new CMSDatabaseTemplate(), TimeSpan.Zero).CreateAsync(ParentPageID, "New website", cancellation.Token),
			Throws.InstanceOf<OperationCanceledException>());
		Assert.That(transport.CreateCount, Is.Zero);
	}

	[Test]
	public async Task CancellationReachesPendingCreateWithoutRetrying() {
		var pending = new TaskCompletionSource<Database>(TaskCreationOptions.RunContinuationsAsynchronously);
		var transport = new RecordingNotionTransport { PendingResponse = token => pending.Task.WaitAsync(token) };
		using var cancellation = new CancellationTokenSource();
		var creation = new CMSDatabaseCreator(transport.CreateClient(), new CMSDatabaseTemplate(), TimeSpan.Zero).CreateAsync(ParentPageID, "New website", cancellation.Token);
		Assert.That(creation.IsCompleted, Is.False);
		Assert.That(transport.CancellationToken, Is.EqualTo(cancellation.Token));
		cancellation.Cancel();
		await Assert.ThatAsync(async () => await creation, Throws.InstanceOf<OperationCanceledException>());
		Assert.That(transport.CreateCount, Is.EqualTo(1));
	}

	[Test]
	public async Task FailureIsPropagatedWithoutRetrying() {
		var failure = new InvalidOperationException("synthetic transport failure");
		var transport = new RecordingNotionTransport { Error = failure };
		await Assert.ThatAsync(() => new CMSDatabaseCreator(transport.CreateClient(), new CMSDatabaseTemplate(), TimeSpan.Zero).CreateAsync(ParentPageID, "New website"),
			Throws.TypeOf<InvalidOperationException>().With.Message.EqualTo(failure.Message));
		Assert.That(transport.CreateCount, Is.EqualTo(1));
	}

	[TestCase("")]
	[TestCase(" ")]
	[TestCase(null)]
	public void BlankTitleIsRejectedBeforeCreation(string title) {
		Assert.That(() => new CMSDatabaseTemplate().CreateRequest(ParentPageID, title), Throws.ArgumentException);
	}

	[Test]
	public void EmptyParentIsRejectedBeforeCreation() {
		Assert.That(() => new CMSDatabaseTemplate().CreateRequest(Guid.Empty, "New website"), Throws.ArgumentException);
	}

	[Test]
	public void TemplateRequestsDoNotShareMutableProperties() {
		var template = new CMSDatabaseTemplate();
		var first = template.CreateRequest(ParentPageID, "First");
		first.InitialDataSource.Properties.Clear();
		Assert.That(template.CreateRequest(ParentPageID, "Second").InitialDataSource.Properties.Count, Is.EqualTo(21));
	}

	[Test]
	public async Task SetupRowCreationFailureReportsDatabaseWithoutRetrying() {
		var transport = new RecordingNotionTransport { PageCreateError = new IOException("synthetic response lost") };
		await Assert.ThatAsync(() => Create(transport), Throws.TypeOf<CMSDatabaseCreationException>()
			.With.Property("DatabaseID").EqualTo(Guid.Parse(transport.Response.Id))
			.And.Property("SetupPageID").EqualTo(Guid.Empty));
		Assert.That(transport.CreateCount, Is.EqualTo(1));
		Assert.That(transport.PageCreateCount, Is.EqualTo(1));
		Assert.That(transport.ArchiveCount, Is.Zero, "An unknown row ID cannot safely be guessed or retried.");
	}

	[Test]
	public async Task FailedSeedingStillArchivesKnownHiddenRowAndPreservesFailure() {
		var transport = new RecordingNotionTransport { PageUpdateError = new IOException("synthetic seeding failure") };
		await Assert.ThatAsync(() => Create(transport), Throws.TypeOf<CMSDatabaseCreationException>()
			.With.Property("SetupPageArchived").True.And.InnerException.TypeOf<IOException>());
		Assert.That(transport.SetupPageArchived, Is.True);
		Assert.That(transport.ArchiveCount, Is.EqualTo(1));
		Assert.That(transport.CreateCount, Is.EqualTo(1));
		Assert.That(transport.PageCreateCount, Is.EqualTo(1));
		Assert.That(transport.QueryCount, Is.Zero);
	}

	[Test]
	public async Task CleanupFailureReportsBothKnownObjectsWithoutClaimingSuccess() {
		var transport = new RecordingNotionTransport { ArchiveError = new IOException("synthetic cleanup failure") };
		await Assert.ThatAsync(() => Create(transport), Throws.TypeOf<CMSDatabaseCreationException>()
			.With.Property("DatabaseID").Not.EqualTo(Guid.Empty)
			.And.Property("SetupPageID").Not.EqualTo(Guid.Empty)
			.And.Property("SetupPageArchived").False.And.Property("CleanupFailed").True);
		Assert.That(transport.SetupPageArchived, Is.False);
		Assert.That(transport.ArchiveCount, Is.EqualTo(1));
		Assert.That(transport.QueryCount, Is.Zero);
	}

	[Test]
	public async Task SetupAndCleanupFailuresAreBothRetained() {
		var setupError = new IOException("synthetic seeding failure");
		var cleanupError = new IOException("synthetic archive failure");
		var transport = new RecordingNotionTransport { PageUpdateError = setupError, ArchiveError = cleanupError };
		await Assert.ThatAsync(() => Create(transport), Throws.TypeOf<CMSDatabaseCreationException>()
			.With.InnerException.SameAs(setupError)
			.And.Property("CleanupError").SameAs(cleanupError)
			.And.Property("CleanupFailed").True);
		Assert.That(transport.CreateCount, Is.EqualTo(1));
		Assert.That(transport.ArchiveCount, Is.EqualTo(1));
		Assert.That(transport.SchemaReadCount, Is.Zero);
	}

	[Test]
	public async Task ArchiveCompletionIsAwaitedBeforeVerificationAndReturn() {
		var pending = new TaskCompletionSource<Page>(TaskCreationOptions.RunContinuationsAsynchronously);
		var transport = new RecordingNotionTransport { PendingArchive = token => pending.Task.WaitAsync(token) };
		var creation = Create(transport);
		Assert.That(creation.IsCompleted, Is.False);
		Assert.That(transport.ArchiveCount, Is.EqualTo(1));
		Assert.That(transport.SchemaReadCount, Is.Zero);
		pending.SetResult(new Page { InTrash = true });
		await creation;
		Assert.That(transport.SchemaReadCount, Is.EqualTo(1));
		Assert.That(transport.QueryCount, Is.EqualTo(1));
	}

	[Test]
	public async Task UnconfirmedArchiveStopsVerification() {
		var transport = new RecordingNotionTransport { PendingArchive = _ => Task.FromResult(new Page { InTrash = false }) };
		await Assert.ThatAsync(() => Create(transport), Throws.TypeOf<CMSDatabaseCreationException>()
			.With.Property("CleanupFailed").True.And.Property("SetupPageArchived").False);
		Assert.That(transport.ArchiveCount, Is.EqualTo(1));
		Assert.That(transport.SchemaReadCount, Is.Zero);
	}

	[TestCase(null)]
	[TestCase("")]
	[TestCase("not-a-uuid")]
	[TestCase("00000000-0000-0000-0000-000000000000")]
	public async Task InvalidDataSourceReportsCreatedDatabaseWithoutCreatingSetupRows(string dataSourceID) {
		var transport = new RecordingNotionTransport();
		transport.Response.DataSources = new[] { new DataSourceReferenceResponse { DataSourceId = dataSourceID } };
		await Assert.ThatAsync(() => Create(transport), Throws.TypeOf<CMSDatabaseCreationException>()
			.With.Property("DatabaseID").EqualTo(Guid.Parse(transport.Response.Id))
			.And.Property("SetupPageID").EqualTo(Guid.Empty));
		Assert.That(transport.CreateCount, Is.EqualTo(1));
		Assert.That(transport.PageCreateCount, Is.Zero);
		Assert.That(transport.ArchiveCount, Is.Zero);
		Assert.That(transport.SchemaReadCount, Is.Zero);
	}

	[Test]
	public async Task ReadbackFailureDoesNotRepeatCreationOrArchive() {
		var failure = new IOException("synthetic schema read failure");
		var transport = new RecordingNotionTransport { SchemaReadError = failure };
		await Assert.ThatAsync(() => Create(transport), Throws.TypeOf<CMSDatabaseCreationException>()
			.With.InnerException.SameAs(failure).And.Property("SetupPageArchived").True);
		Assert.That(transport.CreateCount, Is.EqualTo(1));
		Assert.That(transport.ArchiveCount, Is.EqualTo(1));
		Assert.That(transport.SchemaReadCount, Is.EqualTo(1));
		Assert.That(transport.QueryCount, Is.Zero);
	}

	[Test]
	public async Task CancellationAfterSetupRowCreationUsesIndependentBoundedCleanupToken() {
		var pending = new TaskCompletionSource<Page>(TaskCreationOptions.RunContinuationsAsynchronously);
		var transport = new RecordingNotionTransport { PendingPageUpdate = token => pending.Task.WaitAsync(token) };
		using var cancellation = new CancellationTokenSource();
		var creation = Create(transport, cancellation.Token);
		Assert.That(creation.IsCompleted, Is.False);
		cancellation.Cancel();
		await Assert.ThatAsync(async () => await creation, Throws.TypeOf<CMSDatabaseCreationException>()
			.With.Property("SetupPageArchived").True.And.InnerException.InstanceOf<OperationCanceledException>());
		Assert.That(transport.CleanupToken, Is.Not.EqualTo(cancellation.Token));
		Assert.That(transport.CleanupToken.CanBeCanceled, Is.True, "Cleanup uses its own bounded deadline.");
		Assert.That(transport.CleanupToken.IsCancellationRequested, Is.False);
		Assert.That(transport.SetupPageArchived, Is.True);
		Assert.That(transport.CreateCount, Is.EqualTo(1));
		Assert.That(transport.PageCreateCount, Is.EqualTo(1));
	}

	[TestCase("count")]
	[TestCase("type")]
	[TestCase("color")]
	[TestCase("order")]
	public async Task ReadbackMismatchReportsPartialDatabaseAfterCleanup(string mismatch) {
		var transport = new RecordingNotionTransport { CorruptSchema = schema => {
			var options = (JArray)schema["Themes"]["multi_select"]["options"];
			switch (mismatch) {
				case "count": options.RemoveAt(0); break;
				case "type": schema["Summary"]["type"] = "number"; break;
				case "color": options[0]["color"] = "red"; break;
				case "order": var first = options[0]; options.RemoveAt(0); options.Add(first); break;
			}
		} };
		await Assert.ThatAsync(() => Create(transport), Throws.TypeOf<CMSDatabaseCreationException>().With.Property("SetupPageArchived").True);
		Assert.That(transport.QueryCount, Is.Zero);
		Assert.That(transport.SetupPageArchived, Is.True);
	}

	[Test]
	public async Task UnexpectedActiveRowsPreventSuccessfulBlankDatabaseResult() {
		var transport = new RecordingNotionTransport { LeaveActiveRow = true };
		await Assert.ThatAsync(() => Create(transport), Throws.TypeOf<CMSDatabaseCreationException>().With.Property("SetupPageArchived").True);
		Assert.That(transport.QueryCount, Is.EqualTo(1));
		Assert.That(transport.ArchiveCount, Is.EqualTo(1), "Do not remove unexpected or concurrent user content.");
	}

	private static Task<Database> Create(RecordingNotionTransport transport, CancellationToken cancellationToken = default)
		=> new CMSDatabaseCreator(transport.CreateClient(), new CMSDatabaseTemplate(), TimeSpan.Zero).CreateAsync(ParentPageID, "New website", cancellationToken);
}
