// Copyright (c) Herman Schoenfeld 2018 - Present. All rights reserved. (https://sphere10.com/products/localnotion)
// Author: Herman Schoenfeld <herman@sphere10.com>
//
// Distributed under the GPLv3 software license, see the accompanying file LICENSE 
// or visit https://github.com/HermanSchoenfeld/localnotion/blob/master/LICENSE
//
// This notice must not be removed when duplicating this file or its contents, in whole or in part.

using Notion.Client;
using Sphere10.Framework;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;

namespace LocalNotion.Core;

public static class INotionClientExtensions {
	public static async Task<(LocalNotionResourceType?, DateTime?)> QualifyObjectAsync(this INotionClient client, string objectID, CancellationToken cancellationToken = default) {
		if (!LocalNotionHelper.TryCovertObjectIdToGuid(objectID, out _))
			return (default, default);

		// Primary: Use GET /v1/blocks/{id} — Notion's universal retrieve-by-ID endpoint.
		// Pages return as ChildPageBlock, databases as ChildDatabaseBlock.
		try {
			var block = await client.Blocks.RetrieveAsync(objectID, cancellationToken);
			switch (block?.Type) {
				case BlockType.ChildPage:
					return (LocalNotionResourceType.Page, block.LastEditedTime);
				case BlockType.ChildDatabase:
					return (LocalNotionResourceType.Database, block.LastEditedTime);
			}
		} catch (NotionApiException) {
			// Not a block — fall through to other object types
		}

		// Fallback: Try as a DataSource
		try {
			var dataSource = await client.DataSources.RetrieveAsync(objectID, cancellationToken);
			if (dataSource != null)
				return (LocalNotionResourceType.Database, dataSource.LastEditedTime);
		} catch (NotionApiException) {
			// Not a datasource — fall through
		}

		return (default, default(DateTime?));
	}

	public static async IAsyncEnumerable<IObject> EnumerateAllWorkspaceObjectsAsync(this INotionClient client, SearchRequest request = null, [EnumeratorCancellation] CancellationToken cancellationToken = default) {
		var seenDatabases = new HashSet<string>();

		// Step 1: Enumerate all Pages and DataSources from Search
		await foreach (var obj in client.Search.EnumerateAsync(request, cancellationToken)) {  // Use 'request' parameter

			// Step 2: If it's a DataSource, fetch its parent Database (if not already seen)
			if (obj is DataSource dataSource) {
				var databaseId = dataSource.Parent switch {
					DatabaseParent dbParent => dbParent.DatabaseId,
					DatasourceParent dsParent => dsParent.DatabaseId,  // Nested DataSources also have DatabaseId
					_ => null
				};

				if (databaseId != null && seenDatabases.Add(databaseId)) {
					// Fetch the Database header
					var database = await client.Databases.RetrieveAsync(databaseId, cancellationToken);
					yield return database;
				}

				yield return dataSource;
			} else {
				yield return obj; // Page or other object
			}
		}
	}
}

