using Notion.Client;
using Sphere10.Framework;

namespace LocalNotion.Core;

public static class IPageCoverExtensions {

	public static void SetUrl(this IPageCover pageCover, string url) {
		switch(pageCover) {
			case FilePageCover filePageCover:
				filePageCover.File.Url = url;
				break;
			case ExternalPageCover externalPageCover:
				externalPageCover.External.Url = url;
				break;
			default:
				 throw new NotSupportedException($"Unable to set url for page cover type {pageCover.GetType().ToStringCS()}");
		};
	}

	public static string GetUrl(this IPageCover pageCover) 
		=> pageCover switch {
			FilePageCover filePageCover => filePageCover.File.Url,
			ExternalPageCover externalPageCover => externalPageCover.External.Url,
			_ => throw new NotSupportedException($"Unable to get url for page cover type {pageCover.GetType().ToStringCS()}")
		};

}
