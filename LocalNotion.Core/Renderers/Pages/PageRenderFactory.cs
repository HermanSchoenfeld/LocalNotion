using System.Diagnostics;
using Hydrogen;
using Notion.Client;

namespace LocalNotion.Core;

public static class PageRenderFactory {

	public static IPageRenderer Create(LocalNotionPage page, RenderType renderType, RenderMode renderMode, NotionObjectGraph pageGraph, IDictionary<string, IObject> pageObjects, ILocalNotionRepository repository, ILogger logger) {
		switch(renderType){
			case RenderType.HTML: 
				var themeManager = new HtmlThemeManager(repository.Paths, logger);
				var template = page is { CMSProperties: not null } && !string.IsNullOrWhiteSpace(page.CMSProperties.Root) && repository.ThemeMaps.TryGetValue(page.CMSProperties.Root, out var rootTemplate) ?
					rootTemplate :
					repository.DefaultTemplate;
				var urlGenerator = LinkGeneratorFactory.Create(repository);
				var breadcrumbGenerator = new BreadCrumbGenerator(repository, urlGenerator);
				return new HtmlPageRenderer(renderMode, repository.Paths.Mode, page, pageGraph, pageObjects, repository.Paths, urlGenerator, breadcrumbGenerator, themeManager, template);
			case RenderType.PDF:
			case RenderType.File:
			default:
				throw new NotImplementedException(renderType.ToString());
		}
	}
}