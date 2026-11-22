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
		var baseUrl = repo.Paths.GetRemoteHostedBaseUrl();
		if (!Uri.TryCreate(baseUrl, UriKind.Absolute, out var rootUrl) || (rootUrl.Scheme != Uri.UriSchemeHttp && rootUrl.Scheme != Uri.UriSchemeHttps)) {
			// Relative website links still require an absolute origin in the sitemap. The CMS
			// title retains the domain; its filesystem name may have replaced dots with hyphens.
			var domain = database.Title.Trim();
			if (!domain.StartsWith("http://", StringComparison.OrdinalIgnoreCase) && !domain.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
				domain = "https://" + domain;
			rootUrl = new Uri(domain.TrimEnd('/') + "/" + baseUrl.Trim('/'));
		}
		rootUrl = new Uri(rootUrl.AbsoluteUri.TrimEnd('/') + "/");

		var siteMapXml = new SitemapXml();
		foreach(var cmsItem in repo.CMSItems) {
			// Never generate sitemap entries for private content (self or ancestor tagged @Private)
			if (repo.CMSDatabase.TryGetContent(cmsItem.Slug, out var contentNode) && contentNode.IsPrivate)
				continue;
			var absUrl = new Uri(rootUrl, LocalNotionHelper.SanitizeSlug(cmsItem.Slug ?? string.Empty)).AbsoluteUri;
			DateTime? lastModified = cmsItem.Parts.Any() ? cmsItem.Parts.Max(x => repo.GetPage(x).LastEditedOn) : null;
			siteMapXml.Add(absUrl, lastModified);
		}
		
		return siteMapXml;
	}

}
