namespace LocalNotion.Core;

public class CMSArtifact {

	public CMSPageType Type { get; set; }

	public LocalNotionEditableResource[] Items { get; set; }

	public string Slug { get; set; }

}
