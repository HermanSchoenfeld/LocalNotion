using Hydrogen;
using Notion.Client;

namespace LocalNotion.Core;

public static class ISearchClientExtensions {

	public static IAsyncEnumerable<Page> EnumeratePagesAsync(this ISearchClient searchClient)
		=> searchClient
		   .EnumerateAsync(new SearchParameters { Filter = new SearchFilter { Value = SearchObjectType.Page } })
		   .Cast<Page>();

	public static IAsyncEnumerable<Database> EnumerateDatabasesAsync(this ISearchClient searchClient)
		=> searchClient
		    .EnumerateAsync(new SearchParameters { Filter = new SearchFilter { Value = SearchObjectType.Database } })
		    .Cast<Database>();

	public static async IAsyncEnumerable<IObject> EnumerateAsync(this ISearchClient searchClient, SearchParameters parameters = null) {
		parameters ??= new SearchParameters();
		
		PaginatedList<IObject> searchResult;
		var cursor = parameters.StartCursor;
		do {
			parameters.StartCursor = cursor;
			searchResult = await searchClient.SearchAsync(parameters);
			foreach(var result in searchResult.Results)
				yield return result;
			cursor = searchResult.NextCursor;
		} while (searchResult.HasMore);
	}
}
