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

	public virtual BreadCrumb CalculateBreadcrumb(LocalNotionResource from) {
		const string DefaultUrl = "#";

		var ancestors = Repository.GetResourceAncestry(from.ID).ToArray();
		if (ancestors.Length == 0)
			return BreadCrumb.Empty;

		var trail = new List<BreadCrumbItem>();

		foreach (var (item, i) in ancestors.WithIndex()) {
			BreadCrumbItemTraits traits = 0;

			if (i == 0)
				traits.SetFlags(BreadCrumbItemTraits.IsCurrentPage, true);

			if (item.Type == LocalNotionResourceType.Page)
				traits.SetFlags(BreadCrumbItemTraits.IsPage, true);

			var isCmsPage = item is LocalNotionPage { CMSProperties: not null };
			if (isCmsPage) {
				traits.SetFlags(BreadCrumbItemTraits.IsCMSPage, true);
			}


			//IsFile			= 1 << 3,
			//IsDatabase		= 1 << 4,
			//IsCategory		= 1 << 5,
			//IsRoot			= 1 << 6,
			//IsWorkspace		= 1 << 7,

			var hasUrl = LinkGenerator.TryGenerate(from, item.ID, RenderType.HTML, out var url, out var resource);
			traits.SetFlags(BreadCrumbItemTraits.HasUrl, hasUrl);
			if (!hasUrl)
				url = DefaultUrl;

			var data = string.Empty;
			if (resource is LocalNotionPage localNotionPage) {
				data = localNotionPage.Thumbnail.Data;
				switch (localNotionPage.Thumbnail.Type) {
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


			//HasIcon			= 1 << 9,

			var breadCrumbItem = new BreadCrumbItem() {
				Type = item.Type,
				Text = item.Title,
				Data = data,
				Traits = traits,
				Url = url
			};

			trail.Add(breadCrumbItem);

			// TODO: when implementing databases, the check is
			//var parentIsCMSDatabase = Repository.TryGetDatabase(item.ParentResourceID, out var database) && LocalNotionCMS.IsCMSDatabase(database);
			var parentIsCMSDatabase = !Repository.ContainsResource(item.ParentResourceID);  // currently CMS database doesn't exist as a resource, but if it was page parent, it would

			#region Process CMS-based slug
			
			// if current item is a CMS item and we're in online mode, the remainder of trail is extracted from the slug
			if (LinkGenerator.Mode == LocalNotionMode.Online && isCmsPage && parentIsCMSDatabase) {
				var cmsPage = (LocalNotionPage)item;
				var slugParts = Tools.Url.StripAnchorTag(cmsPage.CMSProperties.CustomSlug.TrimStart("/")).Split('/');

				for(var j = slugParts.Length - 2; j >= 0; j--) {     // note: j skips tip because only interested in ancestors
					var slug = slugParts.Take(j+1).ToDelimittedString("/");
					traits = BreadCrumbItemTraits.HasUrl;
					LocalNotionResourceType type = LocalNotionResourceType.Page; // what about DB?
					string title = null;
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
	}

}
