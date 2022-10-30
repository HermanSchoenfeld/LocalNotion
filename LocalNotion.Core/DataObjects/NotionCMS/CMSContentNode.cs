namespace LocalNotion.Core;

public class CMSContentNode {

	public string Title { get; set; }

	public string Slug { get; set; }

	public List<LocalNotionPage> Content { get; } = new List<LocalNotionPage>();

	public CMSContentNode Parent { get; set; }

	public List<CMSContentNode> Children { get; } = new();

}
