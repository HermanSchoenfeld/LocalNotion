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
