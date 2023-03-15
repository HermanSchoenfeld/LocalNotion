using Notion.Client;

namespace LocalNotion.Core;

public interface IPageRenderer<TOutput> {
	TOutput Render();
}