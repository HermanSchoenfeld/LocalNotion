using Hydrogen;
using Notion.Client;

namespace LocalNotion;

public static class RichTextExtensions {
	public static string ToPlainText(this IEnumerable<RichTextBase> notiontext)
		=> notiontext.Select(x => x.PlainText).ToDelimittedString(" ");

}
