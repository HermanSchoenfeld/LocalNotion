using Notion.Client;

namespace LocalNotion;

public interface IPageRenderer {
	void Render(string destinationFile);

}