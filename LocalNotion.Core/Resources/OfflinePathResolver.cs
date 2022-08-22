using Hydrogen;

namespace LocalNotion;

public class OfflinePathResolver : IUrlResolver {
	
	public OfflinePathResolver(ILocalNotionRepository repository) {
		Repository = repository;
	}

	public ILocalNotionRepository Repository { get; }

	public bool TryResolve(string resourceID, out string resourceUrl, out LocalNotionResource resource) {
		resourceUrl = null;

		if (!Repository.TryGetResource(resourceID, out resource))
			return false;
		
		// TODO: needs to calculate based on known output folder as this makes assumptions
		// about pages subfolder (i.e. "/pages/")

		if (resource is LocalNotionPage localNotionPage) {
			if (!localNotionPage.Renders.TryGetValue(PageRenderType.HTML, out var file)) {
				// try already rendered HTML
				file = Repository.CalculatePageRenderFilename(resource.ID, PageRenderType.HTML); // default to future-rendered HTML
				//file = Path.GetRelativePath(Repository.PagesPath, file);
			}

			resourceUrl = $"../{resource.LocalPath}/{file}";
		} else {
			resourceUrl = $"../{Path.GetRelativePath(Repository.PagesPath, Repository.FilesPath)}/{resource.LocalPath}";
		}

		resourceUrl = resourceUrl.ToUnixPath();
		
		return true;
	}

	
}
