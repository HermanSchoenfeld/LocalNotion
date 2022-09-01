using Hydrogen;
using Notion.Client;

namespace LocalNotion.Core;

public class ResourceRenderer : IResourceRenderer {
	private readonly ILocalNotionRepository _repository;

	public ResourceRenderer(ILocalNotionRepository repository, ILogger logger = null) {
		Guard.ArgumentNotNull(repository, nameof(repository));

		_repository = repository;
		Logger = logger ?? new NoOpLogger(); ;
	}

	public ILogger Logger { get; set; }


	/// <summary>
	/// Renders a Local Notion resource (page or database).
	/// </summary>
	/// <param name="resourceID">ID of the resource (page or database)</param>
	/// <param name="renderType">Type of render to perform</param>
	/// <param name="renderMode">Mode rendering should be performed in</param>
	/// <returns>Filename of rendered file</returns>
	public string RenderLocalResource(string resourceID, RenderType renderType, RenderMode renderMode) {
		Guard.ArgumentNotNull(resourceID, nameof(resourceID));
		if (!_repository.TryGetObject(resourceID, out var @obj)) 
			throw new ObjectNotFoundException(resourceID); 
		switch(@obj.Value.Object) {
			case ObjectType.Page:
				return RenderLocalPage(resourceID, renderType, renderMode);
			case ObjectType.Database:
				return RenderLocalDatabase(resourceID, renderType, renderMode);
			default:
				throw new InvalidOperationException($"Unable to render {@obj.Value.Object} '{resourceID}' as it is not a top-level object");
		}
	}

	public string RenderLocalPage(string pageID, RenderType renderType, RenderMode renderMode) {
		var page = (LocalNotionPage) _repository.GetResource(pageID);
		var pageGraph = _repository.GetPageGraph(pageID);
		var pageObjects = _repository.FetchObjects(pageGraph);

		// HTML render the page graph
		Logger.Info($"Rendering page '{page.Title}'");
		var renderer = PageRenderFactory.Create(page, renderType, renderMode,  pageGraph, pageObjects, _repository, Logger);
		var tmpFile = Tools.FileSystem.GenerateTempFilename(".tmp");
		var output = string.Empty;
		try {
			renderer.Render(tmpFile);
			output = _repository.ImportResourceRender(pageID, RenderType.HTML, tmpFile);
		} catch (Exception error) {
			Logger.LogException(error);
			// Save exception to rendered file (for html)
			Tools.Exceptions.ExecuteIgnoringException(() => {
				if (renderType == RenderType.HTML) {
					File.WriteAllText(tmpFile, error.ToDiagnosticString());
					_repository.ImportResourceRender(pageID, RenderType.HTML, tmpFile);
				}
			});
		} finally {
			File.Delete(tmpFile);
		}
		return output;
	}

	public string RenderLocalDatabase(string databaseID, RenderType renderType, RenderMode renderMode) {
		throw new NotImplementedException();
	}

}
