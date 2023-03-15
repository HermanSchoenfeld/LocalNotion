using System.Diagnostics;
using Hydrogen;
using LocalNotion.Core;
using Notion.Client;

namespace LocalNotion.Core;

public static class RendererFactory {

	public static IPageRenderer<string> CreatePageRenderer(LocalNotionPage page, RenderType renderType, RenderMode renderMode, NotionObjectGraph pageGraph, IDictionary<string, IObject> pageObjects, ILocalNotionRepository repository, ILogger logger) {
		switch (renderType) {
			case RenderType.HTML:
				var themeManager = new HtmlThemeManager(repository.Paths, logger);
				var themes = DeterminePageThemes(page, repository);
				var urlGenerator = LinkGeneratorFactory.Create(repository);
				var breadcrumbGenerator = new BreadCrumbGenerator(repository, urlGenerator);
				return new HtmlPageRenderer(renderMode, repository.Paths.Mode, page, pageGraph, pageObjects, repository.Paths, urlGenerator, breadcrumbGenerator, themes.Select(themeManager.LoadTheme).Cast<HtmlThemeInfo>().ToArray());
			case RenderType.PDF:
			case RenderType.File:
			default:
				throw new NotImplementedException(renderType.ToString());
		}
	}


	private static string[] DeterminePageThemes(LocalNotionPage page, ILocalNotionRepository repository) {
		var pageThemes = Enumerable.Empty<string>();
		if (page is { CMSProperties.Themes.Length: > 0 } && page.CMSProperties.Themes.All(theme => Directory.Exists(repository.Paths.GetThemePath(theme, FileSystemPathType.Absolute)))) {
			pageThemes = page.CMSProperties.Themes;
		}
		return repository.DefaultThemes.Concat(pageThemes).ToArray();
	}
}