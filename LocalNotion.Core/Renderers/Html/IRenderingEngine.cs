using Notion.Client;

namespace LocalNotion.Core;

public interface IRenderingEngine<TOutput> {
	TOutput RenderPage(LocalNotionPage page, NotionObjectGraph pageGraph, IDictionary<string, IObject> pageObjects, ThemeInfo[] themes);
}