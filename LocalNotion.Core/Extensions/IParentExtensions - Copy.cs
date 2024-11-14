using Hydrogen;
using Notion.Client;

namespace LocalNotion.Core;

public static class IPageIconExtensions {

	public static void SetUrl(this IPageIcon pageIcon, string url) {
		switch(pageIcon) {
			case FileObject fileObject:
				fileObject.SetUrl(url);
				break;

			case CustomEmojiObject customEmojiObject:
				customEmojiObject.Emoji.Url = url;
				break;
			default:
				 throw new NotSupportedException($"Unable to set url for page icon type {pageIcon.GetType().ToStringCS()}");
		};
	}

	public static string GetUrl(this IPageIcon pageIcon) 
		=> pageIcon switch {
			FileObject fileObject => fileObject.GetUrl(),
			CustomEmojiObject customEmojiObject => customEmojiObject.GetUrl(),
			_ => throw new NotSupportedException($"Unable to get url for page icon type {pageIcon.GetType().ToStringCS()}")
		};

}