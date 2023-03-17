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
		switch(@obj.Object) {
			case ObjectType.Page:
				return RenderLocalPage(resourceID, renderType, renderMode);
			case ObjectType.Database:
				return RenderLocalDatabase(resourceID, renderType, renderMode);
			default:
				throw new InvalidOperationException($"Unable to render {@obj.Object} '{resourceID}' as it is not a top-level object");
		}
	}

	public string RenderLocalPage(string pageID, RenderType renderType, RenderMode renderMode) {
		var page = (LocalNotionPage) _repository.GetResource(pageID);
		var pageGraph = _repository.GetPageGraph(pageID);
		var pageObjects = _repository.LoadObjects(pageGraph);

		// HTML render the page graph
		Logger.Info($"Rendering page '{page.Title}'");
		var renderer = RendererFactory.CreatePageRenderer(renderType, renderMode, _repository, Logger);
		var tmpFile = Tools.FileSystem.GenerateTempFilename(".tmp");
		var output = string.Empty;
		try {
			var themes = CalculatePageThemes(page, _repository);
			var themeManager = new HtmlThemeManager(_repository.Paths, Logger);
			var htmlThemes = themes.Select(themeManager.LoadTheme).Cast<HtmlThemeInfo>().ToArray();
			var html = renderer.RenderPage(page, pageGraph, pageObjects, htmlThemes);
			File.WriteAllText(tmpFile, html);
			output = _repository.ImportResourceRender(pageID, RenderType.HTML, tmpFile);
		} catch (TaskCanceledException) {
			throw;
		} catch (Exception error) {
			// Save exception to rendered file (for html)
			Tools.Exceptions.ExecuteIgnoringException(() => {
				if (renderType == RenderType.HTML) {
					File.WriteAllText(tmpFile, error.ToDiagnosticString());
					_repository.ImportResourceRender(pageID, RenderType.HTML, tmpFile);
				}
			});
			throw;
		} finally {
			File.Delete(tmpFile);
		}
		return output;
	}

	public string RenderLocalDatabase(string databaseID, RenderType renderType, RenderMode renderMode) {
		throw new NotImplementedException();
		//var database = (LocalNotionDatabase) _repository.GetResource(databaseID);
		//Logger.Info($"Rendering database '{database.Title}'");
		//var renderer = RendererFactory.C
	}


	public static string[] CalculatePageThemes(LocalNotionPage page, ILocalNotionRepository repository) {
		var pageThemes = Enumerable.Empty<string>();
		if (page is { CMSProperties.Themes.Length: > 0 } && page.CMSProperties.Themes.All(theme => Directory.Exists(repository.Paths.GetThemePath(theme, FileSystemPathType.Absolute)))) {
			pageThemes = page.CMSProperties.Themes;
		}
		return repository.DefaultThemes.Concat(pageThemes).ToArray();
	}
}
