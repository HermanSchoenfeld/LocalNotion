using System.Diagnostics;
using Hydrogen;
using LocalNotion.Core;
using Notion.Client;

namespace LocalNotion.Core;

public static class RendererFactory {

	public static IRenderingEngine<string> CreatePageRenderer(RenderType renderType, RenderMode renderMode, ILocalNotionRepository repository, ILogger logger) {
		switch (renderType) {
			case RenderType.HTML:
				var themeManager = new HtmlThemeManager(repository.Paths, logger);
				var urlGenerator = LinkGeneratorFactory.Create(repository);
				var breadcrumbGenerator = new BreadCrumbGenerator(repository, urlGenerator);
				return new HtmlRenderingEngine(renderMode, repository.Paths.Mode, repository.Paths, urlGenerator, breadcrumbGenerator, themeManager);
			case RenderType.PDF:
			case RenderType.File:
			default:
				throw new NotImplementedException(renderType.ToString());
		}
	}

	
	public static IRenderingEngine<string> CreateDatabaseRenderer(LocalNotionDatabase database, RenderType renderType, RenderMode renderMode, NotionObjectGraph pageGraph, IDictionary<string, IObject> pageObjects, string[] themes, ILocalNotionRepository repository, ILogger logger) {
		throw new NotImplementedException();
		//switch (renderType) {
		//	case RenderType.HTML:
		//		var themeManager = new HtmlThemeManager(repository.Paths, logger);
		//		themes ??= new [] { "default" };
		//		var urlGenerator = LinkGeneratorFactory.Create(repository);
		//		var breadcrumbGenerator = new BreadCrumbGenerator(repository, urlGenerator);
		//		return new HtmlDatabaseRenderer(renderMode, repository.Paths.Mode, page, pageGraph, pageObjects, repository.Paths, urlGenerator, breadcrumbGenerator, themes.Select(themeManager.LoadTheme).Cast<HtmlThemeInfo>().ToArray());
		//	case RenderType.PDF:
		//	case RenderType.File:
		//	default:
		//		throw new NotImplementedException(renderType.ToString());
		//}
	}

}