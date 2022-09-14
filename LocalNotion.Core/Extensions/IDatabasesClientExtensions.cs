using Hydrogen;
using Notion.Client;

namespace LocalNotion.Core;

public static class IDatabasesClientExtensions {

	public static Task<Page[]> GetAllDatabaseRows(this IDatabasesClient databasesClient, string databaseId, DatabasesQueryParameters parameters = null)
		=> databasesClient.EnumerateAsync(databaseId, parameters).ToArrayAsync();

	public static async IAsyncEnumerable<Page> EnumerateAsync(this IDatabasesClient databasesClient, string databaseId, DatabasesQueryParameters parameters = null) {
		PaginatedList<Page> searchResult;
		parameters ??= new DatabasesQueryParameters();
		var cursor = parameters.StartCursor;
		do {
			parameters.StartCursor = cursor;
			searchResult = await databasesClient.QueryAsync(databaseId, parameters);
			foreach(var result in searchResult.Results)
				yield return result;
			cursor = searchResult.NextCursor;
		} while (searchResult.HasMore);
	
	}
}