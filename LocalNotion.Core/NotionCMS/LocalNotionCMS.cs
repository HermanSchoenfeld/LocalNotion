using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Hydrogen;
using Microsoft.Win32;

namespace LocalNotion.Core {
	public class LocalNotionCMS : ILocalNotionCMS {
		private readonly ICache<string, string> _resourceBySlug;
		private readonly ICache<string, string[]> _pagesByCategorySlug;
		private readonly ICache<string, string[]> _categoriesByCategorySlug;

		public LocalNotionCMS(ILocalNotionRepository repository) {
			Repository = repository;
			Repository.Changed += _ => FlushCache();

			_resourceBySlug = new BulkFetchActionCache<string, string>(
				() => {
					var result = new Dictionary<string, string>(StringComparer.InvariantCultureIgnoreCase);
					foreach (var resource in Repository.Resources) {
						foreach (var resourceRender in resource.Renders.Values) {
							result[resourceRender.Slug] = resource.ID;
						}
						if (resource is LocalNotionPage { CMSProperties: not null } lnp)
							result[lnp.CMSProperties.CustomSlug] = resource.ID;
					}
					return result;
				},
				keyComparer: StringComparer.InvariantCultureIgnoreCase
			);

			_pagesByCategorySlug = new BulkFetchActionCache<string, string[]>(
				() => {
					var result = new LookupEx<string, string>(StringComparer.InvariantCultureIgnoreCase);
					foreach (var article in Repository.Resources.Where(r => r is LocalNotionPage { CMSProperties: not null }).Cast<LocalNotionPage>()) {
						// Add entry for parent container 
						var categoryKey = NotionCMSHelper.CreateCategorySlug(
							article.CMSProperties.Root,
							article.CMSProperties.Category1,
							article.CMSProperties.Category2,
							article.CMSProperties.Category3,
							article.CMSProperties.Category4,
							article.CMSProperties.Category5
						);
						result.Add(categoryKey, article.ID);
						result.Add(categoryKey + $"/{Constants.NotionCMSCategoryWildcard}", article.ID);

						// Add wildcard entry for all ancestor container categories
						if (!string.IsNullOrWhiteSpace(article.CMSProperties.Category5)) {
							categoryKey = NotionCMSHelper.CreateCategorySlug(
								article.CMSProperties.Root,
								article.CMSProperties.Category1,
								article.CMSProperties.Category2,
								article.CMSProperties.Category3,
								article.CMSProperties.Category4,
								Constants.NotionCMSCategoryWildcard
							);
							result.Add(categoryKey, article.ID);
						}

						if (!string.IsNullOrWhiteSpace(article.CMSProperties.Category4)) {
							categoryKey = NotionCMSHelper.CreateCategorySlug(
								article.CMSProperties.Root,
								article.CMSProperties.Category1,
								article.CMSProperties.Category2,
								article.CMSProperties.Category3,
								Constants.NotionCMSCategoryWildcard,
								null
							);
							result.Add(categoryKey, article.ID);
						}

						if (!string.IsNullOrWhiteSpace(article.CMSProperties.Category3)) {
							categoryKey = NotionCMSHelper.CreateCategorySlug(
								article.CMSProperties.Root,
								article.CMSProperties.Category1,
								article.CMSProperties.Category2,
								Constants.NotionCMSCategoryWildcard,
								null,
								null
							);
							result.Add(categoryKey, article.ID);
						}
						if (!string.IsNullOrWhiteSpace(article.CMSProperties.Category2)) {
							categoryKey = NotionCMSHelper.CreateCategorySlug(
								article.CMSProperties.Root,
								article.CMSProperties.Category1,
								Constants.NotionCMSCategoryWildcard,
								null,
								null,
								null
							);
							result.Add(categoryKey, article.ID);
						}
						if (!string.IsNullOrWhiteSpace(article.CMSProperties.Category1)) {
							categoryKey = NotionCMSHelper.CreateCategorySlug(
								article.CMSProperties.Root,
								Constants.NotionCMSCategoryWildcard,
								null,
								null,
								null,
								null
							);
							result.Add(categoryKey, article.ID);
						}

						categoryKey = NotionCMSHelper.CreateCategorySlug(
								Constants.NotionCMSCategoryWildcard,
								null,
								null,
								null,
								null,
								null
							);
						result.Add(categoryKey, article.ID);
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

		public bool TryLookupResourceBySlug(string slug, out string resourceID) {
			resourceID = _resourceBySlug[slug];
			return resourceID != null;
		}

		private void FlushCache() {
			_resourceBySlug?.Flush();
			_pagesByCategorySlug?.Flush();
			_categoriesByCategorySlug?.Flush();
		}

	}
}
