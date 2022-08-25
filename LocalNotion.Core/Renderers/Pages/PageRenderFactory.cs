using System.Diagnostics;
using Hydrogen;
using Notion.Client;

namespace LocalNotion.Core;

public static class PageRenderFactory {

	public static IPageRenderer Create(RenderOutput rendererOutput, RenderMode renderMode, LocalNotionPage page, NotionObjectGraph pageGraph, IDictionary<string, IObject> pageObjects, ILocalNotionRepository repository, ILogger logger) {
		switch(rendererOutput){
			case RenderOutput.HTML: 
				var templateManager = new HtmlTemplateManager(repository.TemplatesPath, logger);
				var template = page is LocalNotionPage { CMSProperties: not null } cmsPage && !string.IsNullOrWhiteSpace(cmsPage.CMSProperties.Root) && repository.RootTemplates.TryGetValue(cmsPage.CMSProperties.Root, out var rootTemplate) ?
					rootTemplate :
					repository.DefaultTemplate;
				return new HtmlPageRenderer(renderMode, repository.Mode, page, pageGraph, pageObjects, repository.CreateUrlResolver(), templateManager, template);
			case RenderOutput.PDF:
				throw new NotImplementedException();
			default:
				throw new ArgumentOutOfRangeException(nameof(rendererOutput), rendererOutput, null);
		}
	}
}