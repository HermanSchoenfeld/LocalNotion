// Copyright (c) Herman Schoenfeld 2018 - Present. All rights reserved. (https://sphere10.com/products/localnotion)
// Author: Herman Schoenfeld <herman@sphere10.com>
//
// Distributed under the GPLv3 software license, see the accompanying file LICENSE 
// or visit https://github.com/HermanSchoenfeld/localnotion/blob/master/LICENSE
//
// This notice must not be removed when duplicating this file or its contents, in whole or in part.

using Notion.Client;
using Sphere10.Framework;

namespace LocalNotion.Core;

public static class IPageIconExtensions {

	public static void SetUrl(this IPageIcon pageIcon, string url) {
		switch(pageIcon) {
			case FilePageIcon filePageIcon:
				filePageIcon.File.Url = url;
				break;
			case ExternalPageIcon externalPageIcon:
				externalPageIcon.External.Url = url;
				break;
			case CustomEmojiPageIcon customEmojiPageIcon:
				customEmojiPageIcon.CustomEmoji.Url = url;
				break;
			case IconPageIcon iconPageIcon:
				(iconPageIcon.Icon ??= new NotionIcon()).Url = url;
				break;
			default:
				 throw new NotSupportedException($"Unable to set url for page icon type {pageIcon.GetType().ToStringCS()}");
		};
	}

	public static string GetUrl(this IPageIcon pageIcon) 
		=> pageIcon switch {
			FilePageIcon filePageIcon => filePageIcon.File.Url,
			ExternalPageIcon externalPageIcon => externalPageIcon.External.Url,
			CustomEmojiPageIcon customEmojiPageIcon => customEmojiPageIcon.CustomEmoji.Url,
			IconPageIcon iconPageIcon => iconPageIcon.GetIconUrl(),
			_ => throw new NotSupportedException($"Unable to get url for page icon type {pageIcon.GetType().ToStringCS()}")
		};

	/// <summary>
	/// Resolves the url of an icon from Notion's built-in icon library. The API identifies these by
	/// name and color only, but serves them from a predictable CDN path, so the url is derived from
	/// those. Once the icon has been downloaded locally, <see cref="SetUrl"/> replaces it.
	/// </summary>
	public static string GetIconUrl(this IconPageIcon pageIcon) {
		if (!string.IsNullOrWhiteSpace(pageIcon.Icon?.Url))
			return pageIcon.Icon.Url;

		if (string.IsNullOrWhiteSpace(pageIcon.Icon?.Name) || string.IsNullOrWhiteSpace(pageIcon.Icon?.Color))
			return null;

		return $"https://www.notion.so/icons/{pageIcon.Icon.Name}_{pageIcon.Icon.Color}.svg";
	}

}