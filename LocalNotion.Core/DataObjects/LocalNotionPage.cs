using Hydrogen;
using Newtonsoft.Json;
using Notion.Client;

namespace LocalNotion.Core;

public class LocalNotionPage : LocalNotionEditableResource {

	public override LocalNotionResourceType Type => LocalNotionResourceType.Page;

	[JsonProperty("cover", NullValueHandling = NullValueHandling.Ignore)]
	public string Cover { get; set; }

	[JsonProperty("thumbnail")]
	public LocalNotionThumbnail Thumbnail { get; set; } = LocalNotionThumbnail.None;
	
	[JsonProperty("cms", NullValueHandling = NullValueHandling.Ignore)]
	public CMSProperties CMSProperties { get; set; } = null;

}