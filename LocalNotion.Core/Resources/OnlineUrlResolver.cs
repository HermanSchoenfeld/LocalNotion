using Hydrogen;

namespace LocalNotion;

public class OnlineUrlResolver : IUrlResolver {
	public const string DefaultLinkTemplate = "{slug}";
	
	public OnlineUrlResolver(LocalNotionRepository repository)
		: this(repository, DefaultLinkTemplate){
	}

	public OnlineUrlResolver(ILocalNotionRepository repository, string linkTemplate) {
		Repository = repository;
		LinkTemplate = linkTemplate;
	}

	public ILocalNotionRepository Repository { get; }

	public string LinkTemplate { get; }

	public bool TryResolve(string resourceID, out string resourceUrl, out LocalNotionResource resource) {
		if (!Repository.TryGetResource(resourceID, out resource)) {
			resourceUrl = null;
			return false;
		}
		var cmsSlug = resource is LocalNotionPage { CMSProperties: not null } lnp ? lnp.CMSProperties.Slug : null;
	
		resourceUrl = StringFormatter.FormatWithDictionary(LinkTemplate, new Dictionary<string, object> { ["slug"] = cmsSlug ?? resource.DefaultSlug }, true);
		return true;
	}

	public string GetRelativePathFromPage(string fullPath) {
		throw new NotSupportedException();
	}


}
