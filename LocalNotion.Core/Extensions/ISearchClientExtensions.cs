using Hydrogen;
using Notion.Client;
using System.Runtime.CompilerServices;

namespace LocalNotion.Core;

public static class ISearchClientExtensions {

	public static IAsyncEnumerable<Page> EnumeratePagesAsync(this ISearchClient searchClient)
		=> searchClient
		   .EnumerateAsync(new SearchRequest { Filter = new SearchFilter { Value = SearchObjectType.Page } })
		   .Cast<Page>();

	public static IAsyncEnumerable<Database> EnumerateDatabasesAsync(this ISearchClient searchClient)
		=> searchClient
		    .EnumerateAsync(new SearchRequest { Filter = new SearchFilter { Value = SearchObjectType.Database } })
		    .Cast<Database>();

	public static async IAsyncEnumerable<IObject> EnumerateAsync(this ISearchClient searchClient, SearchRequest request = null, [EnumeratorCancellation] CancellationToken cancellationToken = default) {
		request ??= new SearchRequest();
		PaginatedList<IObject> searchResult;
		var cursor = request.StartCursor;
		do {
			cancellationToken.ThrowIfCancellationRequested();
			request.StartCursor = cursor;
			searchResult = await searchClient.SearchAsync(request, cancellationToken);
			foreach(var result in searchResult.Results)
				yield return result;
			cursor = searchResult.NextCursor;
		} while (searchResult.HasMore);
	}
}
