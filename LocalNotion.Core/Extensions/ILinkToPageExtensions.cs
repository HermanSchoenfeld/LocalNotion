using Notion.Client;
using Sphere10.Framework;

namespace LocalNotion.Core;

public static class ILinkToPageExtensions {
	public static string GetId(this ILinkToPage linkToPage) => linkToPage switch {
		LinkPageToPage page => page.PageId,
		LinkDatabaseToPage database => database.DatabaseId,
		LinkCommentToPage comment => comment.CommentId,
		_ => throw new NotSupportedException($"Unable to get id for link to page type {linkToPage.GetType().ToStringCS()}")
	};
}