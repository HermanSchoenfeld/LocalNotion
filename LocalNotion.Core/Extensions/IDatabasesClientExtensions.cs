// Copyright (c) Herman Schoenfeld 2018 - Present. All rights reserved. (https://sphere10.com/products/localnotion)
// Author: Herman Schoenfeld <herman@sphere10.com>
//
// Distributed under the GPLv3 software license, see the accompanying file LICENSE 
// or visit https://github.com/HermanSchoenfeld/localnotion/blob/master/LICENSE
//
// This notice must not be removed when duplicating this file or its contents, in whole or in part.

using System.Runtime.CompilerServices;
using Sphere10.Framework;
using Notion.Client;

namespace LocalNotion.Core;

public static class IDataSourcesClientExtensions {

	public static Task<DataSource> RetrieveAsync(this IDataSourcesClient client, string datasourceID, CancellationToken cancellationToken = default)
		=> client.RetrieveAsync(new RetrieveDataSourceRequest { DataSourceId = datasourceID }, cancellationToken);

	public static Task<Page[]> GetAllDatabaseRows(this IDataSourcesClient dataSourcesClient, string dataSourceId, QueryDataSourceRequest parameters = null, CancellationToken cancellationToken = default)
		=> dataSourcesClient.EnumerateAsync(dataSourceId, parameters, cancellationToken).ToArrayAsync(cancellationToken).AsTask();

	public static async IAsyncEnumerable<Page> EnumerateAsync(this IDataSourcesClient dataSourcesClient, string dataSourceId, QueryDataSourceRequest parameters = null, [EnumeratorCancellation] CancellationToken cancellationToken = default) {
		QueryDataSourceResponse searchResult = null;
		parameters ??= new QueryDataSourceRequest();
		//parameters.Sorts = [ new Sort { Property = "Created time", Direction = Direction.Ascending }];
		parameters.DataSourceId = dataSourceId;
		var cursor = parameters.StartCursor;
		do {
			cancellationToken.ThrowIfCancellationRequested();
			parameters.StartCursor = cursor;
			searchResult = await dataSourcesClient.QueryAsync(parameters, cancellationToken);
			foreach (var result in searchResult.Results)
				yield return (Page)result;    // WARN: this cast is an assumption that database items are only pages
			cursor = searchResult.NextCursor;
		} while (searchResult.HasMore);
	}
}