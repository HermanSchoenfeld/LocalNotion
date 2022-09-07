using Hydrogen;

namespace LocalNotion.Core;

public class BreadCrumbGenerator : IBreadCrumbGenerator {

	public BreadCrumbGenerator(ILocalNotionRepository repository, IUrlResolver urlResolver) {
		Guard.ArgumentNotNull(repository, nameof(repository));
		Guard.ArgumentNotNull(urlResolver, nameof(urlResolver));
		Repository = repository;
		UrlResolver = urlResolver;
	}

	protected ILocalNotionRepository Repository { get; }

	protected IUrlResolver UrlResolver { get; }

	public virtual BreadCrumb CalculateBreadcrumb(LocalNotionResource from) {
		const string DefaultUrl = "#";

		var ancestors = Repository.GetResourceAncestry(from.ID).TakeUntilInclusive(x => x is LocalNotionPage { CMSProperties: not null }).ToArray();
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

			var hasUrl = UrlResolver.TryResolveLinkToResource(from, item.ID, RenderType.HTML, out var url, out var resource);
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

			// if current item is a CMS item, the remainder of trail is extracted from he slug
			if (isCmsPage) {
				var cmsPage = (LocalNotionPage)item;
				var slugParts = cmsPage.CMSProperties.CustomSlug.Split('/');

				foreach (var slugAncestor in slugParts.Reverse().WithDescriptions()) {
					if (slugAncestor.Index == 0)
						continue; ;

					traits = BreadCrumbItemTraits.HasUrl;

					if (slugAncestor.Description.HasFlag(EnumeratedItemDescription.Last))
						traits.SetFlags(BreadCrumbItemTraits.IsRoot, true);
					else
						traits.SetFlags(BreadCrumbItemTraits.IsCategory, true);

					url = slugParts.Take(slugParts.Length - slugAncestor.Index).ToDelimittedString("/");

					trail.Add(new BreadCrumbItem {
						Type = LocalNotionResourceType.Page,
						Text = SelectCMSCategory(cmsPage.CMSProperties, slugParts.Length - slugAncestor.Index - 1),
						Data = string.Empty,
						Traits = traits,
						Url = url

					});
				}
				break;
			}
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
