using AngleSharp.Media.Dom;
using Hydrogen;
using Notion.Client;

namespace LocalNotion.Core;

public class RenderingManager  {

	public RenderingManager(ILocalNotionRepository repository, ILogger logger = null) {
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
				// Get the resource 
				var tmpFile = Tools.FileSystem.GenerateTempFilename(".tmp");
				string output;
				try {
					
					// Get resource and it's visual graph + objcets for rendering
					var resource = (LocalNotionEditableResource)Repository.GetResource(resourceID);
					var visualGraph = Repository.GetEditableResourceGraph(resource.ID);
					var visualObjects = Repository.LoadObjects(visualGraph);

					// Activate the html renderer
					var themeManager = new HtmlThemeManager(Repository.Paths, Logger);
					var urlGenerator = LinkGeneratorFactory.Create(Repository);
					var breadcrumbGenerator = new BreadCrumbGenerator(Repository, urlGenerator);
					var renderer = new HtmlRenderer(renderMode, Repository, themeManager, urlGenerator, breadcrumbGenerator, Logger);

					// Render HTML to a text file
					var html = renderer.Render(resource, visualGraph, visualObjects, Repository.Paths.GetResourceFolderPath(LocalNotionResourceType.Page, resource.ID, FileSystemPathType.Absolute));
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

	public void RenderCMSItem(CMSItem cmsItem) {
		
		if (Repository is not CMSLocalNotionRepository cmsRepo)
			throw new InvalidOperationException("Unable to a CMS item as the repository is not a CMS-based repository");

		// Activate the html renderer
		var themeManager = new HtmlThemeManager(Repository.Paths, Logger);
		var urlGenerator = LinkGeneratorFactory.Create(Repository);
		var breadcrumbGenerator = new BreadCrumbGenerator(Repository, urlGenerator);
		var cmsRenderer = new CmsHtmlRenderer(RenderMode.ReadOnly, cmsRepo, themeManager, urlGenerator, breadcrumbGenerator, Logger);

		// load framing 
		cmsRenderer.HeaderHtmlSection = !cmsItem.HeaderID.IsNullOrWhiteSpace() ? cmsRenderer.RenderCMSPageItem(cmsItem.HeaderID, CMSPageType.Header) : string.Empty;
		cmsRenderer.NavBarHtmlSection = !cmsItem.MenuID.IsNullOrWhiteSpace() ? cmsRenderer.RenderCMSPageItem(cmsItem.MenuID, CMSPageType.Menu) : string.Empty;
		cmsRenderer.FooterHtmlSection = !cmsItem.FooterID.IsNullOrWhiteSpace() ? cmsRenderer.RenderCMSPageItem(cmsItem.FooterID, CMSPageType.Footer) : string.Empty;
		// TODO: need to set output path for links to be from /cms not /pages
		DetermineCMSItemFilename(cmsItem, out var filePath);
		Logger.Info($"Rendering CMS item '{cmsItem.RenderFileName}'");
		string htmlContent;
		switch (cmsItem.ItemType) {
			case CMSItemType.Page:
				// render original page with framing
				Guard.Ensure(cmsItem.Parts.Length == 1, "Page CMSItem must have exactly one part");
				htmlContent = cmsRenderer.RenderCMSPageItem(cmsItem.Parts[0], CMSPageType.Page);
				break;
			case CMSItemType.SectionedPage:
				// render each section as a partial page
				htmlContent = cmsRenderer.RenderSectionedPage(cmsItem.Parts);
				break;
			case CMSItemType.ArticleCategory:
				htmlContent = cmsRenderer.RenderArticlesPage(cmsItem.Slug);
				break;
			case CMSItemType.GalleryPage:
				htmlContent = "NOT IMPLEMENTED";
				break;
			default:
				throw new ArgumentOutOfRangeException();
		}
		htmlContent = HtmlRenderer.Format(htmlContent);
		File.WriteAllText(filePath, htmlContent);
		cmsItem.Dirty = false;
		
		void DetermineCMSItemFilename(CMSItem cmsItem, out string absoluteFilePath) {
			var desiredFilePath = Repository.Paths.CalculateResourceFilePath(LocalNotionResourceType.CMS, cmsItem.Slug, cmsItem.Title, RenderType.HTML, FileSystemPathType.Absolute);
			var desiredRelativeFilePath = Tools.FileSystem.GetRelativePath(Repository.Paths.GetRepositoryPath(FileSystemPathType.Absolute), desiredFilePath);
			var currentIdealRelativeFilePath = Repository.Paths.RemoveConflictResolutionFromFilePath(cmsItem.RenderFileName ?? string.Empty);
			if (currentIdealRelativeFilePath == desiredRelativeFilePath) {
				absoluteFilePath = Path.Join(Repository.Paths.GetRepositoryPath(FileSystemPathType.Absolute), cmsItem.RenderFileName);
			} else {
				absoluteFilePath = Repository.Paths.ResolveConflictingFilePath(desiredFilePath);
				var currentRenderFilePath = Path.Join(Repository.Paths.GetRepositoryPath(FileSystemPathType.Absolute), cmsItem.RenderFileName);
				if (File.Exists(currentRenderFilePath)) {
					Logger.Info($"Deleting CMS item '{cmsItem.RenderFileName}'");
					File.Delete(currentRenderFilePath);
				}
			}
			var renderFileName = Tools.FileSystem.GetRelativePath(Repository.Paths.GetRepositoryPath(FileSystemPathType.Absolute), absoluteFilePath);
			cmsItem.RenderFileName = renderFileName;	
		}
	}

	private string GetResourceHtmlRenderContents(string resourceID) {
		var resource = Repository.GetResource(resourceID);
		var render = resource.GetRender(RenderType.HTML);
		var fullPath = Path.GetFullPath(render.LocalPath, Repository.Paths.GetRepositoryPath(FileSystemPathType.Absolute));
		return File.ReadAllText(fullPath);
	}

}

