using Notion.Client;

namespace LocalNotion.Core;

public static class IDatabasesClientExtensions {

	public static Task<Page[]> GetAllDatabaseRows(this IDatabasesClient databasesClient, string databaseId, DatabasesQueryParameters parameters = null)
		=> databasesClient.QueryAllAsync(databaseId, parameters);

	public static async Task<Page[]> QueryAllAsync(this IDatabasesClient databasesClient, string databaseId, DatabasesQueryParameters parameters = null) {
		var results = new List<Page>();
		PaginatedList<Page> searchResult;
		parameters ??= new DatabasesQueryParameters();
		var cursor = parameters.StartCursor;
		do {
			parameters.StartCursor = cursor;
			searchResult = await databasesClient.QueryAsync(databaseId, parameters);
			results.AddRange(searchResult.Results);
			cursor = searchResult.NextCursor;
		} while (searchResult.HasMore);
		return results.ToArray();
	}
}