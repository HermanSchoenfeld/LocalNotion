using Sphere10.Framework;
using Notion.Client;
using System.Runtime.CompilerServices;

namespace LocalNotion.Core;

public static class IDataSourceClientExtensions {

	public static IAsyncEnumerable<Page> EnumeratePagesAsync(this ISearchClient searchClient)
		=> searchClient
		   .EnumerateAsync(new SearchRequest { Filter = new SearchFilter { Value = SearchObjectType.Page } })
		   .Cast<Page>();

	public static IAsyncEnumerable<DataSource> EnumerateDataSourcesAsync(this ISearchClient searchClient)
		=> searchClient
		    .EnumerateAsync(new SearchRequest { Filter = new SearchFilter { Value = SearchObjectType.DataSource } })
		    .Cast<DataSource>();

	public static async IAsyncEnumerable<ISearchResponseObject> EnumerateAsync(this ISearchClient searchClient, SearchRequest request = null, [EnumeratorCancellation] CancellationToken cancellationToken = default) {
		request ??= new SearchRequest();
		SearchResponse searchResult;
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
