using Hydrogen;

namespace LocalNotion.Core;

public class CMSContentNode {

	public string Slug { get; set; } = string.Empty;

	public CMSContentType Type {
		get {
			var rawContent = NonFramingContent.ToArray(); // filter out menus/footers
			if (rawContent.Length == 0 && !Children.Any()) 
				return CMSContentType.None;
			if (rawContent.Length == 1 && rawContent.Single().Type == LocalNotionResourceType.Page)
				return CMSContentType.Page;
			if (rawContent.Any() && rawContent.All(x => x.CMSProperties.PageType == CMSPageType.Section))
				return CMSContentType.SectionedPage;
			if (Children.Any() && Children.All(x => x.NonFramingContent.Any() && x.NonFramingContent.All(y => y.CMSProperties.PageType == CMSPageType.Gallery)))
				return CMSContentType.Gallery;
			if (Children.Any())
				return CMSContentType.Book;
			return CMSContentType.None;
		}
	}
	
	public string Title {
		get {
			var titleOverride = TitleOverride;
			if (titleOverride != null)
				return titleOverride;

			var rawContent = NonFramingContent.ToArray(); // filter out menus/footers
			switch(Type) {
				case CMSContentType.None:
					return string.Empty;
				case CMSContentType.Page:
					return rawContent.FirstOrDefault()?.Title ?? "Untitled";
				case CMSContentType.SectionedPage:
					return rawContent.FirstOrDefault()?.CMSProperties.Categories.LastOrDefault() ?? rawContent.FirstOrDefault()?.Title ?? "Untitled";
				case CMSContentType.Gallery:
					return rawContent.FirstOrDefault()?.CMSProperties.Root ?? "Untitled";
				case CMSContentType.Book:
					var firstChild = this.Visit(x => x.Children).FirstOrDefault(child => child.NonFramingContent.Any());
					if (firstChild != null) {
						var childCategories = firstChild.NonFramingContent.First().CMSProperties.Categories.ToArray();
						var stepsToRoot = this.Visit(x => x.Parent).Count() - 2;
						return stepsToRoot >= 0 && stepsToRoot < childCategories.Length ? childCategories[stepsToRoot] : "Untitled";
					}
					return "Untitled";
				case CMSContentType.File:
				default:
					throw new NotSupportedException(Type.ToString());
			}
		}
	}

	public string TitleOverride { get; set; } = null;

	public DateTime CreatedOn => Content.Any() ? Content.Min(x => x.CreatedOn) : DateTime.MinValue;

	public DateTime LastEditedOn => Content.Any() ? Content.Min(x => x.LastEditedOn) : DateTime.MinValue;

	public string Summary => Content.FirstOrDefault(x => !x.CMSProperties.Summary.IsNullOrWhiteSpace())?.CMSProperties.Summary ?? string.Empty;

	public int Sequence => Content.Min(x => x.CMSProperties.Sequence) ?? int.MaxValue;

	public LocalNotionThumbnail Thumbnail => Content.FirstOrDefault(x => x.Thumbnail.Type != ThumbnailType.None)?.Thumbnail ?? LocalNotionThumbnail.None;

	public IEnumerable<string> Keywords => Content.SelectMany(x => x.Keywords).Distinct();

	public IEnumerable<string> Tags => Content.SelectMany(x => x.CMSProperties.Tags).Distinct();

	public IEnumerable<string> FeatureImageBlocks => Content.Where(x => !string.IsNullOrWhiteSpace(x.FeatureImageID)).Select(x => x.FeatureImageID);

	public CMSContentNode Root => this.Visit(x => x.Parent).Last();

	public CMSContentNode Parent { get; set; }

	public List<LocalNotionPage> Content { get; } = [];

	internal IEnumerable<LocalNotionPage> NonFramingContent => Content.Where(x => !x.CMSProperties.PageType.IsIn(CMSPageType.Header, CMSPageType.NavBar, CMSPageType.Footer, CMSPageType.Internal));

	public List<CMSContentNode> Children { get; } = [];
	
	public LocalNotionPage Header => 
		!Tags.Contains(Constants.TagHideHeader) ?
		Content.FirstOrDefault(x => x.CMSProperties.PageType == CMSPageType.Header) ?? Parent?.Header :
		default;


	public LocalNotionPage NavBar => 
			!Tags.Contains(Constants.TagHideNavBar) ?
			Content.FirstOrDefault(x => x.CMSProperties.PageType == CMSPageType.NavBar) ?? Parent?.NavBar :
			default;

	public LocalNotionPage Footer => 
		!Tags.Contains(Constants.TagHideFooter) ?
			Content.FirstOrDefault(x => x.CMSProperties.PageType == CMSPageType.Footer) ?? Parent?.Footer :
			default;

	public LocalNotionPage Internal => 
		!Tags.Contains(Constants.TagHideInternal) ?
			Content.FirstOrDefault(x => x.CMSProperties.PageType == CMSPageType.Internal) ?? Parent?.Internal :
			default;

	public CMSContentNode GetLogicalContentRoot() {
		// Consider scenario:
		// /product/local-notion        <-- sectioned page (or normal page)
		// /product/local-notion/docs   <-- this is the logical content root 
		// /product/local-notion/docs/configuration <-- this a category
		// /product/local-notion/docs/configuration/setup-windows <-- this an article
		
		var result = this;
		foreach(var ancestor in this.Visit(x => x.Parent)) {
			// If a previous ancestor had content, it means it's a page/sectioned page/gallery page, thus we do not consider that 
			if (ancestor.NonFramingContent.Any()) {
				break;
			}
			result = ancestor;
		}

		return result;
	}

}
