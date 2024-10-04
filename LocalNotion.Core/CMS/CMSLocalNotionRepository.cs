using Hydrogen;
using Microsoft.Win32;
using Notion.Client;
using System.IO;

namespace LocalNotion.Core;

public class CMSLocalNotionRepository : LocalNotionRepository {

	private readonly IFuture<CMSDatabase> _cmsDatabase;
	private CMSProperties _preUpdateCmsProperties;

	public CMSLocalNotionRepository(string registryFile, ILogger logger = null) 
		: base(registryFile, logger) {
		_cmsDatabase = Tools.Values.Future.LazyLoad( () =>  new CMSDatabase(this));
	}
	
	public CMSDatabase CMSDatabase => _cmsDatabase.Value;

	
	#region CMS Items

	public bool ContainsCmsItem(string slug) {
		CheckLoaded();
		return Registry.CMSItemsBySlug.ContainsKey(slug);
	}

	public bool TryGetCMSItem(string slug, out CMSItem cmsItem) {
		CheckLoaded();
		return Registry.CMSItemsBySlug.TryGetValue(slug, out cmsItem);
	}
	
	public CMSItem GetCMSItem(string slug) {
		CheckLoaded();
		if (!TryGetCMSItem(slug, out var cmsItem))
			throw new InvalidOperationException($"CMS Item '{slug}' does not exist");
		return cmsItem;
	}

	public void AddCMSItem(CMSItem cmsItem) {
		CheckLoaded();
		Registry.CMSItemsBySlug.Add(cmsItem.Slug, cmsItem);
	}

	public void UpdateCMSItem(CMSItem cmsItem) {
		CheckLoaded();
		Guard.Ensure(Registry.CMSItemsBySlug.ContainsKey(cmsItem.Slug), $"CMS Item '{cmsItem.Slug}' does not exist");
		Registry.CMSItemsBySlug[cmsItem.Slug] = cmsItem;
	}

	public void AddOrUpdateCMSItem(CMSItem cmsItem) {
		CheckLoaded();
		Registry.CMSItemsBySlug[cmsItem.Slug] = cmsItem;
	}

	public void RemoveCmsItem(string slug) {
		CheckLoaded();
		var cmsItem = GetCMSItem(slug);
		if (!string.IsNullOrWhiteSpace(cmsItem.RenderFileName)) {
			var renderFile = GetCMSItemRenderPath(cmsItem.RenderFileName);
			if (File.Exists(renderFile)) {
				Logger.Info($"Deleting CMS render '{renderFile}'");
				Tools.FileSystem.DeleteFile(renderFile);
			}
		}
		Registry.CMSItemsBySlug.Remove(slug);
	}

	private string GetCMSItemRenderPath(string renderFilename) {
		return Path.Join(this.Paths.GetResourceTypeFolderPath(LocalNotionResourceType.CMS, FileSystemPathType.Absolute), renderFilename);
	}

	#endregion


	#region Repository Handlers

	protected sealed override void OnResourceAdded(LocalNotionResource resource) {
		base.OnResourceAdded(resource);
		if (resource is LocalNotionPage { CMSProperties: not null } page) {
			switch (page.CMSProperties.PageType) {
				case CMSPageType.Header:
				case CMSPageType.NavBar:
				case CMSPageType.Footer:
					RecalculateAllFraming();
					break;
				case CMSPageType.Page:
					OnAddedPage(page);
					break;
				case CMSPageType.Section:
					OnAddedSectionPage(page);
					break;
				case CMSPageType.Gallery:
					OnAddedGalleryPage(page);
					break;
				default:
					throw new InvalidOperationException($"Unknown CMS Page Type '{page.CMSProperties.PageType}'");
			}
		}
	}

	protected sealed override void OnResourceRemoved(LocalNotionResource resource) {
		base.OnResourceRemoved(resource);
		if (resource is LocalNotionPage { CMSProperties: not null } page) {
			switch (page.CMSProperties.PageType) {
				case CMSPageType.Header:
				case CMSPageType.NavBar:
				case CMSPageType.Footer:
					RecalculateAllFraming();
					break;
				case CMSPageType.Page:
					OnRemovedPage(page, page.CMSProperties);
					break;
				case CMSPageType.Section:
					OnRemovedSectionPage(page, page.CMSProperties);
					break;
				case CMSPageType.Gallery:
					OnRemovedGalleryPage(page, page.CMSProperties);
					break;
				default:
					throw new InvalidOperationException($"Unknown CMS Page Type '{page.CMSProperties.PageType}'");
			}
		}
	}

	protected override void OnResourceUpdating(string resourceID) {
		base.OnResourceUpdating(resourceID);
		var resource = this.GetResource(resourceID);
		if (resource is LocalNotionEditableResource { CMSProperties: not null } lner) {
			_preUpdateCmsProperties = lner.CMSProperties;
		}
	}

	protected sealed override void OnResourceUpdated(LocalNotionResource resource) {
		base.OnResourceUpdated(resource);
		if (resource is LocalNotionPage { CMSProperties: not null } page) {
			switch (page.CMSProperties.PageType) {
				case CMSPageType.Header:
				case CMSPageType.NavBar:
				case CMSPageType.Footer:
					if (_preUpdateCmsProperties.PageType != page.CMSProperties.PageType) {
						RecalculateAllFraming();
					} else {
						MarkAnyCmsItemWhichReferencesPageAsDirty(page);
					}
					break;
				case CMSPageType.Page:
					if (_preUpdateCmsProperties.PageType != CMSPageType.Page) {
						RemovePageWithUpdatedType(page, _preUpdateCmsProperties);
						OnAddedPage(page);
					} else {
						OnUpdatedPage(page);
					}
					break;
				case CMSPageType.Section:
					if (_preUpdateCmsProperties.PageType != CMSPageType.Section) {
						RemovePageWithUpdatedType(page, _preUpdateCmsProperties);
						OnAddedSectionPage(page);
					} else {
						OnUpdatedSectionPage(page);
					}
					break;
				case CMSPageType.Gallery:
					if (_preUpdateCmsProperties.PageType != CMSPageType.Gallery) {
						RemovePageWithUpdatedType(page, _preUpdateCmsProperties);
						OnAddedGalleryPage(page);
					} else {
						OnUpdatedGalleryPage(page);
					}
					break;
				default:
					throw new InvalidOperationException($"Unknown CMS Page Type '{page.CMSProperties.PageType}'");
			}
		}
		_preUpdateCmsProperties = null;

		void RemovePageWithUpdatedType(LocalNotionPage page, CMSProperties pageCmsProperties) {
			switch (pageCmsProperties.PageType) {
				case CMSPageType.Section:
					OnRemovedSectionPage(page, pageCmsProperties);
					break;
				case CMSPageType.Gallery:
					OnRemovedGalleryPage(page, pageCmsProperties);
					break;
				case CMSPageType.Footer:
					// Doesn't need to do anything
					break;
				case CMSPageType.NavBar:
					// Doesn't need to do anything
					break;
				case CMSPageType.Page:
				default:
					OnRemovedPage(page, pageCmsProperties);
					break;
			}
		}
	}

	#endregion

	#region Page Logic

	protected virtual void OnAddedPage(LocalNotionPage page) {
		Guard.Ensure(page.CMSProperties.PageType == CMSPageType.Page, $"Not a {CMSPageType.Page}");

		// Create/update page render
		TouchSingularCmsItem(page);

		// Update any Categories pages which contain this page
		var breadCrumb = Tools.Url.CalculateBreadcrumbFromPath(page.CMSProperties.CustomSlug);
		breadCrumb = breadCrumb.Skip(1);   // skip head since: /category1/category2/article-name -> /category1/category2, /category1
		foreach (var url in breadCrumb)
			TouchContainerCmsItem(url);
		
	}

	protected virtual void OnUpdatedPage(LocalNotionPage page) {
		Guard.Ensure(page.CMSProperties.PageType == CMSPageType.Page, $"Not a {CMSPageType.Page}");

		TouchSingularCmsItem(page);
		MarkAnyCmsItemWhichReferencesPageAsDirty(page);

		var slug = page.CMSProperties.CustomSlug;
		var breadCrumb = Tools.Url.CalculateBreadcrumbFromPath(slug);
		breadCrumb = breadCrumb.Skip(1);   // skip head since: /category1/category2/article-name -> /category1/category2, /category1
		foreach(var url in breadCrumb)
			TouchContainerCmsItem(url);
	}
	
	protected virtual void OnRemovedPage(LocalNotionPage page, CMSProperties cmsProperties) {
		Guard.Ensure(cmsProperties.PageType == CMSPageType.Page, $"Not a {CMSPageType.Page}");

		// Remove the CMS Item for the page
		var renderSlug = cmsProperties.CustomSlug;
		if (ContainsCmsItem(renderSlug)) {
			RemoveCmsItem(renderSlug);
		}

		// Update/collect other CMS Items which reference this item
		RemoveCmsItemReferencesTo(page.ID);
	}

	#endregion
	
	#region Section Page Logic

	protected virtual void OnAddedSectionPage(LocalNotionPage sectionPage) {
		TouchSingularCmsItem(sectionPage);

		// Update any Categories pages which contain this page
		var breadCrumb = Tools.Url.CalculateBreadcrumbFromPath(sectionPage.CMSProperties.CustomSlug);
		breadCrumb = breadCrumb.Skip(1);   // skip head since: /category1/category2/page-name#section-name -> /category1/category2, /category1
		foreach (var url in breadCrumb)
			TouchContainerCmsItem(url);
	}
	protected virtual void OnUpdatedSectionPage(LocalNotionPage sectionPage) {
		TouchSingularCmsItem(sectionPage);
		MarkAnyCmsItemWhichReferencesPageAsDirty(sectionPage);

		// Update any Categories pages which contain this page
		var breadCrumb = Tools.Url.CalculateBreadcrumbFromPath(sectionPage.CMSProperties.CustomSlug);
		breadCrumb = breadCrumb.Skip(1);   // skip head since: /category1/category2/page-name#section-name -> /category1/category2, /category1
		foreach (var url in breadCrumb)
			TouchContainerCmsItem(url);
	}

	protected virtual void OnRemovedSectionPage(LocalNotionPage page, CMSProperties cmsProperties) {
		RemoveCmsItemReferencesTo(page.ID);

		// Remove entire sectioned page if no sections left
		var cmsItemSlug = cmsProperties.CustomSlug;
		if (TryGetCMSItem(cmsItemSlug, out var cmsItem) && cmsItem.Parts.Length == 0) {
			RemoveCmsItem(cmsItemSlug);

			// Update any Categories pages which contain this page
			var breadCrumb = Tools.Url.CalculateBreadcrumbFromPath(cmsItemSlug);
			breadCrumb = breadCrumb.Skip(1);   // skip head since: /category1/category2/page-name#section-name -> /category1/category2, /category1
			foreach (var url in breadCrumb)
				TouchContainerCmsItem(url);
		}
	}

	#endregion

	#region Gallery Page Logic

	protected virtual void OnAddedGalleryPage(LocalNotionPage galleryPage) {
		var galleryPageUrl = galleryPage.CMSProperties.CustomSlug;
		var breadCrumb = Tools.Url.CalculateBreadcrumbFromPath(galleryPageUrl).ToArray();
		var galleryUrl = breadCrumb.Skip(1).First();

		TouchSingularCmsItem(galleryPage); // article page
		TouchContainerCmsItem(galleryUrl); // gallery card which links to article page

		// Update any Categories pages which contain this page
		breadCrumb = breadCrumb.Skip(2).ToArray();   // skip head since: /category1/category2/gallery/card-page -> /category1/category2, /category1
		foreach (var url in breadCrumb)
			TouchContainerCmsItem(url);
	}

	protected virtual void OnUpdatedGalleryPage(LocalNotionPage galleryPage) {
		var galleryPageUrl = galleryPage.CMSProperties.CustomSlug;
		var breadCrumb = Tools.Url.CalculateBreadcrumbFromPath(galleryPageUrl).ToArray();
		var galleryUrl = breadCrumb.Skip(1).First();
		TouchSingularCmsItem(galleryPage);
		TouchContainerCmsItem(galleryUrl);
		MarkAnyCmsItemWhichReferencesPageAsDirty(galleryPage);

		// Update any Categories pages which contain this page
		breadCrumb = breadCrumb.Skip(2).ToArray();   // skip head since: /category1/category2/gallery/card-page -> /category1/category2, /category1
		foreach (var url in breadCrumb)
			TouchContainerCmsItem(url);
	}

	protected virtual void OnRemovedGalleryPage(LocalNotionPage galleryPage, CMSProperties cmsProperties) {
		var galleryPageUrl = galleryPage.CMSProperties.CustomSlug;
		var breadCrumb = Tools.Url.CalculateBreadcrumbFromPath(galleryPageUrl).ToArray();
		var galleryUrl = breadCrumb.Skip(1).First();

		RemoveCmsItemReferencesTo(galleryPage.ID);
		RemoveCmsItem(galleryUrl);
		if (TryGetCMSItem(galleryUrl, out var cmsItem) && cmsItem.Parts.Length == 0) {
			RemoveCmsItem(galleryUrl);

			// Update any Categories pages which contain this page
			breadCrumb = breadCrumb.Skip(2).ToArray();   // skip head since: /category1/category2/gallery/card-page -> /category1/category2, /category1
			foreach (var url in breadCrumb)
				TouchContainerCmsItem(url);
		}
	}

	#endregion 

	#region Aux Methods


	public void RemoveCmsItemReferencesTo(string pageID) {
		foreach(var render in CMSItems.ToArray()) {
			if (render.ReferencesResource(pageID)) {
				render.RemovePageReference(pageID);
				if (render.Parts.Length > 0)
					render.Dirty = true;
				else
					RemoveCmsItem(render.Slug);
			}
		}
	}
	
	private void TouchSingularCmsItem(LocalNotionPage page) {
		if (!CalculateCmsItem(page.CMSProperties.CustomSlug, out var slug, out var type, out var title, out var description, out var image, out var parts, out var keywords))
			throw new InvalidOperationException($"Not a valid CMS Item: {page.Title} ({page.ID})");
		AddOrUpdateCmsItem(type, slug, title, description, image, parts, keywords);
	}

	private void TouchContainerCmsItem(string containerItemSlug) {
		if (!CalculateCmsItem(containerItemSlug, out var slug, out var type, out var title, out var description, out var image, out var parts, out var keywords))
			throw new InvalidOperationException($"Not a valid container CMS Item: {containerItemSlug}");

		if (parts.Length > 0) {
			AddOrUpdateCmsItem(type, slug, title, description, image, parts, keywords);
		} else {
			if (ContainsCmsItem(slug))
				RemoveCmsItem(slug);
		}
	}
	
	private void AddOrUpdateCmsItem(CMSItemType itemType, string slug, string title, string description, string image, string[] parts, string[] keywords) 
		=> AddOrUpdateCMSItem(new CMSItem {
			Slug = slug,
			ItemType = itemType,
			Title = title ?? string.Empty,
			Description = description ?? string.Empty,
			Keywords = keywords,
			Image =  image ?? string.Empty,
			Author = string.Empty,
			HeaderID = CMSDatabase.FindComponentPage(slug, CMSPageType.Header, out var headerPage) ? headerPage.ID : null ,
			MenuID = CMSDatabase.FindComponentPage(slug, CMSPageType.NavBar, out var menuPage) ? menuPage.ID : null,
			FooterID = CMSDatabase.FindComponentPage(slug, CMSPageType.Footer, out var footerPage) ? footerPage.ID : null,
			Parts = parts ?? Array.Empty<string>(),
			Dirty = true,
			RenderFileName = TryGetCMSItem(slug, out var existingRender) ? existingRender.RenderFileName : null
		});
	
	private void MarkAnyCmsItemWhichReferencesPageAsDirty(LocalNotionPage page) {
		// Mark any cms render that references this page as dirty
		foreach (var cmsItem in CMSItems) {
			if (cmsItem.ReferencesResource(page.ID)) {
				cmsItem.Dirty = true;
			}
		}
	}

	private void RecalculateAllFraming() {
		foreach (var render in CMSItems) {
			var headerPageID = CMSDatabase.FindComponentPage(render.Slug, CMSPageType.Header, out var headerPage) ? headerPage.ID : null;
			var menuPageID = CMSDatabase.FindComponentPage(render.Slug, CMSPageType.NavBar, out var menuPage) ? menuPage.ID : null;
			var footerPageID = CMSDatabase.FindComponentPage(render.Slug, CMSPageType.Footer, out var footerPage) ? footerPage.ID : null;

			if (render.HeaderID != headerPageID || render.MenuID != menuPageID || render.FooterID != footerPageID) {
				render.HeaderID = headerPageID;
				render.MenuID = menuPageID;
				render.FooterID = footerPageID;
				render.Dirty = true;
			}
		}
	}

	//private void RemoveArticleCategoryPageIfEmpty(CMSProperties cmsProperties) {
	//	foreach(var url in GetAllParentUrls(cmsProperties)) {
	//		if (GetArticlesForSlug(url, out _).ToArray() is { Length: 0 }) {
	//			RemoveCmsItem(url);
	//		}
	//	}
	//}


	private bool CalculateCmsItem(string cmsDatabaseSlug, out string slug, out CMSItemType type, out string title, out string description, out string image, out string[] parts, out string[] keywords) {
		// Ensure slug has no anchor tag
		slug = Tools.Url.StripAnchorTag(cmsDatabaseSlug);

		// Get the CMS content code for slug
		if (!CMSDatabase.TryGetContent(slug, out var contentNode, out var contentType)) {
			type = 0;
			title = string.Empty;
			description = string.Empty;
			image = string.Empty;
			parts = [];
			keywords = [];
			return false;
		}

		title = contentNode.Title ?? string.Empty;
		description = string.Empty;
		image = string.Empty;
		parts = [];
		keywords = [];
		LocalNotionPage[] pageParts;
		switch (contentType) {
			case CMSContentType.Book:
				type = CMSItemType.CategoryPage;
				pageParts = contentNode.Visit(x => x.Children).SelectMany(x => x.Content).Where(CMSHelper.IsPublicContent).ToArray();
				if (pageParts.Any()) {
					image = pageParts[0] is { CMSProperties: { }, Thumbnail.Type: ThumbnailType.Image } ? pageParts[0].Thumbnail.Data : string.Empty;
					keywords = LocalNotionHelper.CombineMultiPageKeyWords(pageParts.Select(x => x.Keywords)).ToArray();
				} 
				break;
			case CMSContentType.Gallery:
				type = CMSItemType.GalleryPage;
				pageParts = contentNode.Children.SelectMany(x => x.Content).Where(CMSHelper.IsPublicContent).ToArray();
				if (pageParts.Any()) {
					keywords = pageParts.Select(x => x.Title.ToLowerInvariant()).ToArray();
				}
				break;
			case CMSContentType.Page:
				type = CMSItemType.Page;
				pageParts = contentNode.Content.Where(CMSHelper.IsPublicContent).ToArray();
				if (pageParts.Any()) {
					var page = pageParts.First();
					description = page.CMSProperties?.Summary ?? page.Title;
					image = page is { CMSProperties: { }, Thumbnail.Type: ThumbnailType.Image } ? page.Thumbnail.Data : string.Empty;
					keywords = page.Keywords;
				}
				break;
			case CMSContentType.SectionedPage:
				type = CMSItemType.SectionedPage;
				pageParts = contentNode.Content.Where(CMSHelper.IsPublicContent).ToArray();
				if (pageParts.Any()) {
					var primaryPage = this.GetResource(pageParts[0].ID) as LocalNotionPage;
					title = primaryPage.Title;
					description = primaryPage.CMSProperties?.Summary ?? primaryPage.Title;
					image = primaryPage is { CMSProperties: { }, Thumbnail.Type: ThumbnailType.Image } ? primaryPage.Thumbnail.Data : string.Empty;
					keywords = LocalNotionHelper.CombineMultiPageKeyWords(pageParts.Select(x => x.Keywords)).ToArray();
				}
				break;
			case CMSContentType.File:
			case CMSContentType.None:
			default:
				throw new NotImplementedException(contentType.ToString());
		}
		parts = pageParts.Select(x => x.ID).ToArray();
		return true;
	}

	// REMOVE
	//private IEnumerable<LocalNotionPage> GetArticlesForSlug(string slug, out string title) {
	//	if (!CMSDatabase.TryGetContent(slug, out var contentNode, out var contentType)) {
	//		title = string.Empty;
	//		return Enumerable.Empty<LocalNotionPage>();
	//	}

	//	if (contentType != CMSContentType.Book)
	//		throw new InvalidOperationException($"Slug '{slug}' was not a book");

	//	title = contentNode.Title;
	//	return contentNode.Visit(x => x.Children).SelectMany(x => x.Content).Where(CMSHelper.IsPublicContent);
	//}

	// REMOVE
	//private IEnumerable<LocalNotionPage> GetGalleryCardsForSlug(string slug, out string title) {
	//	if (!CMSDatabase.TryGetContent(slug, out var contentNode, out var contentType)) {
	//		title = string.Empty;
	//		return Enumerable.Empty<LocalNotionPage>();
	//	}

	//	if (contentType != CMSContentType.Gallery)
	//		throw new InvalidOperationException($"Slug '{slug}' was not a gallery");

	//	title = contentNode.Title;
	//	return contentNode.Visit(x => x.Children).SelectMany(x => x.Content).Where(CMSHelper.IsPublicContent);
	//}

	//private string GetGalleryUrlFromCardProperties(CMSProperties cardProperties) 
	//	=> GetAllParentUrls(cardProperties).First();
		

	private IEnumerable<LocalNotionPage> GetPageSectionsForSlug(string slug) {
		if (!CMSDatabase.TryGetContent(slug, out var contentNode, out var contentType))
			throw new InvalidOperationException($"Slug '{slug}' had no articles");
		return contentNode.Content.Where(x => x.CMSProperties.PageType == CMSPageType.Section).Where(CMSHelper.IsPublicContent);
	}

	#endregion
}
