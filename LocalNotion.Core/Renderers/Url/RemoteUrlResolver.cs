using Hydrogen;

namespace LocalNotion.Core;

/// <summary>
/// Resolves URLs to resources used in online scenarios (webhosting, etc)
/// </summary>
public class RemoteUrlResolver : IUrlResolver {

	public RemoteUrlResolver(ILocalNotionRepository repository) {
		Repository = repository;
	}

	public ILocalNotionRepository Repository { get; }

	public bool TryResolveLinkToResource(LocalNotionResourceType fromResourceType, string fromResourceID, string toResourceID, RenderType? renderType, out string url, out LocalNotionResource toResource) {
		url = null;
		if (!Repository.TryGetResource(toResourceID, out toResource))
			return false;

		if (toResource is LocalNotionPage { CMSProperties: not null } lnp) {
			url = lnp.CMSProperties.Slug;
		} else {
			if (!toResource.TryGetRender(out var render, renderType))
				return false;
			url = render.Slug;
		}

		url = $"{Repository.Paths.GetRemoteHostedBaseUrl()}/{url.TrimStart("/")}";

		return true;
	}


}
