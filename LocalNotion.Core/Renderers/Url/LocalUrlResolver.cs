using Hydrogen;

namespace LocalNotion.Core;


/// <summary>
/// Resolves URLs to local resources used in offline scenarios.
/// </summary>
public class LocalUrlResolver : IUrlResolver {

	public LocalUrlResolver(ILocalNotionRepository repository) {
		Repository = repository;
	}

	public ILocalNotionRepository Repository { get; }

	public bool TryResolveLinkToResource(LocalNotionResource from, string toResourceID, RenderType? renderType, out string url, out LocalNotionResource toResource) {
		url = default;

		if (from.ID == toResourceID){
			url = "";
			toResource = from;
			return true;
		}

		var fromPath = Repository.Paths.GetResourceFolderPath(from.Type, from.ID, FileSystemPathType.Absolute);

		if (!Repository.TryGetResource(toResourceID, out toResource))
			return false;

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

		var toResourcePath = Path.GetFullPath(render.LocalPath, Repository.Paths.GetRepositoryPath(FileSystemPathType.Absolute));

		url = Path.GetRelativePath(fromPath, toResourcePath).ToUnixPath();

		return true;
	}

}
