using System.Diagnostics;
using Hydrogen;
using Notion.Client;

namespace LocalNotion;

public static class PageRenderFactory {

	public static IPageRenderer Create(PageRenderType rendererType, RenderMode renderMode, LocalNotionPage page, NotionObjectGraph pageGraph, IDictionary<string, IObject> pageObjects, ILocalNotionRepository repository, ILogger logger) {
		switch(rendererType){
			case PageRenderType.HTML: 
				var templateManager = new HtmlTemplateManager(repository.TemplatesPath, logger);
				var template = page is LocalNotionPage { CMSProperties: not null } cmsPage && !string.IsNullOrWhiteSpace(cmsPage.CMSProperties.Root) && repository.RootTemplates.TryGetValue(cmsPage.CMSProperties.Root, out var rootTemplate) ?
					rootTemplate :
					repository.DefaultTemplate;
				return new HtmlRenderer(renderMode, repository.Mode, page, pageGraph, pageObjects, repository.CreateUrlResolver(), templateManager, template);
			case PageRenderType.PDF:
				throw new NotImplementedException();
			default:
				throw new ArgumentOutOfRangeException(nameof(rendererType), rendererType, null);
		}
	}
}