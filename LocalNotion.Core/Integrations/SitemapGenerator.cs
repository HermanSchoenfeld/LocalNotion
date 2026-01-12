// Copyright (c) Herman Schoenfeld 2018 - Present. All rights reserved. (https://sphere10.com/products/localnotion)
// Author: Herman Schoenfeld <herman@sphere10.com>
//
// Distributed under the GPLv3 software license, see the accompanying file LICENSE 
// or visit https://github.com/HermanSchoenfeld/localnotion/blob/master/LICENSE
//
// This notice must not be removed when duplicating this file or its contents, in whole or in part.

using Sphere10.Framework;

namespace LocalNotion.Core;

public class SitemapGenerator {
	
	public SitemapXml Generate(CMSLocalNotionRepository repo) {
		var database = repo.GetDatabase(repo.CMSDatabaseID);
		
		
		var rootUrl = database.Name.ToLowerInvariant().TrimEnd('/');
		if (!rootUrl.StartsWith("http://") && !rootUrl.StartsWith("https://")) {
			rootUrl = "https://" + rootUrl;
		}

		var siteMapXml = new SitemapXml();
		foreach(var cmsItem in repo.CMSItems) {
			var absUrl = !cmsItem.Slug.IsNullOrWhiteSpace() ? "/" + LocalNotionHelper.SanitizeSlug(cmsItem.Slug) : cmsItem.Slug;
			DateTime? lastModified = cmsItem.Parts.Any() ? cmsItem.Parts.Max(x => repo.GetPage(x).LastEditedOn) : null;
			siteMapXml.Add(absUrl, lastModified);
		}
		
		return siteMapXml;
	}

}
