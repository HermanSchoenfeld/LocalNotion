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

public static class ILinkToPageExtensions {
	public static string GetId(this ILinkToPage linkToPage) => linkToPage switch {
		LinkPageToPage page => page.PageId,
		LinkDatabaseToPage database => database.DatabaseId,
		LinkCommentToPage comment => comment.CommentId,
		_ => throw new NotSupportedException($"Unable to get id for link to page type {linkToPage.GetType().ToStringCS()}")
	};
}