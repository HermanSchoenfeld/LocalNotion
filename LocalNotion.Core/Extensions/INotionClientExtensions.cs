using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Hydrogen;
using Notion.Client;

namespace LocalNotion.Core;
public static class INotionClientExtensions {

	public static async Task<(LocalNotionResourceType?, DateTime?)> QualifyObjectAsync(this INotionClient client, string objectID, CancellationToken cancellationToken = default) {
		if (!LocalNotionHelper.TryCovertObjectIdToGuid(objectID, out _))
			return (default, default);

		var block = await client.Blocks.RetrieveAsync(objectID).WithCancellationToken(cancellationToken);
		return block.Type switch {
			BlockType.ChildDatabase => (LocalNotionResourceType.Database, block.LastEditedTime),
			BlockType.ChildPage => (LocalNotionResourceType.Page, block.LastEditedTime),
			_ => (default, default(DateTime?))
		};
	}

}

