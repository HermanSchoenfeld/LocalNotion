using Notion.Client;

namespace LocalNotion.Core;

public interface IPageRenderer {
	void Render(string destinationFile);

}