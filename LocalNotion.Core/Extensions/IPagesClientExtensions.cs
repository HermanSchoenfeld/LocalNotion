using System.Runtime.CompilerServices;
using ExtensionProperties;
using Hydrogen;
using Notion.Client;
namespace LocalNotion.Core;

public static class IPagesClientExtensions  {


	//public static async Task<(Page, DateTime)> RetrieveWithLastUpdate(this IPagesClient pagesClient, string pageId) {
	//	var page = await pagesClient.RetrieveAsync(pageId);
	//	var properties = await pagesClient.RetrievePagePropertiesAsync(page);
	//	page.FetchedProperties = properties.ToDictionary(p => p.Id);
	//	return page;
	//}

	/// <summary>
	/// Retrieves a <see cref="Page"/> via <see cref="IPagesClient.RetrieveAsync"/> and fetches all it's properties 
	/// </summary>
	/// <param name="pagesClient"></param>
	/// <param name="pageId"></param>
	/// <returns></returns>
	public static async Task<Page> RetrieveWithPropertiesAsync(this IPagesClient pagesClient, string pageId) {
		var page = await pagesClient.RetrieveAsync(pageId);
		var properties = await pagesClient.RetrievePagePropertiesAsync(page);
		page.FetchedProperties = properties.ToDictionary(p => p.Id);
		return page;
	}


	public static async Task<IPropertyItemObject[]> RetrievePagePropertiesParallelc(this IPagesClient pagesClient, Page page, CancellationToken cancellationToken = default) 
		=> (await pagesClient.FetchPagePropertiesParallelAsync(page.Id, page.Properties.Values.Select(x => x.Id), cancellationToken)).ToArray();


	public static async Task<IPropertyItemObject[]> RetrievePagePropertiesAsync(this IPagesClient pagesClient, Page page, CancellationToken cancellationToken = default) 
		=> await pagesClient.EnumeratePagePropertiesAsync(page.Id, page.Properties.Values.Select(x => x.Id), cancellationToken).ToArrayAsync();

	public static async Task<IEnumerable<IPropertyItemObject>> FetchPagePropertiesParallelAsync(this IPagesClient pagesClient, string pageId, IEnumerable<string> propertyIds, CancellationToken cancellationToken = default) {
		var results = new SynchronizedDictionary<string, IPropertyItemObject>();
		await Parallel.ForEachAsync(propertyIds, cancellationToken, async (id, ct) => {
			var prop = await pagesClient.RetrievePagePropertyItemCompleteAsync(pageId, id, ct);
			results.Add(id, prop);
		});
		return propertyIds.Select(id => results[id]); // return properties in same order as parameters (since they may be fetched in different order)
	}

	public static async IAsyncEnumerable<IPropertyItemObject> EnumeratePagePropertiesAsync(this IPagesClient pagesClient, string pageId, IEnumerable<string> propertyIds, [EnumeratorCancellation] CancellationToken cancellationToken = default) {
		foreach(var propertyId in propertyIds) 
			yield return await pagesClient.RetrievePagePropertyItemCompleteAsync(pageId, propertyId, cancellationToken);
	}

	public static async Task<IPropertyItemObject> RetrievePagePropertyItemCompleteAsync(this IPagesClient pagesClient, string pageId, string propertyId, CancellationToken cancellationToken = default) {
		IPropertyItemObject searchResult;
		
		var parameters = new RetrievePropertyItemParameters();
		parameters.PageId = pageId;
		parameters.PropertyId = propertyId;
		searchResult = await pagesClient.RetrievePagePropertyItem(parameters).WithCancellationToken(cancellationToken);

		if (searchResult is ListPropertyItem lpi) {
			var itemsList = new List<SimplePropertyItem>();
			//itemsList.Add(lpi.PropertyItem);
			itemsList.AddRange(lpi.Results);
			
			while (lpi.HasMore) {
				parameters.StartCursor = lpi.NextCursor;
				var nextPage = (ListPropertyItem) await pagesClient.RetrievePagePropertyItem(parameters).WithCancellationToken(cancellationToken) ;
				//if (nextPage.PropertyItem != null)
				//	itemsList.Add(nextPage.PropertyItem);
				itemsList.AddRange(nextPage.Results);
			}
			lpi.Results = itemsList; // this aggregates all items into the list of first itemlist
		}

		return searchResult;
	}


}

