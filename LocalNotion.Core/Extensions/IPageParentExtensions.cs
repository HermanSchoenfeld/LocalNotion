using Notion.Client;

namespace LocalNotion.Core;

public static class IPageParentExtensions {

	public static string GetParentId(this IPageParent pageParent) 
		=> pageParent switch {
			DatabaseParent dp => dp.DatabaseId,
			PageParent pp => pp.PageId,
			BlockParent bp => bp.BlockId,
			WorkspaceParent pp => Constants.WorkspaceId,
			_ => throw new NotSupportedException($"{pageParent?.GetType().Name ?? "NULL"}")
		};

}
