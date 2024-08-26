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

	public bool ContainsCMSItem(string slug) {
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

	public void RemoveCMSItem(string slug) {
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
						MarkAnyRenderWhichReferencesPageAsDirty(page);
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
		CreateOrUpdatePageRender(page);
		var pageCategoryUrls = GetAllCategoryUrls(page.CMSProperties);
		if (page.CMSProperties.PageType == CMSPageType.Section)
			pageCategoryUrls = pageCategoryUrls.Skip(1); // skip head since head it's the page name (for section'ed items)

		foreach (var url in pageCategoryUrls)
			CreateOrUpdateArticleCategoryPage(url);
	}

	protected virtual void OnUpdatedPage(LocalNotionPage page) {
		CreateOrUpdatePageRender(page);
		MarkAnyRenderWhichReferencesPageAsDirty(page);
		foreach(var url in GetAllCategoryUrls(page.CMSProperties))
			CreateOrUpdateArticleCategoryPage(url);
	}
	
	protected virtual void OnRemovedPage(LocalNotionPage page, CMSProperties cmsProperties) {
		RemoveCMSItemReferencesTo(page.ID);
		var renderSlug = cmsProperties.CustomSlug;
		if (ContainsCMSItem(renderSlug)) {
			RemoveCMSItem(renderSlug);

			foreach(var url in GetAllCategoryUrls(page.CMSProperties))
				RemoveArticleCategoryPageIfEmpty(cmsProperties);
		}
	}

	#endregion
	
	#region Section Page Logic

	protected virtual void OnAddedSectionPage(LocalNotionPage sectionPage) {
		CreateOrUpdateSectionedPageRender(sectionPage);
	}
	protected virtual void OnUpdatedSectionPage(LocalNotionPage sectionPage) {
		CreateOrUpdateSectionedPageRender(sectionPage);
		MarkAnyRenderWhichReferencesPageAsDirty(sectionPage);
		foreach(var url in GetAllCategoryUrls(sectionPage.CMSProperties).Skip(1))  // skip head since head it's the page name (for section'ed items)
			CreateOrUpdateArticleCategoryPage(url);

	}

	protected virtual void OnRemovedSectionPage(LocalNotionPage page, CMSProperties cmsProperties) {
		RemoveCMSItemReferencesTo(page.ID);
		var renderSlug = cmsProperties.CustomSlug;
		if (TryGetCMSItem(renderSlug, out var cmsItem) && cmsItem.Parts.Length == 0) {
			RemoveCMSItem(renderSlug);

			foreach(var url in GetAllCategoryUrls(page.CMSProperties).Skip(1))  // skip head since head it's the page name (for section'ed items)
				RemoveArticleCategoryPageIfEmpty(page.CMSProperties);
		}
	}

	#endregion

	#region Gallery Page Logic

	protected virtual void OnAddedGalleryPage(LocalNotionPage galleryPage) {
		// Page is added, render with footer and menu
		// Add gallery page (or mark as dirty)
		// Update component pages with reference to gallery page
	}

	protected virtual void OnUpdatedGalleryPage(LocalNotionPage galleryPage) {
	}

	protected virtual void OnRemovedGalleryPage(LocalNotionPage galleryPage, CMSProperties cmsProperties) {
		RemoveCMSItemReferencesTo(galleryPage.ID);
		var renderSlug = cmsProperties.CustomSlug;
		RemoveCMSItem(renderSlug);
		if (TryGetCMSItem(renderSlug, out var cmsItem) && cmsItem.Parts.Length == 0) {
			RemoveCMSItem(renderSlug);

			foreach(var url in GetAllCategoryUrls(galleryPage.CMSProperties))
				RemoveArticleCategoryPageIfEmpty(galleryPage.CMSProperties);

		}
	}

	#endregion 

	#region Aux Methods
	
	private IEnumerable<string> GetAllCategoryUrls(CMSProperties cmsProperties) {
		var origSlug = cmsProperties.CustomSlug; // CMSHelper.CalculateSlug(cmsPage.CMSProperties.Categories);
		var slugParts = Tools.Url.StripAnchorTag(origSlug.TrimStart("/")).Split('/').Reverse().Skip(1).Reverse().ToArray();
		for (var i = slugParts.Length; i > 0; i--) {
			yield return Tools.Url.Combine(slugParts.Take(i));
		}


		//contentNode.Visit(x => x.Children).SelectMany(x => x.Content).Where(CMSHelper.IsPublicContent)
		//var slug = cmsProperties.CustomSlug;
		//var contentNode = this.CMSDatabase.GetContent(slug);
		//return contentNode.Parent is null ? [] : contentNode.Parent.Visit(x => x.Parent, x => x.IsCategoryNode).Select(x => x.Slug);

		//var categories = cmsProperties.Categories.ToArray();
		//for(var i = categories.Length; i > 0; i--) {
		//	yield return Tools.Url.Combine(cmsProperties.Categories.Select(Tools.Url.ToUrlSlug).Take(i));
		//}
	}

	public void RemoveCMSItemReferencesTo(string pageID) {
		foreach(var render in CMSItems.ToArray()) {
			if (render.ReferencesResource(pageID)) {
				render.RemovePageReference(pageID);
				if (render.Parts.Length > 0)
					render.Dirty = true;
				else
					RemoveCMSItem(render.Slug);
			}
		}
	}
	
	private void CreateOrUpdatePageRender(LocalNotionPage page) 
		=> CreateOrUpdateRender(CMSItemType.Page, 
			page.CMSProperties.CustomSlug, 
			page.Title ?? string.Empty, 
			page.CMSProperties?.Summary ?? page.Title,
			page is { CMSProperties: { }, Thumbnail.Type: ThumbnailType.Image } ? page.Thumbnail.Data : string.Empty,
			[page.ID], 
			page.Keywords
		);

	private void CreateOrUpdateSectionedPageRender(LocalNotionPage page) {
		// entire sectioned render is updated whenever an individual section is updated
		var slug = Tools.Url.StripAnchorTag(page.CMSProperties.CustomSlug);
		var parts = GetPageSectionsForSlug(slug).ToArray();
		Guard.Ensure(parts.Length > 0, "Sectioned page should have had at least one section (current argument)");
		var primaryPage = this.GetResource(parts[0].ID) as LocalNotionPage;
		Guard.Ensure(primaryPage != null, $"Section {parts[0].ID} was not a page");
		CreateOrUpdateRender(CMSItemType.SectionedPage, 
			slug, 
			primaryPage.Title, 
			primaryPage.CMSProperties?.Summary ?? primaryPage.Title,
			primaryPage is { CMSProperties: { }, Thumbnail.Type: ThumbnailType.Image } ? primaryPage.Thumbnail.Data : string.Empty,
			parts.Select(x => x.ID).ToArray(), 
			LocalNotionHelper.CombineMultiPageKeyWords(parts.Select(x => x.Keywords)).ToArray()
		);
	}

	private void CreateOrUpdateArticleCategoryPage(string slug) {
		var articles = GetArticlesForSlug(slug, out var title).ToArray();
		if (articles.Length > 0) {
			CreateOrUpdateRender(
				CMSItemType.ArticleCategory,
				slug,
				title,
				string.Empty,
				articles[0] is { CMSProperties: { }, Thumbnail.Type: ThumbnailType.Image } ? articles[0].Thumbnail.Data : string.Empty,
				articles.Select(x => x.ID).ToArray(),
				LocalNotionHelper.CombineMultiPageKeyWords(articles.Select(x => x.Keywords)).ToArray()
			);
		} else {
			if (ContainsCMSItem(slug))
				RemoveCMSItem(slug);
		}
	}

	private void CreateOrUpdateRender(CMSItemType itemType, string slug, string title, string description, string image, string[] parts, string[] keywords) 
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
	
	private void MarkAnyRenderWhichReferencesPageAsDirty(LocalNotionPage page) {
		// Mark any cms render that references this page as dirty
		foreach (var render in CMSItems) {
			if (render.ReferencesResource(page.ID)) {
				render.Dirty = true;
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

	private void RemoveArticleCategoryPageIfEmpty(CMSProperties cmsProperties) {
		foreach(var url in GetAllCategoryUrls(cmsProperties)) {
			if (GetArticlesForSlug(url, out _).ToArray() is { Length: 0 }) {
				RemoveCMSItem(url);
			}
		}
	}

	private IEnumerable<LocalNotionPage> GetArticlesForSlug(string slug, out string title) {
		if (!CMSDatabase.TryGetContent(slug, out var contentNode, out var contentType)) {
			title = string.Empty;
			return Enumerable.Empty<LocalNotionPage>();
		}

		if (contentType != CMSContentType.Book)
			throw new InvalidOperationException($"Slug '{slug}' was not a category");

		title = contentNode.Title;
		return contentNode.Visit(x => x.Children).SelectMany(x => x.Content).Where(CMSHelper.IsPublicContent);
	}

	private IEnumerable<LocalNotionPage> GetPageSectionsForSlug(string slug) {
		if (!CMSDatabase.TryGetContent(slug, out var contentNode, out var contentType))
			throw new InvalidOperationException($"Slug '{slug}' had no articles");
		return contentNode.Content.Where(x => x.CMSProperties.PageType == CMSPageType.Section).Where(CMSHelper.IsPublicContent);
	}

	#endregion
}
