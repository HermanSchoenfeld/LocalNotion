using System.Diagnostics;
using Hydrogen;
using Notion.Client;

namespace LocalNotion.Core;

public static class PageRenderFactory {

	public static IPageRenderer Create(LocalNotionPage page, RenderType renderType, RenderMode renderMode, NotionObjectGraph pageGraph, IDictionary<string, IObject> pageObjects, ILocalNotionRepository repository, ILogger logger) {
		switch(renderType){
			case RenderType.HTML: 
				var themeManager = new HtmlThemeManager(repository.Paths, logger);
				var theme = page is { CMSProperties.Theme: not null } && Directory.Exists(repository.Paths.GetThemePath(page.CMSProperties.Theme, FileSystemPathType.Absolute)) ? page.CMSProperties.Theme : repository.DefaultTheme;
				var urlGenerator = LinkGeneratorFactory.Create(repository);
				var breadcrumbGenerator = new BreadCrumbGenerator(repository, urlGenerator);
				return new HtmlPageRenderer(renderMode, repository.Paths.Mode, page, pageGraph, pageObjects, repository.Paths, urlGenerator, breadcrumbGenerator, themeManager, theme);
			case RenderType.PDF:
			case RenderType.File:
			default:
				throw new NotImplementedException(renderType.ToString());
		}
	}
}