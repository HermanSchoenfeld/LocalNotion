using Notion.Client;

namespace LocalNotion.Core;

public static class IParentExtensions {

	public static string GetId(this IParent pageParent) 
		=> pageParent switch {
			DatabaseParent dp => dp.DatabaseId,
			PageParent pp => pp.PageId,
			BlockParent bp => bp.BlockId,
			WorkspaceParent pp => Constants.WorkspaceId,
			_ => throw new NotSupportedException($"{pageParent?.GetType().Name ?? "NULL"}")
		};

}
