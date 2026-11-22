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
using Sphere10.Framework;

namespace LocalNotion.Core.Tests;

[TestFixture]
[Category("Integration")]
[FixtureLifeCycle(LifeCycle.InstancePerTestCase)]
[Parallelizable(ParallelScope.Children)]
public class PageMetadataFreshnessTests {
	private readonly DateTime _edited = new(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
	private readonly string _databaseID = Guid.NewGuid().ToString();
	private readonly string _dataSourceID = Guid.NewGuid().ToString();
	private readonly string _parentID = Guid.NewGuid().ToString();
	private readonly string _pageID = Guid.NewGuid().ToString();
	private readonly string _fileID = Guid.NewGuid().ToString();
	private string _root;
	private CMSLocalNotionRepository _repository;
	private Page _cachedPage;

	[SetUp]
	public async Task SetUp() {
		_root = Path.Combine(Path.GetTempPath(), "localnotion-page-metadata-" + Guid.NewGuid().ToString("N"));
		Directory.CreateDirectory(_root);
		_repository = (CMSLocalNotionRepository)await LocalNotionRepository.CreateNew(_root, cmsDatabaseID: _databaseID);
		_repository.AddResource(new LocalNotionDatabase { ID = _databaseID, PrimaryDataSourceID = _dataSourceID, Name = "cms", Title = "CMS" });
		_repository.AddResource(new LocalNotionPage {
			ID = _parentID, ParentResourceID = _databaseID, Name = "ai", Title = "AI overview", Keywords = [],
			CMSProperties = new CMSProperties { PageType = CMSPageType.Section, Status = CMSPageStatus.Published, Root = "Services", CustomSlug = "services#ai", Tags = [Constants.TagHideEmbeddedPages], Themes = [] }
		});
	}

	[TearDown]
	public void TearDown() {
		_repository?.Dispose();
		var path = Path.GetFullPath(_root);
		if (!path.StartsWith(Path.GetFullPath(Path.GetTempPath()), StringComparison.OrdinalIgnoreCase))
			throw new InvalidOperationException("Fixture escaped its temporary directory.");
		Directory.Delete(path, true);
	}

	[Test]
	public async Task SameTimestampMoveRefreshesMetadataWithoutDownloadingBody([Values(false, true)] bool suppliedTimestamp) {
		var local = await AddCachedPage();
		var body = BodyFiles();
		var graph = GraphFiles();
		var filePath = Path.GetFullPath(_repository.GetFile(_fileID).GetRender(RenderType.File).LocalPath, _root);
		var media = File.ReadAllBytes(filePath);
		var oldRenderPath = local.GetRender(RenderType.HTML).LocalPath;
		var observations = new List<string>();
		_repository.ResourceUpdating += (_, id) => { if (id == _pageID) observations.Add(_repository.GetPage(id).ParentResourceID); };
		_repository.ResourceUpdated += (_, resource) => { if (resource.ID == _pageID) observations.Add(resource.ParentResourceID); };
		var fresh = Page(new PageParent { PageId = _parentID });
		var transport = new PageOnlyTransport(fresh);
		var changed = await new NotionSyncOrchestrator(transport.CreateClient(), _repository).DownloadPageAsync(
			_pageID, suppliedTimestamp ? _edited : null, new DownloadOptions { Render = false, FaultTolerant = false }
		);
		var saved = _repository.GetPage(_pageID);
		Assert.That(transport.PageReads, Is.EqualTo(1));
		Assert.That(transport.OtherRequests, Is.Zero, "No block/body or file transport is needed for the unchanged body.");
		Assert.That(changed.OfType<LocalNotionPage>().Select(page => page.ID), Is.EqualTo(new[] { _pageID }));
		Assert.That(saved.ParentResourceID, Is.EqualTo(_parentID));
		Assert.That(saved.LastEditedOn, Is.EqualTo(_edited));
		Assert.That(saved.CMSProperties.CustomSlug, Is.EqualTo("services/ai-services"));
		Assert.That(saved.CMSProperties.Tags, Is.EqualTo(new[] { Constants.TagHideEmbeddedPages }));
		Assert.That(saved.CMSProperties.Themes, Is.Null);
		Assert.That(((Page)_repository.GetObject(_pageID)).Properties.Keys, Is.EqualTo(new[] { "title" }));
		Assert.That(observations, Is.EqualTo(new[] { _dataSourceID, _parentID }), "Before notification must still see the detached DTO's predecessor.");
		Assert.That(saved.GetRender(RenderType.HTML).LocalPath, Is.EqualTo(oldRenderPath));
		Assert.That(File.ReadAllBytes(filePath), Is.EqualTo(media));
		AssertFilesUnchanged(body, BodyFiles());
		AssertFilesUnchanged(graph, GraphFiles());
		Assert.That(_repository.GetCMSItem("services/ai-services").Parts, Is.EqualTo(new[] { _pageID }));
	}

	[Test]
	public async Task UnchangedPageKeepsTheBodyAndResourceFastPath([Values(false, true)] bool suppliedTimestamp) {
		await AddCachedPage();
		var before = Tools.Json.WriteToString(_repository.GetPage(_pageID));
		var body = BodyFiles();
		var graph = GraphFiles();
		var notifications = 0;
		_repository.ResourceUpdating += (_, id) => { if (id == _pageID) notifications++; };
		var transport = new PageOnlyTransport(Clone(_cachedPage));
		var changed = await new NotionSyncOrchestrator(transport.CreateClient(), _repository).DownloadPageAsync(
			_pageID, suppliedTimestamp ? _edited : null, new DownloadOptions { Render = false, FaultTolerant = false }
		);
		Assert.That(changed, Is.Empty);
		Assert.That(transport.PageReads, Is.EqualTo(1));
		Assert.That(transport.OtherRequests, Is.Zero);
		Assert.That(notifications, Is.Zero);
		Assert.That(Tools.Json.WriteToString(_repository.GetPage(_pageID)), Is.EqualTo(before));
		AssertFilesUnchanged(body, BodyFiles());
		AssertFilesUnchanged(graph, GraphFiles());
	}

	[Test]
	public async Task EquivalentLocalizedFileMetadataDoesNotCauseARefresh() {
		await AddCachedPage();
		var localUrl = LocalNotionRenderLink.GenerateUrl(_fileID, RenderType.File);
		_cachedPage.Cover = new FilePageCover { File = new InternalFileInfo { Url = localUrl } };
		_cachedPage.Icon = new FilePageIcon { File = new InternalFileInfo { Url = localUrl } };
		_repository.UpdateObject(_cachedPage);
		var fresh = Clone(_cachedPage);
		var signedUrl = "https://prod-files-secure.s3.us-west-2.amazonaws.com/bucket/" + _fileID + "/illustration.svg?renewed=1";
		((FilePageCover)fresh.Cover).File.Url = signedUrl;
		((FilePageIcon)fresh.Icon).File.Url = signedUrl;
		var transport = new PageOnlyTransport(fresh);
		var changed = await new NotionSyncOrchestrator(transport.CreateClient(), _repository).DownloadPageAsync(_pageID, options: new DownloadOptions { Render = false });
		Assert.That(changed, Is.Empty);
		Assert.That(transport.OtherRequests, Is.Zero);
		Assert.That(((FilePageCover)((Page)_repository.GetObject(_pageID)).Cover).File.Url, Is.EqualTo(localUrl));
	}

	[Test]
	public async Task SameParentMetadataChangeRefreshesOnlyThePageHeader() {
		await AddCachedPage(nativeChild: true);
		var body = BodyFiles();
		var fresh = Clone(_cachedPage);
		fresh.Icon = new EmojiPageIcon { Emoji = "🔧" };
		var transport = new PageOnlyTransport(fresh);
		var changed = await new NotionSyncOrchestrator(transport.CreateClient(), _repository).DownloadPageAsync(_pageID, _edited, new DownloadOptions { Render = false });
		Assert.That(changed.OfType<LocalNotionPage>().Select(page => page.ID), Is.EqualTo(new[] { _pageID }));
		Assert.That(_repository.GetPage(_pageID).Thumbnail.Data, Is.EqualTo("🔧"));
		Assert.That(transport.OtherRequests, Is.Zero);
		AssertFilesUnchanged(body, BodyFiles());
	}

	[Test]
	public async Task InvalidReplacementIsRejectedBeforeAnyUpdateNotification() {
		var stored = await AddCachedPage();
		var incoming = Clone(stored);
		incoming.Name = "";
		var notifications = 0;
		_repository.ResourceUpdating += (_, _) => notifications++;
		Assert.That(() => _repository.UpdateResource(incoming), Throws.InvalidOperationException);
		Assert.That(notifications, Is.Zero);
		Assert.That(_repository.GetPage(_pageID).Name, Is.EqualTo(stored.Name));
	}

	[TestCase(false, false)]
	[TestCase(false, true)]
	[TestCase(true, false)]
	[TestCase(true, true)]
	public async Task ResourceUpdatesHandleEveryCmsMembershipTransition(bool hadCms, bool hasCms) {
		var stored = await AddCachedPage(published: hadCms);
		var unrelated = "unrelated";
		_repository.AddOrUpdateCMSItem(new CMSItem { Slug = unrelated, Parts = [_parentID], ItemType = CMSItemType.Page, Dirty = false });
		var incoming = Clone(stored);
		incoming.CMSProperties = hasCms ? new CMSProperties { PageType = CMSPageType.Page, Status = CMSPageStatus.Published, Root = "Services", CustomSlug = "services/refreshed", Tags = [], Themes = [] } : null;
		_repository.UpdateResource(incoming);
		Assert.That(_repository.ContainsCmsItem("services/refreshed"), Is.EqualTo(hasCms));
		Assert.That(_repository.ContainsCmsItem("services/ai-services"), Is.False, "Only the old membership is retired when it changes or disappears.");
		Assert.That(_repository.GetCMSItem(unrelated).Parts, Is.EqualTo(new[] { _parentID }));
		Assert.That(_repository.GetCMSItem(unrelated).Dirty, Is.False);
		if (hasCms)
			Assert.That(_repository.GetCMSItem("services/refreshed").Parts, Is.EqualTo(new[] { _pageID }));
	}

	[Test]
	public async Task BookmarksRefreshEachPullAndPersistWithoutChangingTheNotionBody() {
		await AddCachedPage();
		var block = new BookmarkBlock { Id = Guid.NewGuid().ToString(), Bookmark = new() { Url = "https://example.test/preview" } };
		_repository.AddObject(block);
		var graph = _repository.GetEditableResourceGraph(_pageID);
		graph.Children = graph.Children.Append(new NotionObjectGraph { ObjectID = block.Id }).ToArray();
		_repository.UpdateResourceGraph(graph);
		var builder = new PreviewBuilder();
		var sync = new NotionSyncOrchestrator(new PageOnlyTransport(Clone(_cachedPage)).CreateClient(), _repository, builder);
		var options = new DownloadOptions { Render = false, FaultTolerant = false };
		using (sync.BeginPull()) {
			await sync.DownloadPageAsync(_pageID, options: options);
			await sync.DownloadPageAsync(_pageID, options: options);
			Assert.That(builder.Calls, Is.EqualTo(1));
		}
		await sync.DownloadPageAsync(_pageID, options: options);
		Assert.That(builder.Calls, Is.EqualTo(2));
		Assert.That(_repository.GetPage(_pageID).Bookmarks.Single().Title, Is.EqualTo("Preview 2"));
		await _repository.SaveAsync();
		_repository.Dispose();
		_repository = (CMSLocalNotionRepository)await LocalNotionRepository.Open(_root);
		var page = _repository.GetPage(_pageID);
		Assert.That(page.Bookmarks.Single().Summary, Is.EqualTo("Stored description"));
		var model = new NotionRenderModelBuilder(_repository).Build(page, _repository.GetEditableResourceGraph(_pageID), _repository.LoadObjects(graph), _root);
		var bookmark = model.Children.OfType<Sphere10.Framework.Web.VisualRenderer.BookmarkBlock>().Single();
		Assert.That(bookmark.Title, Is.EqualTo("Preview 2"));
		Assert.That(bookmark.Description, Is.EqualTo("Stored description"));
		Assert.That(builder.Calls, Is.EqualTo(2), "Projection reads persisted data without another fetch.");
	}

	private async Task<LocalNotionPage> AddCachedPage(bool nativeChild = false, bool published = true) {
		_cachedPage = Page(nativeChild ? new PageParent { PageId = _parentID } : new DatasourceParent { DataSourceId = _dataSourceID, DatabaseId = _databaseID });
		if (!nativeChild)
			_cachedPage.Properties["old datasource property"] = new RichTextPropertyValue { RichText = [Run("Old value")] };
		var paragraph = new ParagraphBlock { Id = Guid.NewGuid().ToString(), Parent = new PageParent { PageId = _pageID }, Paragraph = new() { RichText = [Run("Preserve technical body.")] } };
		var image = new ImageBlock { Id = Guid.NewGuid().ToString(), Parent = new PageParent { PageId = _pageID }, Image = new UploadedFile { File = new() { Url = LocalNotionRenderLink.GenerateUrl(_fileID, RenderType.File) } } };
		_repository.AddObject(_cachedPage);
		_repository.AddObject(paragraph);
		_repository.AddObject(image);
		_repository.AddResourceGraph(new NotionObjectGraph { ObjectID = _pageID, Children = [new() { ObjectID = paragraph.Id }, new() { ObjectID = image.Id }] });
		var local = LocalNotionHelper.ParsePage(_cachedPage);
		local.ParentResourceID = nativeChild ? _parentID : _dataSourceID;
		local.LastSyncedOn = _edited.AddMinutes(10);
		local.Keywords = ["technical", "body"];
		local.CMSProperties = !published ? null : new CMSProperties { PageType = CMSPageType.Page, Status = CMSPageStatus.Published, Root = "Services", CustomSlug = "services/ai-services", Tags = nativeChild ? [Constants.TagHideEmbeddedPages] : [Constants.TagHideHeader], Themes = nativeChild ? null : ["h3_typed", "h3_color_aux", "color_text_aux_primary", "cms_article_navigation"] };
		_repository.AddResource(local);
		_repository.AddResource(LocalNotionFile.Parse(_fileID, "illustration.svg", _pageID, "image/svg+xml"));
		var temporary = Path.Combine(_root, "fixture-body.tmp");
		await File.WriteAllTextAsync(temporary, "Preserve cached file bytes.");
		_repository.ImportResourceRender(_fileID, RenderType.File, temporary);
		await File.WriteAllTextAsync(temporary, "<main>Preserve previous render until normal generation.</main>");
		_repository.ImportResourceRender(_pageID, RenderType.HTML, temporary);
		await _repository.SaveAsync();
		return local;
	}

	private Page Page(IParentOfPage parent) => new() {
		Id = _pageID, Parent = parent, CreatedTime = _edited.AddDays(-1), LastEditedTime = _edited,
		Properties = new Dictionary<string, PropertyValue> { ["title"] = new TitlePropertyValue { Title = [Run("AI Services")] } }
	};

	private static RichTextText Run(string text) => new() { PlainText = text, Text = new() { Content = text } };
	private static T Clone<T>(T value) => JsonConvert.DeserializeObject<T>(JsonConvert.SerializeObject(value));
	private Dictionary<string, byte[]> BodyFiles() => Directory.EnumerateFiles(_repository.Paths.GetInternalResourceFolderPath(InternalResourceType.Objects, FileSystemPathType.Absolute), "*.json", SearchOption.AllDirectories)
		.Where(path => Tools.Json.ReadFromFile<IObject>(path).Id != _pageID).ToDictionary(path => path, File.ReadAllBytes);
	private Dictionary<string, byte[]> GraphFiles() => Directory.EnumerateFiles(_repository.Paths.GetInternalResourceFolderPath(InternalResourceType.Graphs, FileSystemPathType.Absolute), "*.json", SearchOption.AllDirectories).ToDictionary(path => path, File.ReadAllBytes);
	private static void AssertFilesUnchanged(Dictionary<string, byte[]> before, Dictionary<string, byte[]> after) {
		Assert.That(after.Keys, Is.EquivalentTo(before.Keys));
		foreach (var pair in before)
			Assert.That(after[pair.Key], Is.EqualTo(pair.Value), "Unchanged cached payload: " + pair.Key);
	}

	#region Inner Types
	private sealed class PreviewBuilder : IBookmarkBuilder {
		public int Calls { get; private set; }
		public Task<LocalNotionBookmark> Build(string url, CancellationToken cancellationToken = default) {
			Calls++;
			return Task.FromResult(new LocalNotionBookmark { Url = url, Title = "Preview " + Calls, Summary = "Stored description" });
		}
	}

	private sealed class PageOnlyTransport : Notion.Client.IRestClient {
		private readonly Page _page;
		public PageOnlyTransport(Page page) => _page = page;
		public int PageReads { get; private set; }
		public int OtherRequests { get; private set; }
		public INotionClient CreateClient() => new NotionClient(this, null, null, new PagesClient(this), null, null, new BlocksClient(this), null, null, null, null, null);
		public Task<T> GetAsync<T>(string uri, IDictionary<string, string> queryParams = null, IDictionary<string, string> headers = null, JsonSerializerSettings serializerSettings = null, CancellationToken cancellationToken = default) {
			cancellationToken.ThrowIfCancellationRequested();
			if (uri == "/v1/pages/" + _page.Id) {
				PageReads++;
				return Task.FromResult((T)(object)Clone(_page));
			}
			OtherRequests++;
			throw new InvalidOperationException("Unchanged body must not require another request: " + uri);
		}
		public Task<T> PostAsync<T>(string uri, object body, IEnumerable<KeyValuePair<string, string>> queryParams = null, IDictionary<string, string> headers = null, JsonSerializerSettings serializerSettings = null, IBasicAuthenticationParameters basicAuthenticationParameters = null, CancellationToken cancellationToken = default) => throw new InvalidOperationException("No POST expected.");
		public Task<T> PostAsync<T>(string uri, ISendFileUploadFormDataParameters formData, IEnumerable<KeyValuePair<string, string>> queryParams = null, IDictionary<string, string> headers = null, JsonSerializerSettings serializerSettings = null, IBasicAuthenticationParameters basicAuthenticationParameters = null, CancellationToken cancellationToken = default) => throw new InvalidOperationException("No upload expected.");
		public Task<T> PatchAsync<T>(string uri, object body, IDictionary<string, string> queryParams = null, IDictionary<string, string> headers = null, JsonSerializerSettings serializerSettings = null, CancellationToken cancellationToken = default) => throw new InvalidOperationException("No PATCH expected.");
		public Task<T> DeleteAsync<T>(string uri, IDictionary<string, string> queryParams = null, IDictionary<string, string> headers = null, JsonSerializerSettings serializerSettings = null, CancellationToken cancellationToken = default) => throw new InvalidOperationException("No DELETE expected.");

		public Task DeleteAsync(string uri, IDictionary<string, string> queryParams = null, IDictionary<string, string> headers = null, CancellationToken cancellationToken = default) => throw new InvalidOperationException("No DELETE expected.");
	}
	#endregion
}
