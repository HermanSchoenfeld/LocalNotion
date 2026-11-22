// Copyright (c) Herman Schoenfeld 2018 - Present. All rights reserved. (https://sphere10.com/products/localnotion)
// Author: Herman Schoenfeld <herman@sphere10.com>
//
// Distributed under the GPLv3 software license, see the accompanying file LICENSE
// or visit https://github.com/HermanSchoenfeld/localnotion/blob/master/LICENSE
//
// This notice must not be removed when duplicating this file or its contents, in whole or in part.

using System;
using System.Linq;

namespace LocalNotion.Core;

/// <summary>Plans bounded article listings by explicit sequence, newest creation time and a stable slug tie-break.</summary>
internal sealed class CmsArticleListing {

	public const int PageSize = 20;

	public CmsArticleListing(CMSLocalNotionRepository repository, CMSItem item) {
		ArgumentNullException.ThrowIfNull(repository);
		ArgumentNullException.ThrowIfNull(item);
		if (item.ItemType != CMSItemType.CategoryPage)
			throw new ArgumentException("Only category pages have article listings.", nameof(item));
		Articles = item.Parts.Select(repository.GetPage)
			.Where(page => !CMSHelper.IsFramingContent(page.CMSProperties.PageType))
			.GroupBy(page => Tools.Url.StripAnchorTag(page.CMSProperties.CustomSlug))
			.Select(group => repository.CMSDatabase.GetContent(group.Key))
			.OrderBy(node => node.Content.Select(page => page.CMSProperties.Sequence ?? int.MaxValue).DefaultIfEmpty(int.MaxValue).Min())
			// Editing artwork or metadata must not reshuffle articles whose sequence and creation time match.
			.ThenByDescending(node => node.CreatedOn).ThenBy(node => node.Slug, StringComparer.Ordinal).ToArray();
	}

	public CMSContentNode[] Articles { get; }

	public int PageCount => Math.Max(1, (Articles.Length + PageSize - 1) / PageSize);

	public CMSContentNode[] GetPage(int pageNumber) {
		if (pageNumber < 1 || pageNumber > PageCount)
			throw new ArgumentOutOfRangeException(nameof(pageNumber), pageNumber, $"Article page must be between 1 and {PageCount}.");
		return Articles.Skip((pageNumber - 1) * PageSize).Take(PageSize).ToArray();
	}
}
