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
using System.Threading;
using System.Threading.Tasks;
using LocalNotion.Core;
using Newtonsoft.Json;
using Notion.Client;
using NUnit.Framework;

namespace LocalNotion.Core.Tests;

[TestFixture]
[Parallelizable(ParallelScope.Children)]
public class WorkspaceSearchCutoffTests {
	private static readonly DateTime Now = new(2026, 11, 25, 10, 0, 0, DateTimeKind.Utc);

	[Test]
	public async Task ASyncPollReadsOnlyObjectsEditedSinceItsCutoffNewestFirst() {
		var transport = new SearchTransport(Page("newest", Now.AddMinutes(5)), Page("recent", Now.AddMinutes(1)), Page("older", Now.AddMinutes(-30)), Page("oldest", Now.AddDays(-3)));

		var found = await transport.CreateClient().EnumerateAllWorkspaceObjectsAsync(new SearchRequest(), Now).Select(item => item.Id).ToArrayAsync();

		Assert.That(found, Is.EqualTo(new[] { "newest", "recent" }), "Enumeration stops at the first object edited before the cutoff.");
		Assert.That(transport.Requests.Single().Sort, Is.Not.Null);
		Assert.That((transport.Requests.Single().Sort.Direction, transport.Requests.Single().Sort.Timestamp), Is.EqualTo((SearchDirection.Descending, "last_edited_time")));
	}

	[Test]
	public async Task WithoutACutoffEveryObjectIsReadInSearchOrder() {
		var transport = new SearchTransport(Page("a", Now.AddDays(-3)), Page("b", Now), Page("c", Now.AddDays(-1)));

		var found = await transport.CreateClient().EnumerateAllWorkspaceObjectsAsync(new SearchRequest()).Select(item => item.Id).ToArrayAsync();

		Assert.That(found, Is.EqualTo(new[] { "a", "b", "c" }));
		Assert.That(transport.Requests.Single().Sort, Is.Null, "A full read keeps Search's own order.");
	}

	private static Page Page(string id, DateTime edited) => new() { Id = id, LastEditedTime = edited };

	#region Inner Types
	private sealed class SearchTransport : Notion.Client.IRestClient {
		private readonly Page[] _pages;
		public SearchTransport(params Page[] pages) => _pages = pages;
		public List<SearchRequest> Requests { get; } = new();
		public INotionClient CreateClient() => new NotionClient(this, null, null, null, new SearchClient(this), null, null, null, null, null, null, null);
		public Task<T> PostAsync<T>(string uri, object body, IEnumerable<KeyValuePair<string, string>> queryParams = null, IDictionary<string, string> headers = null, JsonSerializerSettings serializerSettings = null, IBasicAuthenticationParameters basicAuthenticationParameters = null, CancellationToken cancellationToken = default) {
			Assert.That(uri, Is.EqualTo("/v1/search"));
			Requests.Add((SearchRequest)body);
			return Task.FromResult((T)(object)new SearchResponse { Results = _pages.Cast<ISearchResponseObject>().ToList(), HasMore = false });
		}
		public Task<T> GetAsync<T>(string uri, IDictionary<string, string> queryParams = null, IDictionary<string, string> headers = null, JsonSerializerSettings serializerSettings = null, CancellationToken cancellationToken = default) => throw new InvalidOperationException("No GET expected: " + uri);
		public Task<T> PostAsync<T>(string uri, ISendFileUploadFormDataParameters formData, IEnumerable<KeyValuePair<string, string>> queryParams = null, IDictionary<string, string> headers = null, JsonSerializerSettings serializerSettings = null, IBasicAuthenticationParameters basicAuthenticationParameters = null, CancellationToken cancellationToken = default) => throw new InvalidOperationException("No upload expected.");
		public Task<T> PatchAsync<T>(string uri, object body, IDictionary<string, string> queryParams = null, IDictionary<string, string> headers = null, JsonSerializerSettings serializerSettings = null, CancellationToken cancellationToken = default) => throw new InvalidOperationException("No PATCH expected.");
		public Task<T> DeleteAsync<T>(string uri, IDictionary<string, string> queryParams = null, IDictionary<string, string> headers = null, JsonSerializerSettings serializerSettings = null, CancellationToken cancellationToken = default) => throw new InvalidOperationException("No DELETE expected.");
		public Task DeleteAsync(string uri, IDictionary<string, string> queryParams = null, IDictionary<string, string> headers = null, CancellationToken cancellationToken = default) => throw new InvalidOperationException("No DELETE expected.");
	}
	#endregion
}
