using Notion.Client;

namespace LocalNotion.Core;

public interface IRenderer<TOutput> {
	TOutput Render(LocalNotionEditableResource page);
}