namespace LocalNotion.Core;

public class BreadCrumbItem {

	public LocalNotionResourceType Type { get; set; }

	public BreadCrumbItemTraits Traits { get; set; }

	public string Data { get; set; }

	public string Text { get; set; }

	public string Url { get; set; }
}
