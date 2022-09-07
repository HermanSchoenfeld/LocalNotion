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

		if (!toResource.TryGetRender(out var render, renderType))
			return false;

		var toResourcePath = Path.GetFullPath(render.LocalPath, Repository.Paths.GetRepositoryPath(FileSystemPathType.Absolute));

		url = Path.GetRelativePath(fromPath, toResourcePath).ToUnixPath();

		return true;
	}

}
