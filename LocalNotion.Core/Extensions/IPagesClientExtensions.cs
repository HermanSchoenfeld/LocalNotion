using System.Runtime.CompilerServices;
using ExtensionProperties;
using Hydrogen;
using Notion.Client;
namespace LocalNotion.Core;

public static class IPagesClientExtensions  {


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

	public static async Task<IPropertyItemObject[]> RetrievePagePropertiesAsync(this IPagesClient pagesClient, Page page, CancellationToken cancellationToken = default) 
		=> await pagesClient.EnumeratePagePropertiesAsync(page.Id, page.Properties.Values.Select(x => x.Id), cancellationToken).ToArrayAsync();

	public static async IAsyncEnumerable<IPropertyItemObject> EnumeratePagePropertiesAsync(this IPagesClient pagesClient, string pageId, IEnumerable<string> propertyIds, [EnumeratorCancellation] CancellationToken cancellationToken = default) {
		foreach(var propertyId in propertyIds) 
			yield return await pagesClient.RetrievePagePropertyItemCompleteAsync(pageId, propertyId, cancellationToken);
	}

	public static async Task<IPropertyItemObject> RetrievePagePropertyItemCompleteAsync(this IPagesClient pagesClient, string pageId, string propertyId, CancellationToken cancellationToken = default) {
		IPropertyItemObject searchResult;
		var listItems = new List<SimplePropertyItem>();
		var parameters = new RetrievePropertyItemParameters();
		parameters.PageId = pageId;
		parameters.PropertyId = propertyId;
		var cursor = parameters.StartCursor;
		do {
			parameters.StartCursor = cursor;
			searchResult = await pagesClient.RetrievePagePropertyItem(parameters).WithCancellationToken(cancellationToken);
			if (searchResult is ListPropertyItem listPropertyItem) {
				listItems.AddRange(listPropertyItem.Results);
				cursor = listPropertyItem.NextCursor;
			}
			
		} while (searchResult is SimplePropertyItem || ((ListPropertyItem)searchResult).HasMore);
		if (searchResult is ListPropertyItem lpi) 
			lpi.Results = listItems;
		return searchResult;
	}


}

