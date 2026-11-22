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
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json.Serialization;
using Notion.Client;

namespace LocalNotion.Core.Tests;

public sealed class RecordingNotionTransport : IRestClient {
	private readonly List<JObject> _pageBodies = new List<JObject>();
	private JObject _schema;
	private bool _hasSetupPage;
	private const string DataSourceID = "2c488c62-d537-4b8f-898d-d6484f9476b9";
	private const string SetupPageID = "4bbaf28f-2701-43f7-ab70-49182f987cc6";

	public int CreateCount { get; private set; }
	public int PageCreateCount { get; private set; }
	public int PageUpdateCount { get; private set; }
	public int ArchiveCount { get; private set; }
	public int SchemaReadCount { get; private set; }
	public int QueryCount { get; private set; }
	public string Endpoint { get; private set; }
	public JObject Body { get; private set; }
	public JObject[] PageBodies => _pageBodies.ToArray();
	public JObject SavedSchema => _schema;
	public CancellationToken CancellationToken { get; private set; }
	public CancellationToken CleanupToken { get; private set; }
	public Exception Error { get; set; }
	public Exception PageCreateError { get; set; }
	public Exception PageUpdateError { get; set; }
	public Exception ArchiveError { get; set; }
	public Exception SchemaReadError { get; set; }
	public bool LeaveActiveRow { get; set; }
	public Action<JObject> CorruptSchema { get; set; }
	public Database Response { get; set; } = new Database {
		Id = "c2c3fd36-bc08-4355-a0db-fb746b88d4a1",
		DataSources = new[] { new DataSourceReferenceResponse { DataSourceId = DataSourceID } }
	};
	public Func<CancellationToken, Task<Database>> PendingResponse { get; set; }
	public Func<CancellationToken, Task<Page>> PendingPageUpdate { get; set; }
	public Func<CancellationToken, Task<Page>> PendingArchive { get; set; }
	public bool SetupPageArchived { get; private set; }

	public INotionClient CreateClient()
		=> new NotionClient(this, null, new DatabasesClient(this), new PagesClient(this), null, null, null, null, null, new DataSourcesClient(this), null, null);

	public async Task<T> PostAsync<T>(
		string uri, object body, IEnumerable<KeyValuePair<string, string>> queryParams = null,
		IDictionary<string, string> headers = null, JsonSerializerSettings serializerSettings = null,
		IBasicAuthenticationParameters basicAuthenticationParameters = null, CancellationToken cancellationToken = default
	) {
		// Match the real RestClient serialization; interface-only annotations must not hide wire-format defects.
		return (T)await HandleAsync("POST", uri, Serialize(body), cancellationToken);
	}

	public async Task<T> GetAsync<T>(string uri, IDictionary<string, string> queryParams = null, IDictionary<string, string> headers = null,
		JsonSerializerSettings serializerSettings = null, CancellationToken cancellationToken = default)
		=> (T)await HandleAsync("GET", uri, null, cancellationToken);

	public Task<T> PostAsync<T>(string uri, ISendFileUploadFormDataParameters formData, IEnumerable<KeyValuePair<string, string>> queryParams = null,
		IDictionary<string, string> headers = null, JsonSerializerSettings serializerSettings = null,
		IBasicAuthenticationParameters basicAuthenticationParameters = null, CancellationToken cancellationToken = default)
		=> throw new InvalidOperationException("CMS creation must not upload files.");

	public async Task<T> PatchAsync<T>(string uri, object body, IDictionary<string, string> queryParams = null,
		IDictionary<string, string> headers = null, JsonSerializerSettings serializerSettings = null, CancellationToken cancellationToken = default)
		=> (T)await HandleAsync("PATCH", uri, Serialize(body), cancellationToken);

	public Task<T> DeleteAsync<T>(string uri, IDictionary<string, string> queryParams = null, IDictionary<string, string> headers = null, JsonSerializerSettings serializerSettings = null, CancellationToken cancellationToken = default) => throw new InvalidOperationException("No DELETE expected.");

	public Task DeleteAsync(string uri, IDictionary<string, string> queryParams = null, IDictionary<string, string> headers = null,
		CancellationToken cancellationToken = default)
		=> throw new InvalidOperationException("CMS creation must not delete content.");

	public async Task<object> HandleAsync(string method, string uri, JObject body, CancellationToken cancellationToken) {
		cancellationToken.ThrowIfCancellationRequested();
		if (method == "POST" && uri == "/v1/databases") {
			CreateCount++;
			Endpoint = uri;
			Body = body;
			CancellationToken = cancellationToken;
			if (Error != null)
				throw Error;
			if (body.SelectTokens("$..options").OfType<JArray>().Any(x => x.Count > 100))
				throw new NotionApiException(HttpStatusCode.BadRequest, null, "Schema option arrays must contain at most 100 entries.");
			_schema = (JObject)body["initial_data_source"]["properties"].DeepClone();
			if (PendingResponse != null)
				return await PendingResponse(cancellationToken);
			return Response;
		}
		if (method == "POST" && uri == "/v1/pages") {
			PageCreateCount++;
			_pageBodies.Add(body);
			if (PageCreateError != null)
				throw PageCreateError;
			if ((string)body["properties"]["Type"]["select"]["name"] != "Page" || (string)body["properties"]["Status"]["select"]["name"] != "Hidden")
				throw new InvalidOperationException("The temporary row must be an unpublished Page with Hidden status.");
			_hasSetupPage = true;
			AppendOptions((JObject)body["properties"]);
			return new Page { Id = SetupPageID };
		}
		if (method == "PATCH" && uri == "/v1/pages/" + SetupPageID) {
			_pageBodies.Add(body);
			if ((bool?)body["in_trash"] == true) {
				ArchiveCount++;
				CleanupToken = cancellationToken;
				if (ArchiveError != null)
					throw ArchiveError;
				var archived = PendingArchive != null
					? await PendingArchive(cancellationToken)
					: new Page { Id = SetupPageID, InTrash = true };
				SetupPageArchived = archived?.InTrash == true;
				return archived;
			}
			PageUpdateCount++;
			if (PageUpdateError != null)
				throw PageUpdateError;
			if (PendingPageUpdate != null)
				return await PendingPageUpdate(cancellationToken);
			AppendOptions((JObject)body["properties"]);
			return new Page { Id = SetupPageID };
		}
		if (method == "GET" && uri == "/v1/data_sources/" + DataSourceID) {
			SchemaReadCount++;
			if (SchemaReadError != null)
				throw SchemaReadError;
			CorruptSchema?.Invoke(_schema);
			return JsonConvert.DeserializeObject<DataSource>(new JObject { ["id"] = DataSourceID, ["properties"] = _schema }.ToString());
		}
		if (method == "POST" && uri == "/v1/data_sources/" + DataSourceID + "/query") {
			QueryCount++;
			return new QueryDataSourceResponse {
				Results = _hasSetupPage && (!SetupPageArchived || LeaveActiveRow)
					? new List<IQueryDataSourceResponseObject> { new Page { Id = SetupPageID } }
					: new List<IQueryDataSourceResponseObject>(),
				HasMore = false
			};
		}
		throw new InvalidOperationException("Unexpected CMS request: " + method + " " + uri);
	}

	private void AppendOptions(JObject properties) {
		foreach (var property in properties.Properties()) {
			if (property.Value["multi_select"] is not JArray options)
				continue;
			if (options.Count > 100)
				throw new NotionApiException(HttpStatusCode.BadRequest, null, "Page selections must contain at most 100 entries.");
			var saved = (JArray)_schema[property.Name]["multi_select"]["options"];
			foreach (var option in options)
				if (!saved.Any(x => (string)x["name"] == (string)option["name"]))
					saved.Add(new JObject { ["name"] = option["name"].DeepClone(), ["color"] = option["color"].DeepClone() });
		}
	}

	private static JObject Serialize(object value)
		=> JObject.Parse(JsonConvert.SerializeObject(value, new JsonSerializerSettings {
			NullValueHandling = NullValueHandling.Ignore,
			ContractResolver = new DefaultContractResolver { NamingStrategy = new CamelCaseNamingStrategy() }
		}));
}
