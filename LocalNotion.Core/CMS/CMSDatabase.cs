// Copyright (c) Herman Schoenfeld 2018 - Present. All rights reserved. (https://sphere10.com/products/localnotion)
// Author: Herman Schoenfeld <herman@sphere10.com>
//
// Distributed under the GPLv3 software license, see the accompanying file LICENSE 
// or visit https://github.com/HermanSchoenfeld/localnotion/blob/master/LICENSE
//
// This notice must not be removed when duplicating this file or its contents, in whole or in part.

using AngleSharp.Text;
using Sphere10.Framework;

namespace LocalNotion.Core;

public class CMSDatabase : ICMSDatabase {
	private readonly ICache<string, CMSContentNode> _contentHierarchy;

	public CMSDatabase(ILocalNotionRepository repository) {
		Guard.ArgumentNotNull(repository, nameof(repository));
		Guard.Ensure(repository.CMSDatabaseID != null, "Repository must have a CMS Database ID");
		Repository = repository;
		Repository.Changed += _ => FlushCache();
		_contentHierarchy = new BulkFetchActionCache<string, CMSContentNode>(() => BuildContentHierarchy(repository.CMSDatabaseID, true), keyComparer: StringComparer.InvariantCultureIgnoreCase);
	}

	public ILocalNotionRepository Repository { get; }

	public string CMSDatabaseID => Repository.CMSDatabaseID;

	public IEnumerable<CMSContentNode> CMSContent => _contentHierarchy.GetAllCachedValues();

	public bool ContainsSlug(string slug)
		=> _contentHierarchy.ContainsCachedItem(slug);

	public CMSContentNode GetContent(string slug)
		=> _contentHierarchy[slug];

	public bool TryGetContent(string slug, out CMSContentNode contentNode) {
		contentNode = _contentHierarchy[slug];
		return contentNode != null;
	}


	private void FlushCache() {
		_contentHierarchy?.Purge();
	}

	private Dictionary<string, CMSContentNode> BuildContentHierarchy(string cmsDatabaseID, bool publishedOnly) {
		var allNodes = new Dictionary<string, CMSContentNode>(StringComparer.InvariantCultureIgnoreCase);
		var cmsItems = Repository
						.Resources
						.Where(r => r is LocalNotionPage { CMSProperties: not null })
						.Cast<LocalNotionPage>()
						.Where(r => cmsDatabaseID.IsIn(Repository.GetResourceAncestry(r).Select(a => a.ID)))
						.Where(x =>  !publishedOnly || CMSHelper.IsPublicContent(x))
						.ToArray();


		// Create nodes for all urls
		allNodes.Add(string.Empty, new CMSContentNode()); // root node is empty slug
		foreach (var item in cmsItems)
			foreach (var slug in Tools.Url.CalculateBreadcrumbFromPath(item.CMSProperties.CustomSlug))
				if (!allNodes.ContainsKey(slug))
					allNodes.Add(slug, new CMSContentNode() { Slug = slug });

		// Sort node ordering
		allNodes = allNodes.OrderBy(x => x.Key).ToDictionary();

		// Link all child nodes to parent nodes
		foreach(var (slug, child) in allNodes) {
			// root slug has not parent
			if (slug == string.Empty)
				continue;
			var parentSlug = Tools.Url.CalculateBreadcrumbFromPath(slug).Skip(1).FirstOrDefault() ?? string.Empty; // empty means root 
			var parentNode = allNodes[parentSlug];
			parentNode.Children.Add(child);
			child.Parent = parentNode;
		}
		
		// Add all content to corresponding node
		foreach(var item in cmsItems) {
			var pageSlug = Tools.Url.StripAnchorTag(item.CMSProperties.CustomSlug);
			var pageNode = allNodes[pageSlug];
			pageNode.Content.Add(item);
		}

		// Sort all node content
		foreach(var (_, node) in allNodes) {
			node.Content.Sort(ComparerBuilder.For<LocalNotionPage>().StartWith(x => x.CMSProperties.Sequence ?? int.MaxValue));
		}

		// Sort all node children
		foreach(var (_, node) in allNodes) {
			node.Children.Sort(ComparerBuilder.For<CMSContentNode>().StartWith(x => x.Content.FirstOrDefault()?.CMSProperties.Sequence ?? 0));
		}

		return allNodes;
	}


}