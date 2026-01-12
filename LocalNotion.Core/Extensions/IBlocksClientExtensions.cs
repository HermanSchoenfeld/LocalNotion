// Copyright (c) Herman Schoenfeld 2018 - Present. All rights reserved. (https://sphere10.com/products/localnotion)
// Author: Herman Schoenfeld <herman@sphere10.com>
//
// Distributed under the GPLv3 software license, see the accompanying file LICENSE 
// or visit https://github.com/HermanSchoenfeld/localnotion/blob/master/LICENSE
//
// This notice must not be removed when duplicating this file or its contents, in whole or in part.

using System.Runtime.CompilerServices;
using Sphere10.Framework;
using Notion.Client;


namespace LocalNotion.Core;

public static class IBlocksClientExtensions {

	public static async IAsyncEnumerable<IBlock> EnumerateChildrenAsync(this IBlocksClient blocksClient, string blockId, [EnumeratorCancellation] CancellationToken cancellationToken) {
		PaginatedList<IBlock> searchResult;
		var parameters = new BlockRetrieveChildrenRequest();
		parameters.BlockId = blockId;
		var cursor = parameters.StartCursor;
		do {
			parameters.StartCursor = cursor;
			searchResult = await blocksClient.RetrieveChildrenAsync(parameters, cancellationToken);
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
		
		var rootObject = objects.TryGetValue(objectID, out var x) ? x : await blocksClient.RetrieveAsync(objectID, cancellationToken);
		objects[objectID] = rootObject;

		if (rootObject is Page or IBlock { HasChildren: true })  // note will not populate child page blocks
			await PopulateChildren(root);

		return root;

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
	}
}