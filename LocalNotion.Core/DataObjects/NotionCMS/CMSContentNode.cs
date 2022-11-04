using System.Runtime.CompilerServices;
using FluentNHibernate.Utils;
using Hydrogen;
using Hydrogen.Application;

namespace LocalNotion.Core;

public class CMSContentNode {

	public string Title { get; set; }

	public string Slug { get; set; }


	public bool IsCategoryNode => !Content.Any() || Children.Any();

	public List<LocalNotionPage> Content { get; init; } = new();

	public CMSContentNode Parent { get; set; }

	public List<CMSContentNode> Children { get; init; } = new();


	public CMSContentNode GetRoot() 
		=> this.Visit(x => x.Parent).Last();

	public CMSContentNode GetLogicalContentRoot() {
		// Consider scenario:
		// /product/local-notion        <-- sectioned page (or normal page)
		// /product/local-notion/docs   <-- this is the logical content root 
		// /product/local-notion/docs/configuration <-- this a category
		// /product/local-notion/docs/configuration/setup-windows <-- this an article
		
		var result = this;
		foreach(var ancestor in this.Visit(x => x.Parent)) {
			// If a previous ancestor had content, it means it's a page/sectioned page/gallery page, thus we do not consider that 
			if (ancestor.Content.Count > 0) {
				break;
			}
			result = ancestor;
		}

		return result;
	}

}
