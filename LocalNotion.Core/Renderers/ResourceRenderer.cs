using Hydrogen;
using Notion.Client;

namespace LocalNotion.Core;

public class ResourceRenderer : IResourceRenderer {

	public ResourceRenderer(ILocalNotionRepository repository, ILogger logger = null) {
		Guard.ArgumentNotNull(repository, nameof(repository));

		Repository = repository;
		Logger = logger ?? new NoOpLogger(); ;
	}

	protected ILocalNotionRepository Repository { get; }

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
		if (!Repository.TryGetObject(resourceID, out var @obj))
			throw new ObjectNotFoundException(resourceID);
		switch (@obj.Object) {
			case ObjectType.Page:
			case ObjectType.Database:
				var resource = (LocalNotionEditableResource)Repository.GetResource(resourceID);
				var renderer = CreateRenderer(resource, renderType, renderMode, Repository, Logger);
				var tmpFile = Tools.FileSystem.GenerateTempFilename(".tmp");
				string output;
				try {
					var html = renderer.Render(resource);
					File.WriteAllText(tmpFile, html);
					output = Repository.ImportResourceRender(resourceID, RenderType.HTML, tmpFile);
				} catch (TaskCanceledException) {
					throw;
				} catch (Exception error) {
					// Save exception to rendered file (for html)
					Tools.Exceptions.ExecuteIgnoringException(() => {
						if (renderType == RenderType.HTML) {
							File.WriteAllText(tmpFile, error.ToDiagnosticString());
							Repository.ImportResourceRender(resourceID, RenderType.HTML, tmpFile);
						}
					});
					throw;
				} finally {
					File.Delete(tmpFile);
				}

				return output;


			default:
				throw new InvalidOperationException($"Unable to render {@obj.Object} '{resourceID}' as it is not a top-level object");
		}
	}


	protected virtual IRenderer<string> CreateRenderer(LocalNotionResource resource, RenderType renderType, RenderMode renderMode, ILocalNotionRepository repository, ILogger logger) {
		switch (renderType) {
			case RenderType.HTML:
				var themeManager = new HtmlThemeManager(repository.Paths, logger);
				var urlGenerator = LinkGeneratorFactory.Create(repository);
				var breadcrumbGenerator = new BreadCrumbGenerator(repository, urlGenerator);
				return new HtmlRenderer(renderMode, repository, themeManager, urlGenerator, breadcrumbGenerator, logger);
			case RenderType.PDF:
			case RenderType.File:
			default:
				throw new NotImplementedException(renderType.ToString());
		}
	}

}

