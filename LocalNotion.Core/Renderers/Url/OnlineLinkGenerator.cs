using Hydrogen;

namespace LocalNotion.Core;

/// <summary>
/// Resolves URLs to resources used in online scenarios (webhosting, etc)
/// </summary>
public class OnlineLinkGenerator : LinkGeneratorBase {

	public OnlineLinkGenerator(ILocalNotionRepository repository) : base(repository) {
	}

	public override LocalNotionMode Mode => LocalNotionMode.Online;

	public override bool TryGenerate(LocalNotionResource from, string toResourceID, RenderType? renderType, out string url, out LocalNotionResource toResource) {
		Tools.Debugger.CounterA++;
		url = default;

		if (from.ID == toResourceID) {
			url = "";
			toResource = from;
			return true;
		}

		if (!Repository.TryGetResource(toResourceID, out toResource)) {
			if (!Repository.ContainsObject(toResourceID)) {
				return false;
			} 
			// Try to link to an object, so link to it's parent resource and try to attach anchor
			if (!Repository.TryGetParentResource(toResourceID, out var parentResource))
				return false;
			
			if (!TryGenerate(from, parentResource.ID, renderType, out url, out toResource)) 
				return false;

			if (!url.Contains('#'))
				url += $"#{toResourceID}";
			return true;

		}
			

		if (toResource is LocalNotionPage { CMSProperties: not null } lnp) {
			url = lnp.CMSProperties.CustomSlug;
		} else {
			if (!toResource.TryGetRender(renderType, out var render))
				if (renderType != null && Repository.Paths.UsesObjectIDSubFolders(toResource.Type)) {
					// Here we are trying to resolve a render that is likely to be  rendered later in the processing
					// pipeline.  We only attempt this if Render lives under a object-id folder as otherwise
					// it's filename may clash.  Search for 646870E8-FEDC-45F0-9CF5-B8945C4A2F9E in source code
					// for how this is dealt with when object-id folders are not used.
					var expectedRenderPath = Repository.Paths.CalculateResourceFilePath(toResource.Type, toResourceID, toResource.Title, renderType.Value, FileSystemPathType.Relative);
					render = new RenderEntry {
						LocalPath = expectedRenderPath,
						Slug = Repository.CalculateRenderSlug(toResource, renderType.Value, expectedRenderPath)
					};
				} else return false;
			url = render.Slug;
		}

		url = $"{Repository.Paths.GetRemoteHostedBaseUrl().TrimEnd("/")}/{url.TrimStart("/")}";

		return true;
	}


}
