using System.Runtime.CompilerServices;
using ExtensionProperties;
using Hydrogen;
using Notion.Client;
namespace LocalNotion.Core;

public static class IPagesClientExtensions  {

	public static async Task<PagePropertyItems> RetrievePagePropertiesAsync(this IPagesClient pagesClient, Page page, CancellationToken cancellationToken = default) 
		=> new (
			page,
			await pagesClient.EnumeratePagePropertiesAsync(page.Id, page.Properties.Values.Select(x => x.Id), cancellationToken).ToArrayAsync()
		);

	public static async Task<PagePropertyItems> ParallelRetrievePagePropertiesAsync(this IPagesClient pagesClient, Page page, IEnumerable<string> propertyIds, CancellationToken cancellationToken = default) {
		var results = new SynchronizedDictionary<string,IPropertyItemObject>();
		await Parallel.ForEachAsync(propertyIds, cancellationToken, async (id, ct) => {
			var prop = await pagesClient.RetrievePagePropertyItemCompleteAsync(page.Id, id, ct);
			results.Add(prop);
		});
		var kvps = propertyIds.Select(id => new KeyValuePair<string, IPropertyItemObject>(id, results[id])); // return properties in same order as parameters (since they may be fetched in different order)
		return new(page, kvps);
	}

	public static async IAsyncEnumerable<KeyValuePair<string, IPropertyItemObject>> EnumeratePagePropertiesAsync(this IPagesClient pagesClient, string pageID, IEnumerable<string> propertyIds, [EnumeratorCancellation] CancellationToken cancellationToken = default) {
		foreach(var propertyId in propertyIds) 
			yield return await pagesClient.RetrievePagePropertyItemCompleteAsync(pageID, propertyId, cancellationToken);
	}

	public static async Task<KeyValuePair<string, IPropertyItemObject>> RetrievePagePropertyItemCompleteAsync(this IPagesClient pagesClient, string pageID, string propertyId, CancellationToken cancellationToken = default) {
		var parameters = new RetrievePropertyItemParameters();
		parameters.PageId = pageID;
		parameters.PropertyId = propertyId;
		var searchResult = await pagesClient.RetrievePagePropertyItem(parameters).WithCancellationToken(cancellationToken);

		if (searchResult is ListPropertyItem lpi) {
			var itemsList = new List<SimplePropertyItem>();
			itemsList.AddRange(lpi.Results);
			
			while (lpi.HasMore) {
				parameters.StartCursor = lpi.NextCursor;
				var nextPage = (ListPropertyItem) await pagesClient.RetrievePagePropertyItem(parameters).WithCancellationToken(cancellationToken) ;
				itemsList.AddRange(nextPage.Results);
			}
			lpi.Results = itemsList; // this aggregates all items into the list of first itemlist
		}

		return new KeyValuePair<string, IPropertyItemObject>(propertyId, searchResult);
	}
}