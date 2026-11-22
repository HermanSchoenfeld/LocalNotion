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
using System.Threading;
using System.Threading.Tasks;
using LocalNotion.Core;
using Newtonsoft.Json;
using Notion.Client;
using NUnit.Framework;

namespace LocalNotion.Core.Tests;

[TestFixture]
[Category("Integration")]
[FixtureLifeCycle(LifeCycle.InstancePerTestCase)]
[Parallelizable(ParallelScope.Children)]
public class DatabaseRowCleanupTests {

	private record CachedRow(LocalNotionPage Resource, Page Native, string RenderPath);

	private readonly DateTime _edited = new(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
	private readonly string _databaseID = Guid.NewGuid().ToString();
	private readonly string _primarySourceID = Guid.NewGuid().ToString();
	private readonly string _secondarySourceID = Guid.NewGuid().ToString();
	private readonly Dictionary<string, string> _bodyIDs = new();
	private string _root;
	private CMSLocalNotionRepository _repository;
	private Database _database;

	[SetUp]
	public async Task SetUp() {
		// Every case uses a private repository and an in-memory transport, without credentials.
		_root = Path.Combine(Path.GetTempPath(), "localnotion-row-cleanup-" + Guid.NewGuid().ToString("N"));
		Directory.CreateDirectory(_root);
		_repository = (CMSLocalNotionRepository)await LocalNotionRepository.CreateNew(_root, cmsDatabaseID: _databaseID);
		_database = new Database {
			Id = _databaseID, Parent = new WorkspaceParent(), Title = [Text("CMS")], Description = [],
			CreatedTime = _edited.AddDays(-1), LastEditedTime = _edited,
			DataSources = [new DataSourceReferenceResponse { DataSourceId = _primarySourceID }, new DataSourceReferenceResponse { DataSourceId = _secondarySourceID }]
		};
		_repository.AddObject(_database);
		_repository.AddResourceGraph(new NotionObjectGraph { ObjectID = _databaseID });
		_repository.AddResource(new LocalNotionDatabase {
			ID = _databaseID, PrimaryDataSourceID = _primarySourceID, Name = "cms", Title = "CMS", Keywords = [],
			LastEditedOn = _edited, LastSyncedOn = _edited.AddMinutes(10)
		});
	}

	[TearDown]
	public void TearDown() {
		_repository?.Dispose();
		if (!Directory.Exists(_root))
			return;
		var path = Path.GetFullPath(_root);
		Assert.That(Path.GetDirectoryName(path), Is.EqualTo(Path.GetFullPath(Path.GetTempPath()).TrimEnd(Path.DirectorySeparatorChar)));
		Assert.That(Path.GetFileName(path), Does.StartWith("localnotion-row-cleanup-"));
		Directory.Delete(path, recursive: true);
	}

	[Test]
	public async Task DeletedPrimaryRowRemovesRawAndCmsRouteWithoutChangingRetainedAuthentication([Values(false, true)] bool suppliedTimestamp) {
		// A modern primary-source row disappears; a second authenticated row remains.
		var removed = await AddCachedRow(PrimaryParent(), _primarySourceID);
		var kept = await AddCachedRow(PrimaryParent(), _primarySourceID);
		var before = CaptureRow(kept.Resource);
		var transport = Transport(kept.Native);
		await Download(transport, suppliedTimestamp);

		Assert.That(_repository.ContainsResource(removed.Resource.ID), Is.False);
		Assert.That(_repository.ContainsCmsItem(removed.Resource.CMSProperties.CustomSlug), Is.False);
		Assert.That(File.Exists(removed.RenderPath), Is.False);
		Assert.That(CaptureRow(kept.Resource), Is.EqualTo(before), "Retained body, parent, raw output, CMS authentication and metadata stay unchanged.");
		Assert.That(_repository.GetPage(kept.Resource.ID).ParentResourceID, Is.EqualTo(_primarySourceID));
		Assert.That(transport.QueriedSources, Is.EqualTo(new[] { _primarySourceID }));
		Assert.That(transport.PageReads, Is.EqualTo(new[] { kept.Resource.ID }), "No extra per-row requests are added for deleted cached rows.");
		Assert.That(transport.OtherRequests, Is.Empty);
	}

	[Test]
	public async Task ReturnedPrimaryRowsKeepTheirNativeParentBodyAndAuthentication() {
		var kept = await AddCachedRow(PrimaryParent(), _primarySourceID);
		var before = CaptureRow(kept.Resource);
		var transport = Transport(kept.Native);
		await Download(transport);

		Assert.That(_repository.GetPage(kept.Resource.ID), Is.SameAs(kept.Resource));
		Assert.That(CaptureRow(kept.Resource), Is.EqualTo(before));
		Assert.That(_repository.GetCMSItem(kept.Resource.CMSProperties.CustomSlug).Auth, Is.EqualTo("fixture-user:fixture-password"));
		Assert.That(transport.QueriedSources, Is.EqualTo(new[] { _primarySourceID }));
		Assert.That(transport.OtherRequests, Is.Empty);
	}

	[Test]
	public async Task LegacyDatabaseParentRowsAreStillReconciled([Values(false, true)] bool returnedByQuery) {
		var row = await AddCachedRow(new DatabaseParent { DatabaseId = _databaseID }, _databaseID);
		var before = CaptureRow(row.Resource);
		var transport = returnedByQuery ? Transport(row.Native) : Transport();
		await Download(transport);

		Assert.That(_repository.ContainsResource(row.Resource.ID), Is.EqualTo(returnedByQuery));
		Assert.That(_repository.ContainsCmsItem(row.Resource.CMSProperties.CustomSlug), Is.EqualTo(returnedByQuery));
		Assert.That(File.Exists(row.RenderPath), Is.EqualTo(returnedByQuery));
		if (returnedByQuery)
			Assert.That(CaptureRow(row.Resource), Is.EqualTo(before));
		Assert.That(transport.OtherRequests, Is.Empty);
	}

	[Test]
	public async Task SecondarySourceRowsAreNotPrunedByThePrimarySourceQuery() {
		// A database header advertises two sources, but this operation queries only its primary.
		var keptPrimary = await AddCachedRow(PrimaryParent(), _primarySourceID);
		var secondary = await AddCachedRow(new DatasourceParent { DatabaseId = _databaseID, DataSourceId = _secondarySourceID }, _secondarySourceID, cms: false);
		var before = CaptureRow(secondary.Resource);
		var transport = Transport(keptPrimary.Native);
		await Download(transport, suppliedTimestamp: false);

		Assert.That(CaptureRow(secondary.Resource), Is.EqualTo(before));
		Assert.That(_repository.GetPage(secondary.Resource.ID).ParentResourceID, Is.EqualTo(_secondarySourceID));
		Assert.That(transport.QueriedSources, Is.EqualTo(new[] { _primarySourceID }));
		Assert.That(transport.PageReads, Is.EqualTo(new[] { keptPrimary.Resource.ID }));
		Assert.That(transport.OtherRequests, Is.Empty);
	}

	[Test]
	public async Task RowsOfAnotherDatabaseRemainUnchanged([Values(false, true)] bool modernParent) {
		var otherDatabaseID = Guid.NewGuid().ToString();
		var otherSourceID = Guid.NewGuid().ToString();
		_repository.AddResource(new LocalNotionDatabase { ID = otherDatabaseID, PrimaryDataSourceID = otherSourceID, Name = "other", Title = "Other", Keywords = [] });
		var parent = modernParent
			? (IParentOfPage)new DatasourceParent { DatabaseId = otherDatabaseID, DataSourceId = otherSourceID }
			: new DatabaseParent { DatabaseId = otherDatabaseID };
		var other = await AddCachedRow(parent, modernParent ? otherSourceID : otherDatabaseID, cms: false);
		var removed = await AddCachedRow(PrimaryParent(), _primarySourceID);
		var before = CaptureRow(other.Resource);
		var transport = Transport();
		await Download(transport);

		Assert.That(_repository.ContainsResource(removed.Resource.ID), Is.False);
		Assert.That(CaptureRow(other.Resource), Is.EqualTo(before));
		Assert.That(transport.QueriedSources, Is.EqualTo(new[] { _primarySourceID }));
		Assert.That(transport.PageReads, Is.Empty);
		Assert.That(transport.OtherRequests, Is.Empty);
	}

	[Test]
	public async Task NonPageResourcesAreNotDatabaseRows([Values(false, true)] bool sourceParent) {
		var fileID = Guid.NewGuid().ToString();
		var file = LocalNotionFile.Parse(fileID, "keep.bin", sourceParent ? _primarySourceID : _databaseID, "application/octet-stream");
		_repository.AddResource(file);
		var temporary = Path.Combine(_root, "file.tmp");
		await File.WriteAllTextAsync(temporary, "Keep this unrelated resource.");
		_repository.ImportResourceRender(fileID, RenderType.File, temporary);
		var filePath = Path.GetFullPath(file.GetRender(RenderType.File).LocalPath, _root);
		var before = File.ReadAllBytes(filePath);
		var removed = await AddCachedRow(PrimaryParent(), _primarySourceID);
		var transport = Transport();
		await Download(transport);

		Assert.That(_repository.ContainsResource(removed.Resource.ID), Is.False);
		Assert.That(_repository.GetFile(fileID), Is.SameAs(file));
		Assert.That(File.ReadAllBytes(filePath), Is.EqualTo(before));
		Assert.That(transport.QueriedSources, Is.EqualTo(new[] { _primarySourceID }));
		Assert.That(transport.OtherRequests, Is.Empty);
	}

	[Test]
	public async Task MissingDataSourceDoesNotEstablishThatCachedRowsWereDeleted() {
		var kept = await AddCachedRow(new DatabaseParent { DatabaseId = _databaseID }, _databaseID);
		var before = CaptureRow(kept.Resource);
		_database.DataSources = [];
		_database.LastEditedTime = _edited.AddDays(1);
		var transport = Transport();
		await Download(transport, suppliedTimestamp: false);

		Assert.That(CaptureRow(kept.Resource), Is.EqualTo(before));
		Assert.That(transport.QueriedSources, Is.Empty);
		Assert.That(transport.PageReads, Is.Empty);
		Assert.That(transport.OtherRequests, Is.Empty);
	}

	[Test]
	public async Task FailedSourceQueryCannotPruneCachedRows() {
		var kept = await AddCachedRow(PrimaryParent(), _primarySourceID);
		var before = CaptureRow(kept.Resource);
		var transport = Transport();
		transport.FailQuery = true;
		await Assert.ThatAsync(async () => await Download(transport), Throws.TypeOf<InvalidOperationException>().With.Message.EqualTo("Fixture query failed."));

		Assert.That(CaptureRow(kept.Resource), Is.EqualTo(before));
		Assert.That(transport.QueriedSources, Is.EqualTo(new[] { _primarySourceID }));
		Assert.That(transport.PageReads, Is.Empty);
		Assert.That(transport.OtherRequests, Is.Empty);
	}

	private Task<LocalNotionResource[]> Download(DatabaseTransport transport, bool suppliedTimestamp = true)
		=> new NotionSyncOrchestrator(transport.CreateClient(), _repository).DownloadDatabaseAsync(
			_databaseID, suppliedTimestamp ? _edited : null, new DownloadOptions { Render = false, FaultTolerant = false }
		);

	private DatabaseTransport Transport(params Page[] returnedRows) => new(Clone(_database), _primarySourceID, returnedRows);

	private DatasourceParent PrimaryParent() => new() { DatabaseId = _databaseID, DataSourceId = _primarySourceID };

	private async Task<CachedRow> AddCachedRow(IParentOfPage parent, string localParentID, bool cms = true) {
		// Seed a realistic unchanged Page, body graph and prior raw export for the real sync path.
		var id = Guid.NewGuid().ToString();
		var native = new Page {
			Id = id, Parent = parent, CreatedTime = _edited.AddDays(-1), LastEditedTime = _edited,
			Properties = new Dictionary<string, PropertyValue> { ["title"] = new TitlePropertyValue { Title = [Text("Row " + id)] } }
		};
		var paragraph = new ParagraphBlock {
			Id = Guid.NewGuid().ToString(), Parent = new PageParent { PageId = id },
			Paragraph = new() { RichText = [Text("Keep this technical content.")] }
		};
		_bodyIDs[id] = paragraph.Id;
		_repository.AddObject(native);
		_repository.AddObject(paragraph);
		_repository.AddResourceGraph(new NotionObjectGraph { ObjectID = id, Children = [new() { ObjectID = paragraph.Id }] });
		var local = LocalNotionHelper.ParsePage(native);
		local.ParentResourceID = localParentID;
		local.LastSyncedOn = _edited.AddMinutes(10);
		local.Keywords = ["technical", "content"];
		local.CMSProperties = !cms ? null : new CMSProperties {
			PageType = CMSPageType.Page, Status = CMSPageStatus.Published, Root = "services", CustomSlug = "services/row-" + id,
			Authentication = "fixture-user:fixture-password", Tags = [], Themes = []
		};
		_repository.AddResource(local);
		var temporary = Path.Combine(_root, "row.tmp");
		await File.WriteAllTextAsync(temporary, "<main>Prior authored output " + id + "</main>");
		_repository.ImportResourceRender(id, RenderType.HTML, temporary);
		await _repository.SaveAsync();
		return new CachedRow(local, native, Path.GetFullPath(local.GetRender(RenderType.HTML).LocalPath, _root));
	}

	private string CaptureRow(LocalNotionPage row) => JsonConvert.SerializeObject(new {
		Resource = _repository.GetPage(row.ID), Root = _repository.GetObject(row.ID),
		Body = _repository.GetObject(_bodyIDs[row.ID]), Graph = _repository.GetEditableResourceGraph(row.ID),
		Html = File.ReadAllText(Path.GetFullPath(row.GetRender(RenderType.HTML).LocalPath, _root)),
		Auth = row.CMSProperties != null ? _repository.GetCMSItem(row.CMSProperties.CustomSlug).Auth : null
	});

	private static RichTextText Text(string value) => new() { PlainText = value, Text = new() { Content = value } };

	private static T Clone<T>(T value) => JsonConvert.DeserializeObject<T>(JsonConvert.SerializeObject(value));

	private sealed class DatabaseTransport : Notion.Client.IRestClient {
		private readonly Database _database;
		private readonly string _sourceID;
		private readonly Page[] _rows;
		private readonly List<string> _queriedSources = new();
		private readonly List<string> _pageReads = new();
		private readonly List<string> _otherRequests = new();

		public DatabaseTransport(Database database, string sourceID, Page[] rows) {
			_database = database;
			_sourceID = sourceID;
			_rows = rows;
		}

		public bool FailQuery { get; set; }
		public string[] QueriedSources => _queriedSources.ToArray();
		public string[] PageReads => _pageReads.ToArray();
		public string[] OtherRequests => _otherRequests.ToArray();

		public INotionClient CreateClient() => new NotionClient(this, null, new DatabasesClient(this), new PagesClient(this), null, null, new BlocksClient(this), null, null, new DataSourcesClient(this), null, null);

		public Task<T> GetAsync<T>(string uri, IDictionary<string, string> queryParams = null, IDictionary<string, string> headers = null, JsonSerializerSettings serializerSettings = null, CancellationToken cancellationToken = default) {
			cancellationToken.ThrowIfCancellationRequested();
			if (uri == "/v1/databases/" + _database.Id)
				return Task.FromResult((T)(object)Clone(_database));
			if (uri == "/v1/data_sources/" + _sourceID)
				return Task.FromResult((T)(object)new DataSource { Id = _sourceID, Parent = new DatabaseParent { DatabaseId = _database.Id }, Title = [], Description = [], Properties = new Dictionary<string, DataSourcePropertyConfig>() });
			var row = _rows.FirstOrDefault(page => uri == "/v1/pages/" + page.Id);
			if (row != null) {
				_pageReads.Add(row.Id);
				return Task.FromResult((T)(object)Clone(row));
			}
			_otherRequests.Add("GET " + uri);
			throw new InvalidOperationException("Unexpected fixture request: " + uri);
		}

		public Task<T> PostAsync<T>(string uri, object body, IEnumerable<KeyValuePair<string, string>> queryParams = null, IDictionary<string, string> headers = null, JsonSerializerSettings serializerSettings = null, IBasicAuthenticationParameters basicAuthenticationParameters = null, CancellationToken cancellationToken = default) {
			cancellationToken.ThrowIfCancellationRequested();
			if (uri == "/v1/data_sources/" + _sourceID + "/query") {
				_queriedSources.Add(_sourceID);
				if (FailQuery)
					throw new InvalidOperationException("Fixture query failed.");
				return Task.FromResult((T)(object)new QueryDataSourceResponse { Results = _rows.Select(page => (IQueryDataSourceResponseObject)Clone(page)).ToList(), HasMore = false });
			}
			_otherRequests.Add("POST " + uri);
			throw new InvalidOperationException("Unexpected fixture request: " + uri);
		}

		public Task<T> PostAsync<T>(string uri, ISendFileUploadFormDataParameters formData, IEnumerable<KeyValuePair<string, string>> queryParams = null, IDictionary<string, string> headers = null, JsonSerializerSettings serializerSettings = null, IBasicAuthenticationParameters basicAuthenticationParameters = null, CancellationToken cancellationToken = default) => throw new InvalidOperationException("No upload expected.");
		public Task<T> PatchAsync<T>(string uri, object body, IDictionary<string, string> queryParams = null, IDictionary<string, string> headers = null, JsonSerializerSettings serializerSettings = null, CancellationToken cancellationToken = default) => throw new InvalidOperationException("No PATCH expected.");
		public Task<T> DeleteAsync<T>(string uri, IDictionary<string, string> queryParams = null, IDictionary<string, string> headers = null, JsonSerializerSettings serializerSettings = null, CancellationToken cancellationToken = default) => throw new InvalidOperationException("No DELETE expected.");

		public Task DeleteAsync(string uri, IDictionary<string, string> queryParams = null, IDictionary<string, string> headers = null, CancellationToken cancellationToken = default) => throw new InvalidOperationException("No DELETE expected.");
	}
}
