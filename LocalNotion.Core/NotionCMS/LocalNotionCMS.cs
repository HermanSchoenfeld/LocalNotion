using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Mime;
using System.Security.AccessControl;
using System.Text;
using System.Threading.Tasks;
using FluentNHibernate.Conventions;
using Hydrogen;
using Microsoft.Win32;
using Notion.Client;

namespace LocalNotion.Core;

public class LocalNotionCMS : ILocalNotionCMS {
	private readonly ICache<string, CMSContentNode> _contentHierarchy;

	public LocalNotionCMS(string cmsDatabaseID, ILocalNotionRepository repository) {
		Repository = repository;
		Repository.Changed += _ => FlushCache();
		CMSDatabaseID = cmsDatabaseID;

		_contentHierarchy = new BulkFetchActionCache<string, CMSContentNode>(() => FetchContentHierarchy(cmsDatabaseID), keyComparer: StringComparer.InvariantCultureIgnoreCase);
	}

	public ILocalNotionRepository Repository { get; }

	public string CMSDatabaseID { get; init; }

	public bool ContainsSlug(string slug)
		=> _contentHierarchy.ContainsCachedItem(slug);

	public bool TryGetContent(string slug, out CMSContentNode contentNode, out CMSContentType contentType) {
		contentNode = _contentHierarchy[slug];
		if (contentNode == null) {
			contentType = CMSContentType.None;
			return false;
		}
		contentType = DetermineContentType(contentNode);
		return true;
	}

	public bool TryGetFooter(string slug, out LocalNotionPage page) {
		var contentNode = _contentHierarchy[slug];
		if (contentNode == null || !contentNode.Content.Any(x => x.CMSProperties.PageType == CMSPageType.Footer)) {
			page = null;
			return false;
		}
		page = contentNode.Content.First(x => x.CMSProperties.PageType == CMSPageType.Footer);
		return true;
	}

	protected virtual CMSContentType DetermineContentType(CMSContentNode node) {
		// Possibilities for a slug are:

		// CASE          ContentCount      ChildCount     Majority Page Type          Content Type
		// 1             0                 0              N/A                         No Content
		// 2             1                 0              PAGE                        PAGE
		// 3             +                 *              SECTION                     SECTION
		// 4             +                 *              GALLERY (child type)        GALLERY   
		// 5             *                 +              PAGE                        BOOK

		var consumableNodeContent = node.Content.Where(LocalNotionCMSHelper.IsContent).ToArray(); // filter out menus/footers
		if (consumableNodeContent.Length == 0 && node.Children.Count == 0)
			return CMSContentType.None;

		if (consumableNodeContent.Length == 1 && node.Children.Count == 0 && consumableNodeContent[0].Type == LocalNotionResourceType.Page)
			return CMSContentType.Page;

		if (consumableNodeContent.Any() && consumableNodeContent.All(x => x.CMSProperties.PageType == CMSPageType.Section))
			return CMSContentType.SectionedPage;

		if (node.Visit(n => n.Children).SelectMany(n => n.Content).Where(LocalNotionCMSHelper.IsContent).All(x => x.CMSProperties.PageType == CMSPageType.Gallery))
			return CMSContentType.Gallery;

		if (node.Children.Any())
			return CMSContentType.Book;

		// content has a mix of page types and no children, so use most used page type
		var majorityPageType =
			consumableNodeContent
				.ToLookup(x => x.CMSProperties.PageType, _ => 1)
				.AggregateValue(0, (acc, t) => acc + t)
				.MaxBy(x => x.Value)
				.Key;

		return majorityPageType switch {
			CMSPageType.Page => CMSContentType.Book,
			CMSPageType.Section => CMSContentType.SectionedPage,
			CMSPageType.Gallery => CMSContentType.Gallery,
			_ => throw new ArgumentOutOfRangeException()
		};

	}

	private void FlushCache() {
		_contentHierarchy?.Flush();
	}

	private Dictionary<string, CMSContentNode> FetchContentHierarchy(string cmsDatabaseID) {
		// first create the content hierarchy tree
		var tree = new Dictionary<string, CMSContentNode>(StringComparer.InvariantCultureIgnoreCase);

		// Note: the tree is encountered in an unordered manner
		foreach (var page in GetCMSPages(cmsDatabaseID)) {
			CreatePageNode(page);
		}

		// Sort all item children by their sequence
		tree.Values.ForEach(v => v.Content.Sort(new ProjectionComparer<LocalNotionPage, int?>(x => x.CMSProperties.Sequence)));
		
		// Sort all children
		tree.Values.Visit(n => n.Children).ForEach(c => c.Children.Sort(new ProjectionComparer<CMSContentNode,int>( x=> x.Content.FirstOrDefault()?.CMSProperties.Sequence ?? 0)));

		return tree;

		void CreatePageNode(LocalNotionPage page) {
			var slug = page.CMSProperties.CustomSlug;
			var node = GetOrCreateNode(page.Title, page.CMSProperties.Categories, slug);
			node.Content.Add(page);
		}


		CMSContentNode GetOrCreateNode(string title, IEnumerable<string> slugParts, string slugOverride = null) {

			// Calculate node slug
			var slugPartsArr = slugParts as string[] ?? slugParts.ToArray();
			var slug = Tools.Url.StripAnchorTag( slugOverride ?? LocalNotionCMSHelper.CalculateSlug(slugPartsArr.Concat(title)) );

			//if (string.IsNullOrWhiteSpace(slug))
			//	return null;

			// Fetch node by slug, if exists return it
			if (tree.TryGetValue(slug, out var node))
				return node;
			
			// Node doesn't exist, so we need to create it. First we fetch/create the parent.

			var parentNode = slugPartsArr.Any() ? GetOrCreateNode(slugPartsArr[^1], slugPartsArr[..^1]) : null;

			if (parentNode != null && StringComparer.InvariantCultureIgnoreCase.Equals(slug, parentNode.Slug)) {
				// if the current item is a section of a parent, then we don't create it as a content node.
				// We return the parent node and the caller adds it to the Pages collection
				return parentNode;
			} 
			
			node = new CMSContentNode {
				Title = title,
				Parent = parentNode,
				Slug = slug
			};
			tree.Add(slug, node);
			parentNode?.Children.Add(node);
			return node;
			
		}
	}

	private IEnumerable<LocalNotionPage> GetCMSPages(string cmsDatabaseID)
		=> Repository
		   .Resources
		   .Where(r => r is LocalNotionPage { CMSProperties: not null })
		   .Where(r => r.ParentResourceID == cmsDatabaseID)
		   .Cast<LocalNotionPage>();


	#region Currently unused

	// 2022-10-28 The below methods were written during development but are no longer required. They are left here for potential future use cases (although none are known).

	private IDictionary<string, string[]> FetchCategoriesByCategorySlug() {
		var result = new LookupEx<string, string>(StringComparer.InvariantCultureIgnoreCase);
		foreach (var page in Repository.Resources.Where(r => r.Type == LocalNotionResourceType.Page).Cast<LocalNotionPage>()) {
			if (string.IsNullOrWhiteSpace(page.CMSProperties.Root))
				continue;
			result.Add(string.Empty, page.CMSProperties.Root);

			if (string.IsNullOrWhiteSpace(page.CMSProperties.Category1))
				continue;
			result.Add(LocalNotionCMSHelper.CalculateSlug(new[] { page.CMSProperties.Root, null, null, null, null, null }), page.CMSProperties.Category1);

			if (string.IsNullOrWhiteSpace(page.CMSProperties.Category2))
				continue;
			result.Add(LocalNotionCMSHelper.CalculateSlug(new[] { page.CMSProperties.Root, page.CMSProperties.Category1, null, null, null, null }), page.CMSProperties.Category2);

			if (string.IsNullOrWhiteSpace(page.CMSProperties.Category3))
				continue;
			result.Add(LocalNotionCMSHelper.CalculateSlug(new[] { page.CMSProperties.Root, page.CMSProperties.Category1, page.CMSProperties.Category2, null, null, null }), page.CMSProperties.Category3);

			if (string.IsNullOrWhiteSpace(page.CMSProperties.Category4))
				continue;
			result.Add(LocalNotionCMSHelper.CalculateSlug(new[] { page.CMSProperties.Root, page.CMSProperties.Category1, page.CMSProperties.Category2, page.CMSProperties.Category3, null, null }), page.CMSProperties.Category4);

			if (string.IsNullOrWhiteSpace(page.CMSProperties.Category5))
				continue;
			result.Add(LocalNotionCMSHelper.CalculateSlug(new[] { page.CMSProperties.Root, page.CMSProperties.Category1, page.CMSProperties.Category2, page.CMSProperties.Category3, page.CMSProperties.Category4, null }), page.CMSProperties.Category5);

		}
		return result.ToDictionary();
	}

	private IDictionary<string, string[]> FetchPagesByCategorySlug() {

		var result = new LookupEx<string, string>(StringComparer.InvariantCultureIgnoreCase);
		foreach (var page in Repository.Resources.Where(r => r is LocalNotionPage { CMSProperties: not null }).Cast<LocalNotionPage>()) {
			// Add entry for parent container 
			var categoryKey = LocalNotionCMSHelper.CalculateSlug(
				new[] {
				page.CMSProperties.Root,
				page.CMSProperties.Category1,
				page.CMSProperties.Category2,
				page.CMSProperties.Category3,
				page.CMSProperties.Category4,
				page.CMSProperties.Category5
				}
			);
			result.Add(categoryKey, page.ID);
			result.Add(categoryKey + $"/{Constants.NotionCMSCategoryWildcard}", page.ID);

			// Add wildcard entry for all ancestor container categories
			if (!string.IsNullOrWhiteSpace(page.CMSProperties.Category5)) {
				categoryKey = LocalNotionCMSHelper.CalculateSlug(
			new[] {
					page.CMSProperties.Root,
					page.CMSProperties.Category1,
					page.CMSProperties.Category2,
					page.CMSProperties.Category3,
					page.CMSProperties.Category4,
					Constants.NotionCMSCategoryWildcard
					}
				);
				result.Add(categoryKey, page.ID);
			}

			if (!string.IsNullOrWhiteSpace(page.CMSProperties.Category4)) {
				categoryKey = LocalNotionCMSHelper.CalculateSlug(
					new[] {
					page.CMSProperties.Root,
					page.CMSProperties.Category1,
					page.CMSProperties.Category2,
					page.CMSProperties.Category3,
					Constants.NotionCMSCategoryWildcard,
					null
					}
				);
				result.Add(categoryKey, page.ID);
			}

			if (!string.IsNullOrWhiteSpace(page.CMSProperties.Category3)) {
				categoryKey = LocalNotionCMSHelper.CalculateSlug(
					new[] {
					page.CMSProperties.Root,
					page.CMSProperties.Category1,
					page.CMSProperties.Category2,
					Constants.NotionCMSCategoryWildcard,
					null,
					null
					}
				);
				result.Add(categoryKey, page.ID);
			}
			if (!string.IsNullOrWhiteSpace(page.CMSProperties.Category2)) {
				categoryKey = LocalNotionCMSHelper.CalculateSlug(
					new[] {
					page.CMSProperties.Root,
					page.CMSProperties.Category1,
					Constants.NotionCMSCategoryWildcard,
					null,
					null,
					null
					}
				);
				result.Add(categoryKey, page.ID);
			}
			if (!string.IsNullOrWhiteSpace(page.CMSProperties.Category1)) {
				categoryKey = LocalNotionCMSHelper.CalculateSlug(
					new[] {
					page.CMSProperties.Root,
					Constants.NotionCMSCategoryWildcard,
					null,
					null,
					null,
					null
					}
				);
				result.Add(categoryKey, page.ID);
			}

			categoryKey = LocalNotionCMSHelper.CalculateSlug(
				new[] {
				Constants.NotionCMSCategoryWildcard,
				null,
				null,
				null,
				null,
				null
				}
			);
			result.Add(categoryKey, page.ID);
		}
		return result.ToDictionary();
	}

	private Dictionary<string, CMSArtifact[]> FetchItemsBySlug() {

		return Repository
			   .Resources
			   .Where(r => r.ParentResourceID == CMSDatabaseID && r is LocalNotionEditableResource { CMSProperties: not null } cmsResource && cmsResource.CMSProperties.Status == CMSPageStatus.Published)
			   .Cast<LocalNotionEditableResource>()
			   .OrderBy(r => r.CMSProperties.Sequence)
			   .GroupBy(r => r.CMSProperties.PageType switch {
				   CMSPageType.Gallery => Tools.Url.ToUrlSlug(r.CMSProperties.Root),   // Gallery pages are grouped by their roots (categories are badges)
				   _ => Tools.Url.StripAnchorTag(r.CMSProperties.CustomSlug)
			   })
			   .ToDictionary(
				   g => g.Key,
				   g => g.GroupBy(g2 => g2.CMSProperties.PageType)
						 .Select(g2 => new CMSArtifact { Type = g2.Key, Items = g.ToArray(), Slug = g.Key })
						 .ToArray()
			   );
	}

	private class CMSArtifact {

		public CMSPageType Type { get; set; }

		public LocalNotionEditableResource[] Items { get; set; }

		public string Slug { get; set; }

	}

	#endregion
}