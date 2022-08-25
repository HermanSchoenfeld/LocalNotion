using Notion.Client;


namespace LocalNotion.Core;

public static class IBlocksClientExtensions {

	public static async Task<IBlock[]> GetPageBlocks(this IBlocksClient blocksClient, string pageId) {
		var blocks = await blocksClient.RetrieveAllChildrenAsync(pageId);
		return blocks;
	}

	public static async Task<IBlock[]> RetrieveAllChildrenAsync(this IBlocksClient blocksClient, string blockId) {
		var results = new List<IBlock>();
		PaginatedList<IBlock> searchResult;
		var parameters = new BlocksRetrieveChildrenParameters();
		var cursor = parameters.StartCursor;
		do {
			parameters.StartCursor = cursor;
			searchResult = await blocksClient.RetrieveChildrenAsync(blockId, parameters);
			results.AddRange(searchResult.Results);
			cursor = searchResult.NextCursor;
		} while (searchResult.HasMore);
		return results.ToArray();
	}

	public static async Task<NotionObjectGraph> GetObjectGraph(this IBlocksClient blocksClient, string objectId, IDictionary<string, IObject> objects) {
		var root = new NotionObjectGraph {
			ObjectID =	objectId,
			Children = Array.Empty<NotionObjectGraph>()
		};
		
		var rootObject = objects.TryGetValue(objectId, out var x) ? x : await blocksClient.RetrieveAsync(objectId);
		objects[objectId] = rootObject;

		if (rootObject is Page or IBlock { HasChildren: true })  // note will not populate child page blocks
			await PopulateChildren(root);

		async Task PopulateChildren(NotionObjectGraph parent) {
			var children = new List<NotionObjectGraph>();
			foreach (var child in await blocksClient.RetrieveAllChildrenAsync(parent.ObjectID)) {
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

