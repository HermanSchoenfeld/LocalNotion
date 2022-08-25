using Hydrogen;
using Notion.Client;

namespace LocalNotion.Core;

public static class ISearchClientExtensions {

	public static async Task<IEnumerable<Database>> GetAllDatabases(this ISearchClient searchClient)
		=> (await searchClient.SearchAllAsync(
			new SearchParameters { Filter = new SearchFilter { Value = SearchObjectType.Database } }
		)).Cast<Database>();

	public static async Task<IObject[]> SearchAllAsync(this ISearchClient searchClient, SearchParameters parameters) {
		Guard.ArgumentNotNull(parameters, nameof(parameters));
		var results = new List<IObject>();
		PaginatedList<IObject> searchResult;
		var cursor = parameters.StartCursor;
		do {
			parameters.StartCursor = cursor;
			searchResult = await searchClient.SearchAsync(parameters);
			results.AddRange(searchResult.Results);
			cursor = searchResult.NextCursor;
		} while (searchResult.HasMore);
		return results.ToArray();
	}
}
