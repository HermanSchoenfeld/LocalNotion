using Hydrogen;
using Notion.Client;


namespace LocalNotion.Core;

public static class IBlocksClientExtensions {

	public static async IAsyncEnumerable<IBlock> EnumerateChildrenAsync(this IBlocksClient blocksClient, string blockId, CancellationToken cancellationToken) {
		PaginatedList<IBlock> searchResult;
		var parameters = new BlocksRetrieveChildrenParameters();
		var cursor = parameters.StartCursor;
		do {
			parameters.StartCursor = cursor;
			searchResult = await blocksClient.RetrieveChildrenAsync(blockId, parameters).WithCancellationToken(cancellationToken);
			if (searchResult == null)
				break;
			foreach(var result in searchResult.Results)
				yield return result;
			cursor = searchResult.NextCursor;
		} while (searchResult.HasMore);
	}

	public static async Task<NotionObjectGraph> GetObjectGraphAsync(this IBlocksClient blocksClient, string objectID, IDictionary<string, IObject> objects, CancellationToken cancellationToken = default) {
		var root = new NotionObjectGraph {
			ObjectID =	objectID,
			Children = Array.Empty<NotionObjectGraph>()
		};
		
		var rootObject = objects.TryGetValue(objectID, out var x) ? x : await blocksClient.RetrieveAsync(objectID);
		objects[objectID] = rootObject;

		if (rootObject is Page or IBlock { HasChildren: true })  // note will not populate child page blocks
			await PopulateChildren(root);

		async Task PopulateChildren(NotionObjectGraph parent) {
			cancellationToken.ThrowIfCancellationRequested();
			var children = new List<NotionObjectGraph>();
			await foreach (var child in blocksClient.EnumerateChildrenAsync(parent.ObjectID, cancellationToken)) {
				var graphChild = new NotionObjectGraph { ObjectID = child.Id };
				objects[child.Id] = child;
				if (child.HasChildren && child is not ChildPageBlock)
					await PopulateChildren(graphChild);
				children.Add(graphChild);
			}
			parent.Children = children.ToArray();
		}
		return root;
	}
}