using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Hydrogen;
using Microsoft.Win32;
using Notion.Client;

namespace LocalNotion.Core {
	public class LocalNotionCMS : ILocalNotionCMS {
		private readonly ICache<string, CMSArtifact[]> _itemsBySlug;
		private readonly ICache<string, string[]> _pagesByCategorySlug;
		private readonly ICache<string, string[]> _categoriesByCategorySlug;


		public LocalNotionCMS(string cmsDatabaseID, ILocalNotionRepository repository) {
			Repository = repository;
			Repository.Changed += _ => FlushCache();
			CMSDatabaseID = cmsDatabaseID;
			
			_itemsBySlug = new BulkFetchActionCache<string, CMSArtifact[]>( 
				() => Repository
				      .Resources
				      .Where(r =>  r.ParentResourceID == CMSDatabaseID &&  r is LocalNotionEditableResource { CMSProperties: not null } cmsResource && cmsResource.CMSProperties.Status == CMSItemStatus.Published )
				      .Cast<LocalNotionEditableResource>()
				      .OrderBy(r => r.CMSProperties.Sequence)
				      .GroupBy(r => r.CMSProperties.PageType switch {
						  CMSPageType.Gallery => Tools.Url.ToUrlSlug(r.CMSProperties.Root),   // Gallery pages are grouped by their roots (categories are badges)
						  _ => Tools.Url.StripAnchorTag(r.CMSProperties.CustomSlug)
					  })
				      .ToDictionary(
							g => g.Key, 
							g => g.GroupBy(g2 => g2.CMSProperties.PageType)
							      .Select(g2 => new CMSArtifact { Type = g2.Key, Items = g.ToArray(), Slug = g.Key} )
								  .ToArray()
					   ),
				keyComparer: StringComparer.InvariantCultureIgnoreCase
			);
			_itemsBySlug.ContainsCachedItem(string.Empty);

			_pagesByCategorySlug = new BulkFetchActionCache<string, string[]>(
				() => {
					var result = new LookupEx<string, string>(StringComparer.InvariantCultureIgnoreCase);
					foreach (var page in Repository.Resources.Where(r => r is LocalNotionPage { CMSProperties: not null }).Cast<LocalNotionPage>()) {
						// Add entry for parent container 
						var categoryKey = NotionCMSHelper.CreateCategorySlug(
							page.CMSProperties.Root,
							page.CMSProperties.Category1,
							page.CMSProperties.Category2,
							page.CMSProperties.Category3,
							page.CMSProperties.Category4,
							page.CMSProperties.Category5
						);
						result.Add(categoryKey, page.ID);
						result.Add(categoryKey + $"/{Constants.NotionCMSCategoryWildcard}", page.ID);

						// Add wildcard entry for all ancestor container categories
						if (!string.IsNullOrWhiteSpace(page.CMSProperties.Category5)) {
							categoryKey = NotionCMSHelper.CreateCategorySlug(
								page.CMSProperties.Root,
								page.CMSProperties.Category1,
								page.CMSProperties.Category2,
								page.CMSProperties.Category3,
								page.CMSProperties.Category4,
								Constants.NotionCMSCategoryWildcard
							);
							result.Add(categoryKey, page.ID);
						}

						if (!string.IsNullOrWhiteSpace(page.CMSProperties.Category4)) {
							categoryKey = NotionCMSHelper.CreateCategorySlug(
								page.CMSProperties.Root,
								page.CMSProperties.Category1,
								page.CMSProperties.Category2,
								page.CMSProperties.Category3,
								Constants.NotionCMSCategoryWildcard,
								null
							);
							result.Add(categoryKey, page.ID);
						}

						if (!string.IsNullOrWhiteSpace(page.CMSProperties.Category3)) {
							categoryKey = NotionCMSHelper.CreateCategorySlug(
								page.CMSProperties.Root,
								page.CMSProperties.Category1,
								page.CMSProperties.Category2,
								Constants.NotionCMSCategoryWildcard,
								null,
								null
							);
							result.Add(categoryKey, page.ID);
						}
						if (!string.IsNullOrWhiteSpace(page.CMSProperties.Category2)) {
							categoryKey = NotionCMSHelper.CreateCategorySlug(
								page.CMSProperties.Root,
								page.CMSProperties.Category1,
								Constants.NotionCMSCategoryWildcard,
								null,
								null,
								null
							);
							result.Add(categoryKey, page.ID);
						}
						if (!string.IsNullOrWhiteSpace(page.CMSProperties.Category1)) {
							categoryKey = NotionCMSHelper.CreateCategorySlug(
								page.CMSProperties.Root,
								Constants.NotionCMSCategoryWildcard,
								null,
								null,
								null,
								null
							);
							result.Add(categoryKey, page.ID);
						}

						categoryKey = NotionCMSHelper.CreateCategorySlug(
								Constants.NotionCMSCategoryWildcard,
								null,
								null,
								null,
								null,
								null
							);
						result.Add(categoryKey, page.ID);
					}
					return result.ToDictionary();
				},
				keyComparer: StringComparer.InvariantCultureIgnoreCase
			);

			_categoriesByCategorySlug = new BulkFetchActionCache<string, string[]>(
				() => {
					var result = new LookupEx<string, string>(StringComparer.InvariantCultureIgnoreCase);
					foreach (var page in Repository.Resources.Where(r => r.Type == LocalNotionResourceType.Page).Cast<LocalNotionPage>()) {
						if (string.IsNullOrWhiteSpace(page.CMSProperties.Root))
							continue;
						result.Add(string.Empty, page.CMSProperties.Root);

						if (string.IsNullOrWhiteSpace(page.CMSProperties.Category1))
							continue;
						result.Add(NotionCMSHelper.CreateCategorySlug(page.CMSProperties.Root, null, null, null, null, null), page.CMSProperties.Category1);

						if (string.IsNullOrWhiteSpace(page.CMSProperties.Category2))
							continue;
						result.Add(NotionCMSHelper.CreateCategorySlug(page.CMSProperties.Root, page.CMSProperties.Category1, null, null, null, null), page.CMSProperties.Category2);

						if (string.IsNullOrWhiteSpace(page.CMSProperties.Category3))
							continue;
						result.Add(NotionCMSHelper.CreateCategorySlug(page.CMSProperties.Root, page.CMSProperties.Category1, page.CMSProperties.Category2, null, null, null), page.CMSProperties.Category3);

						if (string.IsNullOrWhiteSpace(page.CMSProperties.Category4))
							continue;
						result.Add(NotionCMSHelper.CreateCategorySlug(page.CMSProperties.Root, page.CMSProperties.Category1, page.CMSProperties.Category2, page.CMSProperties.Category3, null, null), page.CMSProperties.Category4);

						if (string.IsNullOrWhiteSpace(page.CMSProperties.Category5))
							continue;
						result.Add(NotionCMSHelper.CreateCategorySlug(page.CMSProperties.Root, page.CMSProperties.Category1, page.CMSProperties.Category2, page.CMSProperties.Category3, page.CMSProperties.Category4, null), page.CMSProperties.Category5);

					}
					return result.ToDictionary();
				},
				keyComparer: StringComparer.InvariantCultureIgnoreCase
			);

		}

		public ILocalNotionRepository Repository { get; }

		public string CMSDatabaseID { get; init; }

		public IEnumerable<LocalNotionPage> GetNotionCMSPages(string root, params string[] categories) {
			var categoryKey = NotionCMSHelper.CreateCategorySlug(root, categories);
			if (_pagesByCategorySlug.ContainsCachedItem(categoryKey))
				return
				_pagesByCategorySlug[categoryKey]
				.Select(nid => Repository.GetResource(nid))
				.Cast<LocalNotionPage>()
				.OrderBy(p => p.CMSProperties?.Sequence ?? 0);

			return Enumerable.Empty<LocalNotionPage>();
		}

		public string[] GetRoots() {
			return _categoriesByCategorySlug[string.Empty];
		}

		public string[] GetSubCategories(string root, params string[] categories) {
			return _categoriesByCategorySlug[NotionCMSHelper.CreateCategorySlug(root, categories)];
		}

		public bool ContainsSlug(string slug) 
			=> _itemsBySlug.ContainsCachedItem(slug);

		public bool TryLookupSlug(string slug, out CMSArtifact[] artifacts) {
			artifacts =  _itemsBySlug[slug];
			return artifacts != null;
		}

		private void FlushCache() {
			_itemsBySlug.Flush();
			_pagesByCategorySlug?.Flush();
			_categoriesByCategorySlug?.Flush();
		}

	}
}
