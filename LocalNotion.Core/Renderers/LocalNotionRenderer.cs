#pragma warning disable CS8618

using System.Net;
using System.Runtime.CompilerServices;
using System.Security.AccessControl;
using Hydrogen;
using LocalNotion.Core.Repository;
using Notion.Client;

namespace LocalNotion.Core;

public class LocalNotionRenderer : IRenderer {
	private readonly ILocalNotionRepository _repository;

	public LocalNotionRenderer(ILocalNotionRepository repository, ILogger logger = null) {
		Guard.ArgumentNotNull(repository, nameof(repository));

		_repository = repository;
		Logger = logger ?? new NoOpLogger(); ;
	}

	public ILogger Logger { get; set; }


	/// <summary>
	/// Renders a Local Notion resource (page or database).
	/// </summary>
	/// <param name="resourceID">ID of the resource (page or database)</param>
	/// <param name="renderOutput">Type of render to perform</param>
	/// <param name="renderMode">Mode rendering should be performed in</param>
	/// <returns>Filename of rendered file</returns>
	public string RenderLocalResource(string resourceID, RenderOutput renderOutput, RenderMode renderMode) {
		Guard.ArgumentNotNull(resourceID, nameof(resourceID));
		if (!_repository.TryGetObject(resourceID, out var @obj)) 
			throw new ObjectNotFoundException(resourceID); 
		switch(@obj.Value.Object) {
			case ObjectType.Page:
				return RenderLocalPage(resourceID, renderOutput, renderMode);
			case ObjectType.Database:
				return RenderLocalDatabase(resourceID, renderOutput, renderMode);
			default:
				throw new InvalidOperationException($"Unable to render {@obj.Value.Object} '{resourceID}' as it is not a top-level object");
		}
	}

	public string RenderLocalPage(string pageID, RenderOutput renderOutput, RenderMode renderMode) {
		var page = _repository.GetPage(pageID);
		var pageGraph = _repository.GetPageGraph(pageID);
		var pageObjects = _repository.FetchObjects(pageGraph);

		// HTML render the page graph
		Logger.Info($"Rendering page '{page.Title}'");
		var renderer = PageRenderFactory.Create(renderOutput, renderMode, page, pageGraph, pageObjects, _repository, Logger);
		var tmpFile = Tools.FileSystem.GenerateTempFilename(".tmp");
		var output = string.Empty;
		try {
			renderer.Render(tmpFile);
			output = _repository.ImportPageRender(pageID, RenderOutput.HTML, tmpFile);
		} catch (Exception error) {
			Logger.LogException(error);
			// Save exception to rendered file (for html)
			Tools.Exceptions.ExecuteIgnoringException(() => {
				if (renderOutput == RenderOutput.HTML) {
					File.WriteAllText(tmpFile, error.ToDiagnosticString());
					_repository.ImportPageRender(pageID, RenderOutput.HTML, tmpFile);
				}
			});
		} finally {
			File.Delete(tmpFile);
		}
		return output;
	}

	public string RenderLocalDatabase(string databaseID, RenderOutput renderOutput, RenderMode renderMode) {
		throw new NotImplementedException();
	}

}
