using Hydrogen;

namespace LocalNotion.Core;


public abstract class UrlResolverBase : IUrlResolver {

	protected UrlResolverBase(ILocalNotionRepository repository) {
		Repository = repository;
	}

	public ILocalNotionRepository Repository { get; }

	public bool TryGetResourceFromUrl(string url, out LocalNotionResource resource, out RenderEntry entry) {
		resource = default;
		entry = default;

		if (!Repository.TryFindRenderBySlug(url, out var resourceID, out var renderType))
			return false;
		
		if (!Repository.TryGetResource(resourceID, out resource))
			return false;

		if (!resource.TryGetRender(renderType, out entry))
			return false;
		
		return true;
			
	}

	public abstract bool TryResolveLinkToResource(LocalNotionResource from, string toResourceID, RenderType? renderType, out string url, out LocalNotionResource toResource);

}
