#pragma warning disable CS8618

using System.Net;
using System.Runtime.CompilerServices;
using System.Security.AccessControl;
using Hydrogen;
using Notion.Client;

namespace LocalNotion;

// Add Scoping to ResourceRepository

public class LocalNotionRenderer {
	private readonly ILocalNotionRepository _repository;

	public LocalNotionRenderer(ILocalNotionRepository repository, ILogger logger = null) {
		Guard.ArgumentNotNull(repository, nameof(repository));

		_repository = repository;
		Logger = logger ?? new NoOpLogger(); ;
	}

	public ILogger Logger { get; set; }

	public string RenderLocalPage(string pageID, PageRenderType renderType, RenderMode renderMode) {
		Guard.ArgumentNotNull(pageID, nameof(pageID));
		var page = _repository.GetPage(pageID);
		var pageGraph = _repository.GetPageGraph(pageID);
		var pageObjects = _repository.FetchObjects(pageGraph);

		// HTML render the page graph
		Logger.Info($"Rendering page '{page.Title}'");
		var renderer = PageRenderFactory.Create(renderType, renderMode, page, pageGraph, pageObjects, _repository, Logger);
		var tmpFile = Tools.FileSystem.GenerateTempFilename(".tmp");
		var output = string.Empty;
		try {
			renderer.Render(tmpFile);
			output = _repository.ImportPageRender(pageID, PageRenderType.HTML, tmpFile);
		} catch (Exception error) {
			Logger.LogException(error);
			// Save exception to rendered file (for html)
			Tools.Exceptions.ExecuteIgnoringException(() => {
				if (renderType == PageRenderType.HTML) {
					File.WriteAllText(tmpFile, error.ToDiagnosticString());
					_repository.ImportPageRender(pageID, PageRenderType.HTML, tmpFile);
				}
			});
		} finally {
			File.Delete(tmpFile);
		}
		return output;
	}


}
