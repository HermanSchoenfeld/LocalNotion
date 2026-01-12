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
	public static string GetObjectID(this Mention mention)
		=> mention.Type switch {
			"database" => mention.Database.Id,
			"date" => null, // Date mentions don't have an Id
			"link_preview" => null,
			"page" => mention.Page.Id,
			"template_mention" => null,
			"user" => mention.User.Id,
			_ => throw new InvalidOperationException($"Unrecognized mention type '{mention.Type}'")
		};
}
