using System.Runtime.CompilerServices;
using Hydrogen;
using Notion.Client;

namespace LocalNotion.Core;

public static class IDatabasesClientExtensions {

	public static Task<Page[]> GetAllDatabaseRows(this IDatabasesClient databasesClient, string databaseId, DatabasesQueryParameters parameters = null, CancellationToken cancellationToken = default)
		=> databasesClient.EnumerateAsync(databaseId, parameters, cancellationToken).ToArrayAsync();

	public static async IAsyncEnumerable<Page> EnumerateAsync(this IDatabasesClient databasesClient, string databaseId, DatabasesQueryParameters parameters = null, [EnumeratorCancellation] CancellationToken cancellationToken = default) {
		DatabaseQueryResponse searchResult;
		parameters ??= new DatabasesQueryParameters();
		var cursor = parameters.StartCursor;
		do {
			cancellationToken.ThrowIfCancellationRequested();
			parameters.StartCursor = cursor;
			searchResult = await databasesClient.QueryAsync(databaseId, parameters, cancellationToken);
			foreach(var result in searchResult.Results)
				yield return (Page)result;    // WARN: this cast is an assumption that database items are only pages
			cursor = searchResult.NextCursor;
		} while (searchResult.HasMore);
	
	}
}