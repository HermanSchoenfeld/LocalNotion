using Hydrogen;

namespace LocalNotion.Core;

public class BreadCrumbGenerator : IBreadCrumbGenerator {

	public BreadCrumbGenerator(ILocalNotionRepository repository, ILinkGenerator linkGenerator) {
		Guard.ArgumentNotNull(repository, nameof(repository));
		Guard.ArgumentNotNull(linkGenerator, nameof(linkGenerator));
		Repository = repository;
		LinkGenerator = linkGenerator;
	}

	protected ILocalNotionRepository Repository { get; }

	protected ILinkGenerator LinkGenerator { get; }


	public virtual BreadCrumb CalculateBreadcrumb(LocalNotionResource from) 
		=> CalculateBreadcrumb(from, (from as LocalNotionEditableResource)?.CMSProperties);

	public virtual BreadCrumb CalculateBreadcrumb(LocalNotionResource from, CMSProperties cmsProperties) {
		const string DefaultUrl = "#";

		var ancestors = Repository.GetResourceAncestry(from).ToArray();
		if (ancestors.Length == 0)
			return BreadCrumb.Empty;

		var trail = new List<BreadCrumbItem>();

		// When generating crumb to inline database, we want to link to it's parent page + anchor to database
		var skippedCrumbName = string.Empty;
		var skippedCrumbID = string.Empty;
		foreach (var (item, i) in ancestors.WithIndex()) {
			BreadCrumbItemTraits traits = 0;

			if (i == 0)
				traits.SetFlags(BreadCrumbItemTraits.IsCurrentPage, true);

			if (item.Type == LocalNotionResourceType.Page)
				traits.SetFlags(BreadCrumbItemTraits.IsPage, true);

			
			if (item.Type == LocalNotionResourceType.Database) {
				traits.SetFlags(BreadCrumbItemTraits.IsDatabase, true);
				var isInternalDB = item.ParentResourceID != null &&  Repository.ContainsResource(item.ParentResourceID);
				if (isInternalDB) {
					skippedCrumbName = item.Title;
					skippedCrumbID = item.ID;
					continue;;
				}
			}

			var isCmsPage = item is LocalNotionPage { CMSProperties: not null } itemPage;
			if (isCmsPage) {
				traits.SetFlags(BreadCrumbItemTraits.IsCMSPage, true);
			}

			var isPartialPage = 
				isCmsPage && ((LocalNotionPage)item).CMSProperties.PageType.IsIn(CMSPageType.Section, CMSPageType.Footer);

			var hasUrl = LinkGenerator.TryGenerate(from, item.ID, RenderType.HTML, out var url, out var resource);
			traits.SetFlags(BreadCrumbItemTraits.HasUrl, hasUrl);
			if (!hasUrl)
				url = DefaultUrl;

			var data = string.Empty;
			if (resource is LocalNotionEditableResource lner) {
				data = lner.Thumbnail.Data;
				switch (lner.Thumbnail.Type) {
					case ThumbnailType.None:
						break;
					case ThumbnailType.Emoji:
						traits.SetFlags(BreadCrumbItemTraits.HasIcon, true);
						traits.SetFlags(BreadCrumbItemTraits.HasEmojiIcon, true);
						break;
					case ThumbnailType.Image:
						traits.SetFlags(BreadCrumbItemTraits.HasIcon, true);
						traits.SetFlags(BreadCrumbItemTraits.HasImageIcon, true);
						break;
					default:
						throw new ArgumentOutOfRangeException();
				}
			}

			// TODO: when implementing databases, the check is
			var parentIsCmsDatabase = item.ParentResourceID != null  && Repository.TryGetDatabase(item.ParentResourceID, out var database) && CMSHelper.IsCMSDatabase(database);   // note: properties can be null when only downloading pages
			var repoContainsParentResource = item.ParentResourceID != null && Repository.ContainsResource(item.ParentResourceID);
			//var parentIsCmsDatabase = item.ParentResourceID != null && !repoContainsParentResource;  // currently CMS database doesn't exist as a resource, but if it was page parent, it would
			var parentIsPartial = 
				repoContainsParentResource && 
				Repository.TryGetResource(item.ParentResourceID, out var parentResource) && 
				parentResource is LocalNotionEditableResource { CMSProperties.PageType: CMSPageType.Section or CMSPageType.Footer }; 

			var title = isPartialPage ? 
				 BuildCompositeSlugPartTitle(((LocalNotionEditableResource)item).CMSProperties.GetTipCategory(), item.Title) :
				 BuildCompositeSlugPartTitle(item.Title, skippedCrumbName);
			var breadCrumbItem = new BreadCrumbItem() {
				Type = item.Type,
				Text = title,
				Data = data,
				Traits = traits,
				Url = url + (!string.IsNullOrWhiteSpace(skippedCrumbID) ? $"#{skippedCrumbID.Replace("-", string.Empty)}" : string.Empty)
			};

			trail.Add(breadCrumbItem);
			
			skippedCrumbName = string.Empty;
			skippedCrumbID = string.Empty;

			#region Process CMS-based slug
			
			// if current item is a CMS item and we're in online mode, the remainder of trail is extracted from the slug
			if (LinkGenerator.Mode == LocalNotionMode.Online && isCmsPage && parentIsCmsDatabase) {
				var cmsPage = (LocalNotionPage)item;
				var origSlug = cmsPage.CMSProperties.CustomSlug; // CMSHelper.CalculateSlug(cmsPage.CMSProperties.Categories);
				var slugParts = Tools.Url.StripAnchorTag(origSlug.TrimStart("/")).Split('/').Reverse().Skip(1).Reverse().ToArray();


				for(var j = slugParts.Length - 1; j >= 0; j--) {    
					var slug = slugParts.Take(j+1).ToDelimittedString("/");
					traits = BreadCrumbItemTraits.HasUrl;
					var type = LocalNotionResourceType.Page; // what about DB?
					if (Repository.TryFindRenderBySlug(slug, out var slugResult)) {
						// TODO: what about if DB?
						traits.SetFlags(BreadCrumbItemTraits.IsPage);
						traits.SetFlags(BreadCrumbItemTraits.IsCMSPage);
						url = LinkGenerator.Generate(from, slugResult.ResourceID, RenderType.HTML, out resource);
						title = resource.Title;;
						if (resource is LocalNotionPage lnp) {
							data = lnp.Thumbnail.Data;
							switch (lnp.Thumbnail.Type) {
								case ThumbnailType.None:
									break;
								case ThumbnailType.Emoji:
									traits.SetFlags(BreadCrumbItemTraits.HasIcon, true);
									traits.SetFlags(BreadCrumbItemTraits.HasEmojiIcon, true);
									break;
								case ThumbnailType.Image:
									traits.SetFlags(BreadCrumbItemTraits.HasIcon, true);
									traits.SetFlags(BreadCrumbItemTraits.HasImageIcon, true);
									break;
								default:
									throw new ArgumentOutOfRangeException();
							}
						}
					} else {
						traits.SetFlags(j == 0 ? BreadCrumbItemTraits.IsRoot : BreadCrumbItemTraits.IsCategory);
						title = SelectCMSCategory(cmsPage.CMSProperties, j);
						data = string.Empty;
						url = $"/{slug.TrimStart("/")}";
					}
					trail.Add(
						new BreadCrumbItem {
							Type = LocalNotionResourceType.Page,
							Text = title,
							Data = data,
							Traits = traits,
							Url = url
						}
					);
				}

				// Break outer foreach loop
				break;
			} 

			#endregion

		}
		trail.Reverse();
		return new BreadCrumb {
			Trail = trail.ToArray()
		};

		string SelectCMSCategory(CMSProperties cmsProperties, int index)
			=> index switch {
				0 => cmsProperties.Root,
				1 => cmsProperties.Category1,
				2 => cmsProperties.Category2,
				3 => cmsProperties.Category3,
				4 => cmsProperties.Category4,
				5 => cmsProperties.Category5,
			};

		string BuildCompositeSlugPartTitle(string slugPartName, string pageTitle) {
			if (string.IsNullOrEmpty(slugPartName))
				return pageTitle ?? "Untitled";

			if (string.IsNullOrEmpty(pageTitle))
				return slugPartName ?? "Untitled";;

			return $"{slugPartName } ({pageTitle})";
		}
	}

}
