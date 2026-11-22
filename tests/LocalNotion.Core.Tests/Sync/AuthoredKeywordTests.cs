// Copyright (c) Herman Schoenfeld 2018 - Present. All rights reserved. (https://sphere10.com/products/localnotion)
// Author: Herman Schoenfeld
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
using AngleSharp.Html.Parser;
using LocalNotion.Core;
using Newtonsoft.Json;
using Notion.Client;
using NUnit.Framework;
using R = Sphere10.Framework.Web.VisualRenderer;

namespace LocalNotion.Core.Tests;

[TestFixture]
[Category("Integration")]
[FixtureLifeCycle(LifeCycle.InstancePerTestCase)]
[Parallelizable(ParallelScope.Children)]
public class AuthoredKeywordTests {

	private readonly string _databaseID = Guid.NewGuid().ToString();
	private readonly string _sourceID = Guid.NewGuid().ToString();
	private readonly string _pageID = Guid.NewGuid().ToString();
	private readonly DateTime _edited = new(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
	private string _root;
	private CMSLocalNotionRepository _repository;
	private ParagraphBlock _paragraph;
	private HeadingOneBlock _heading;
	private NotionObjectGraph _graph;

	[SetUp]
	public async Task SetUp() {
		_root = Path.Combine(Path.GetTempPath(), "localnotion-authored-keywords-" + Guid.NewGuid().ToString("N"));
		Directory.CreateDirectory(_root);
		_repository = (CMSLocalNotionRepository)await LocalNotionRepository.CreateNew(_root, cmsDatabaseID: _databaseID);
		_repository.AddObject(new Database { Id = _databaseID });
		_repository.AddResourceGraph(new NotionObjectGraph { ObjectID = _databaseID });
		_repository.AddResource(new LocalNotionDatabase { ID = _databaseID, PrimaryDataSourceID = _sourceID, Name = "cms", Title = "CMS", Keywords = [] });
		_paragraph = new ParagraphBlock { Id = Guid.NewGuid().ToString(), Parent = new PageParent { PageId = _pageID }, Paragraph = new() { RichText = [Text("Warehouse inventory indexing.")] } };
		_heading = new HeadingOneBlock { Id = Guid.NewGuid().ToString(), Parent = new PageParent { PageId = _pageID }, Heading_1 = new() { RichText = [Text("Technical workflow")] } };
		_graph = new NotionObjectGraph { ObjectID = _pageID, Children = [new() { ObjectID = _paragraph.Id }, new() { ObjectID = _heading.Id }] };
	}

	[TearDown]
	public void TearDown() {
		_repository?.Dispose();
		var path = Path.GetFullPath(_root);
		if (!path.StartsWith(Path.GetFullPath(Path.GetTempPath()), StringComparison.OrdinalIgnoreCase))
			throw new InvalidOperationException("Fixture escaped its temporary directory.");
		Directory.Delete(path, true);
	}

	[TestCase(null, false)]
	[TestCase("", false)]
	[TestCase("  , \t, ", false)]
	[TestCase(null, true)]
	[TestCase("", true)]
	[TestCase("  , \t, ", true)]
	public async Task MissingOrBlankKeywordsPreserveExistingAutomaticAndCachePaths(string value, bool bodyPull) {
		await Cache(null, ["previous automatic metadata"]);
		var expected = bodyPull ? AutomaticKeywords() : new[] { "previous automatic metadata" };
		var transport = await Pull(value, bodyPull);
		Assert.That(_repository.GetPage(_pageID).Keywords, Is.EqualTo(expected));
		Assert.That(transport.BodyReads, Is.EqualTo(bodyPull ? 1 : 0));
		Assert.That(_repository.GetPage(_pageID).CMSProperties.Summary, Is.EqualTo("Updated summary."));
	}

	[TestCase(CMSPageType.Page, false)]
	[TestCase(CMSPageType.Page, true)]
	[TestCase(CMSPageType.Gallery, false)]
	[TestCase(CMSPageType.Gallery, true)]
	public async Task AuthoredKeywordsOverrideBodyAndMetadataPullsAndReachPublishedMetadata(CMSPageType type, bool bodyPull) {
		await Cache(null, ["previous automatic metadata"], type);
		var transport = await Pull(" Sphere10 Framework, C#, , ObjectSpaces ", bodyPull, type);
		var expected = new[] { "Sphere10 Framework", "C#", "ObjectSpaces" };
		var saved = _repository.GetPage(_pageID);
		Assert.That(saved.CMSProperties.Keywords, Is.EqualTo(expected));
		Assert.That(saved.Keywords, Is.EqualTo(expected));
		Assert.That(transport.BodyReads, Is.EqualTo(bodyPull ? 1 : 0));
		var pageItem = _repository.GetCMSItem(saved.CMSProperties.CustomSlug);
		var collection = _repository.CMSItems.Single(item => item.Slug == "articles");
		var manager = new RenderingManager(_repository);
		manager.PrepareRenderPaths([], cmsItems: _repository.CMSItems);
		var builder = new CmsRenderModelBuilder(_repository, Path.Combine(_root, "cms"));
		foreach (var item in new[] { pageItem, collection }) {
			var document = builder.Build(item);
			// The article index retains its own existing overview metadata policy;
			// Gallery shells aggregate their cards, while article pages own their terms.
			var effective = type == CMSPageType.Page && item == collection ? Array.Empty<string>() : expected;
			Assert.That(document.Keywords, Is.EqualTo(effective));
			var html = new R.HtmlRenderer().Render(document, new R.RenderOptions { Environment = R.RenderEnvironment.Online }).Html;
			var parsed = new HtmlParser().ParseDocument(html);
			Assert.That(parsed.QuerySelector("meta[name=keywords]")?.GetAttribute("content"), Is.EqualTo(string.Join(", ", effective)));
		}
		if (type == CMSPageType.Page) {
			var document = builder.Build(pageItem);
			Assert.That(document.Children.OfType<R.BreadcrumbBlock>().Count(), Is.EqualTo(1));
			Assert.That(document.Children.OfType<R.TableOfContentsBlock>().Count(), Is.EqualTo(1));
		}
	}

	[TestCase(null)]
	[TestCase("")]
	[TestCase(" , \t , ")]
	public async Task ClearingAuthoredKeywordsOnMetadataOnlyPullRestoresAutomaticBodyKeywords(string value) {
		await Cache("Sphere10 Framework, Old override", ["Sphere10 Framework", "Old override"]);
		var expected = AutomaticKeywords();
		var transport = await Pull(value, false);
		var saved = _repository.GetPage(_pageID);
		Assert.That(saved.Keywords, Is.EqualTo(expected));
		Assert.That(saved.Keywords, Does.Not.Contain("Old override"));
		Assert.That(transport.BodyReads, Is.Zero, "Clearing the override uses the already cached native body.");
	}

	[Test]
	public async Task ClearingAuthoredKeywordsWithoutCachedGraphUsesTheNormalBodyRefresh() {
		await Cache("Sphere10 Framework, Old override", ["Sphere10 Framework", "Old override"]);
		var expected = AutomaticKeywords();
		_repository.RemoveResourceGraph(_pageID);
		var transport = await Pull("", false);
		Assert.That(transport.BodyReads, Is.EqualTo(1));
		Assert.That(_repository.GetPage(_pageID).Keywords, Is.EqualTo(expected));
	}

	[Test]
	public async Task AuthoredKeywordsRetainMoreThanTheAutomaticTenPhraseLimit() {
		await Cache(null, []);
		var expected = Enumerable.Range(1, 12).Select(index => "Authored term " + index).ToArray();
		await Pull(string.Join(", ", expected), true);
		Assert.That(_repository.GetPage(_pageID).Keywords, Is.EqualTo(expected));
	}

	[Test]
	public void KeywordsAreOptionalAndPreserveAuthoredSpellingAndOrder() {
		Assert.That(CMSHelper.ParseCMSProperties("article", Native(null)).Keywords, Is.Null);
		var page = Native(" C#, Sphere10 Framework, object spaces, C# ");
		Assert.That(CMSHelper.ParseCMSProperties("article", page).Keywords, Is.EqualTo(new[] { "C#", "Sphere10 Framework", "object spaces", "C#" }));
		page.Properties[Constants.KeywordsPropertyName] = new NumberPropertyValue { Number = 42 };
		Assert.That(CMSHelper.ParseCMSProperties("article", page).Keywords, Is.Null, "An unrelated older non-text property does not become a new pull failure.");
	}

	private async Task Cache(string authored, string[] effective, CMSPageType type = CMSPageType.Page) {
		var native = Native(authored, type);
		_repository.AddObject(native);
		_repository.AddObject(_paragraph);
		_repository.AddObject(_heading);
		_repository.AddResourceGraph(_graph);
		var local = LocalNotionHelper.ParsePage(native);
		local.ParentResourceID = _sourceID;
		local.LastSyncedOn = _edited.AddMinutes(10);
		local.CMSProperties = CMSHelper.ParseCMSProperties(local.Name, native);
		local.Keywords = effective;
		_repository.AddResource(local);
		await _repository.SaveAsync();
	}

	private async Task<KeywordTransport> Pull(string authored, bool bodyPull, CMSPageType type = CMSPageType.Page) {
		var fresh = Native(authored, type);
		fresh.Properties[Constants.SummaryPropertyName] = Rich("Updated summary.");
		var transport = new KeywordTransport(fresh, [_paragraph, _heading]);
		await new NotionSyncOrchestrator(transport.CreateClient(), _repository).DownloadPageAsync(
			_pageID, _edited, new DownloadOptions { Render = false, FaultTolerant = false, ForceRefresh = bodyPull }
		);
		return transport;
	}

	private string[] AutomaticKeywords() {
		return LocalNotionHelper.ExtractDefaultPageKeywords(_graph, new Dictionary<string, IObject> {
			[_pageID] = Native(null), [_paragraph.Id] = _paragraph, [_heading.Id] = _heading
		});
	}

	private Page Native(string keywords, CMSPageType type = CMSPageType.Page) {
		var values = new Dictionary<string, PropertyValue> {
			[Constants.TitlePropertyName] = new TitlePropertyValue { Title = [Text("Keyword article")] },
			[Constants.PageTypePropertyName] = new SelectPropertyValue { Select = new() { Name = type.ToString() } },
			[Constants.StatusPropertyName] = new SelectPropertyValue { Select = new() { Name = "Published" } },
			[Constants.PublishOnPropertyName] = new DatePropertyValue(),
			[Constants.ThemesPropertyName] = new MultiSelectPropertyValue { MultiSelect = [] },
			[Constants.TagsPropertyName] = new MultiSelectPropertyValue { MultiSelect = [] },
			[Constants.SlugPropertyName] = Rich(""),
			[Constants.SequencePropertyName] = new NumberPropertyValue { Number = 1 },
			[Constants.RootCategoryPropertyName] = Rich("Articles"),
			[Constants.Category1PropertyName] = Rich(""),
			[Constants.Category2PropertyName] = Rich(""),
			[Constants.Category3PropertyName] = Rich(""),
			[Constants.Category4PropertyName] = Rich(""),
			[Constants.Category5PropertyName] = Rich(""),
			[Constants.SummaryPropertyName] = Rich("Original summary."),
			[Constants.AuthenticationPropertyName] = Rich(""),
			[Constants.CreatedByPropertyName] = new CreatedByPropertyValue(),
			[Constants.CreatedOnPropertyName] = new CreatedTimePropertyValue(),
			[Constants.EditedByPropertyName] = new LastEditedByPropertyValue(),
			[Constants.EditedOnPropertyName] = new LastEditedTimePropertyValue()
		};
		if (keywords != null)
			values[Constants.KeywordsPropertyName] = Rich(keywords);
		return new Page { Id = _pageID, Parent = new DatasourceParent { DataSourceId = _sourceID, DatabaseId = _databaseID }, CreatedTime = _edited.AddDays(-1), LastEditedTime = _edited, Properties = values };
	}

	private static RichTextText Text(string value) {
		return new() { PlainText = value, Text = new() { Content = value } };
	}

	private static RichTextPropertyValue Rich(string value) {
		return new() { RichText = [Text(value)] };
	}

	private static T Clone<T>(T value) {
		return JsonConvert.DeserializeObject<T>(JsonConvert.SerializeObject(value));
	}

	#region Inner Types
	private sealed class KeywordTransport : Notion.Client.IRestClient {
		private readonly Page _page;
		private readonly IBlock[] _blocks;
		public KeywordTransport(Page page, IBlock[] blocks) {
			_page = page;
			_blocks = blocks;
		}
		public int BodyReads { get; private set; }
		public INotionClient CreateClient() {
			return new NotionClient(this, null, null, new PagesClient(this), null, null, new BlocksClient(this), null, null, null, null, null);
		}
		public Task<T> GetAsync<T>(string uri, IDictionary<string, string> queryParams = null, IDictionary<string, string> headers = null, JsonSerializerSettings serializerSettings = null, CancellationToken cancellationToken = default) {
			cancellationToken.ThrowIfCancellationRequested();
			if (uri == "/v1/pages/" + _page.Id)
				return Task.FromResult((T)(object)Clone(_page));
			if (uri == "/v1/blocks/" + _page.Id + "/children") {
				BodyReads++;
				return Task.FromResult((T)(object)new RetrieveChildrenResponse { Results = _blocks.ToList(), HasMore = false });
			}
			throw new InvalidOperationException("Unexpected fixture request: " + uri);
		}
		public Task<T> PostAsync<T>(string uri, object body, IEnumerable<KeyValuePair<string, string>> queryParams = null, IDictionary<string, string> headers = null, JsonSerializerSettings serializerSettings = null, IBasicAuthenticationParameters basicAuthenticationParameters = null, CancellationToken cancellationToken = default) {
			throw new InvalidOperationException("No POST expected.");
		}

		public Task<T> PostAsync<T>(string uri, ISendFileUploadFormDataParameters formData, IEnumerable<KeyValuePair<string, string>> queryParams = null, IDictionary<string, string> headers = null, JsonSerializerSettings serializerSettings = null, IBasicAuthenticationParameters basicAuthenticationParameters = null, CancellationToken cancellationToken = default) {
			throw new InvalidOperationException("No upload expected.");
		}

		public Task<T> PatchAsync<T>(string uri, object body, IDictionary<string, string> queryParams = null, IDictionary<string, string> headers = null, JsonSerializerSettings serializerSettings = null, CancellationToken cancellationToken = default) {
			throw new InvalidOperationException("No PATCH expected.");
		}

		public Task<T> DeleteAsync<T>(string uri, IDictionary<string, string> queryParams = null, IDictionary<string, string> headers = null, JsonSerializerSettings serializerSettings = null, CancellationToken cancellationToken = default) => throw new InvalidOperationException("No DELETE expected.");

		public Task DeleteAsync(string uri, IDictionary<string, string> queryParams = null, IDictionary<string, string> headers = null, CancellationToken cancellationToken = default) {
			throw new InvalidOperationException("No DELETE expected.");
		}
	}
	#endregion
}
