using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Hydrogen;
using Microsoft.Win32;

namespace LocalNotion.Core {
	public class NotionCms : INotionCMS {
		private readonly ICache<string, string> _resourceBySlug;
		private readonly ICache<string, string[]> _articlesByCategorySlug;
		private readonly ICache<string, string[]> _categoriesByCategorySlug;

		public NotionCms(ILocalNotionRepository repository) {
			Repository = repository;
			Repository.Changed += _ => FlushCache();

			_resourceBySlug = new BulkFetchActionCache<string, string>(
				() => {
					var result = new Dictionary<string, string>(StringComparer.InvariantCultureIgnoreCase);
					foreach (var resource in Repository.Resources) {
						foreach(var resourceRender in resource.Renders.Values) {
							result[resourceRender.Slug] = resource.ID;
						}
						if (resource is LocalNotionPage { CMSProperties: not null } lnp)
							result[lnp.CMSProperties.CustomSlug] = resource.ID;
					}
					return result;
				},
				keyComparer: StringComparer.InvariantCultureIgnoreCase
			);

			_articlesByCategorySlug = new BulkFetchActionCache<string, string[]>(
				() => {
					var result = new LookupEx<string, string>(StringComparer.InvariantCultureIgnoreCase);
					foreach (var article in Repository.Resources.Where(r => r.Type == LocalNotionResourceType.Page).Cast<LocalNotionPage>()) {
						var categoryKey = NotionCMSHelper.CreateCategorySlug(article.CMSProperties.Root, article.CMSProperties.Category1, article.CMSProperties.Category2, article.CMSProperties.Category3, article.CMSProperties.Category4, article.CMSProperties.Category5);
						result.Add(categoryKey, article.ID);
					}
					return result.ToDictionary();
				},
				keyComparer: StringComparer.InvariantCultureIgnoreCase
			);

			_categoriesByCategorySlug = new BulkFetchActionCache<string, string[]>(
				() => {
					var result = new LookupEx<string, string>(StringComparer.InvariantCultureIgnoreCase);
					foreach (var article in Repository.Resources.Where(r => r.Type == LocalNotionResourceType.Page).Cast<LocalNotionPage>()) {
						if (string.IsNullOrWhiteSpace(article.CMSProperties.Root))
							continue;
						result.Add(string.Empty, article.CMSProperties.Root);

						if (string.IsNullOrWhiteSpace(article.CMSProperties.Category1))
							continue;
						result.Add(NotionCMSHelper.CreateCategorySlug(article.CMSProperties.Root, null, null, null, null, null), article.CMSProperties.Category1);

						if (string.IsNullOrWhiteSpace(article.CMSProperties.Category2))
							continue;
						result.Add(NotionCMSHelper.CreateCategorySlug(article.CMSProperties.Root, article.CMSProperties.Category1, null, null, null, null), article.CMSProperties.Category2);

						if (string.IsNullOrWhiteSpace(article.CMSProperties.Category3))
							continue;
						result.Add(NotionCMSHelper.CreateCategorySlug(article.CMSProperties.Root, article.CMSProperties.Category1, article.CMSProperties.Category2, null, null, null), article.CMSProperties.Category3);

						if (string.IsNullOrWhiteSpace(article.CMSProperties.Category4))
							continue;
						result.Add(NotionCMSHelper.CreateCategorySlug(article.CMSProperties.Root, article.CMSProperties.Category1, article.CMSProperties.Category2, article.CMSProperties.Category3, null, null), article.CMSProperties.Category4);

						if (string.IsNullOrWhiteSpace(article.CMSProperties.Category5))
							continue;
						result.Add(NotionCMSHelper.CreateCategorySlug(article.CMSProperties.Root, article.CMSProperties.Category1, article.CMSProperties.Category2, article.CMSProperties.Category3, article.CMSProperties.Category4, null), article.CMSProperties.Category5);

					}
					return result.ToDictionary();
				},
				keyComparer: StringComparer.InvariantCultureIgnoreCase
			);
		}

		public ILocalNotionRepository Repository { get; }

		public IEnumerable<LocalNotionPage> GetNotionCMSPages(string root, params string[] categories) {
			return _articlesByCategorySlug[NotionCMSHelper.CreateCategorySlug(root, categories)].Select(nid => Repository.GetResource(nid)).Cast<LocalNotionPage>();
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
			_articlesByCategorySlug?.Flush();
			_categoriesByCategorySlug?.Flush();
		}

	}
}
