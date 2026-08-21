// Copyright (c) Herman Schoenfeld 2018 - Present. All rights reserved. (https://sphere10.com/products/localnotion)
// Author: Herman Schoenfeld <herman@sphere10.com>
//
// Distributed under the GPLv3 software license, see the accompanying file LICENSE 
// or visit https://github.com/HermanSchoenfeld/localnotion/blob/master/LICENSE
//
// This notice must not be removed when duplicating this file or its contents, in whole or in part.

using Sphere10.Framework;
using Notion.Client;

namespace LocalNotion.Core;

public static class ObjectIdExtensions {
	/// <summary>
	/// The object a mention points at, or null when it does not reference one. An unrecognized
	/// type degrades to null with a warning rather than throwing -- Notion introduces mention
	/// types without notice, and an unattended pull must not abort over one span of text.
	/// </summary>
	public static string GetObjectID(this Mention mention) {
		switch (mention?.Type) {
			case null:
				return null;
			case "database":
				return mention.Database?.Id;
			case "page":
				return mention.Page?.Id;
			case "user":
				return mention.User?.Id;
			case "custom_emoji":     // no referenced object
			case "date":             // date mentions don't have an Id
			case "link_mention":
			case "link_preview":
			case "template_mention":
				return null;
			default:
				SystemLog.Warning($"Unrecognized mention type '{mention.Type}' - treating as having no object ID");
				return null;
		}
	}
}
