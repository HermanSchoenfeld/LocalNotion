using Hydrogen;
using Notion.Client;

namespace LocalNotion.Core;

public class CMSResourceRenderer : ResourceRenderer {
	private readonly ILocalNotionCMS _localNotionCMS;

	public CMSResourceRenderer(ILocalNotionCMS localNotionCMS, ILocalNotionRepository repository, ILogger logger = null) 
		: base(repository, logger) {
		_localNotionCMS = localNotionCMS;
	}


	protected override IRenderer<string> CreateRenderer(LocalNotionResource resource, RenderType renderType, RenderMode renderMode, ILocalNotionRepository repository, ILogger logger) {
		if (resource is not LocalNotionEditableResource { CMSProperties.CustomSlug: not null } cmsResource) {
			return base.CreateRenderer(resource, renderType, renderMode, repository, logger);
		}
		switch (renderType) {
			case RenderType.HTML:
				var themeManager = new HtmlThemeManager(repository.Paths, logger);
				var urlGenerator = LinkGeneratorFactory.Create(repository);
				var menuPage = (LocalNotionPage)null;
				var footerPage = TryGetFooterPageBySlug(cmsResource.CMSProperties.CustomSlug, out var footer) ? footer : null;
				var breadcrumbGenerator = new BreadCrumbGenerator(repository, urlGenerator);
				return new HtmlRenderer(renderMode, repository, themeManager, urlGenerator, breadcrumbGenerator, logger, menuPage, footerPage);
			case RenderType.PDF:
			case RenderType.File:
			default:
				throw new NotImplementedException(renderType.ToString());
		}
	}

	private string GetPagePath(LocalNotionPage page) {
		if (!page.TryGetRender(RenderType.HTML, out var render))
			throw new InvalidOperationException($"Page '{page.ID}' does not have an HTML render");

		return Path.GetFullPath(render.LocalPath, Repository.Paths.GetRepositoryPath(FileSystemPathType.Absolute));
	}

	private bool TryGetFooterPageBySlug(string slug, out LocalNotionPage footerPage) {
		// slug specific footer
		if (_localNotionCMS.TryGetFooter(slug, out footerPage))
			return true;

		// default footer
		if (_localNotionCMS.TryGetFooter(string.Empty, out footerPage))
			return true;

		// no footer
		footerPage = null;
		return false;
	}

}