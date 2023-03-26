using System.Diagnostics;
using Hydrogen;
using LocalNotion.Core;
using Notion.Client;

namespace LocalNotion.Core;

public static class RendererFactory {

	public static IRenderer<string> CreateRenderer(RenderType renderType, RenderMode renderMode, ILocalNotionRepository repository, ILogger logger) {
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