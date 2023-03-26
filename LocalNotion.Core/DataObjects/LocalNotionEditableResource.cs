using Newtonsoft.Json;
using Notion.Client;

namespace LocalNotion.Core;

public abstract class LocalNotionEditableResource : LocalNotionResource {

	[JsonProperty("cover", NullValueHandling = NullValueHandling.Ignore)]
	public string Cover { get; set; }

	[JsonProperty("thumbnail")]
	public LocalNotionThumbnail Thumbnail { get; set; } = LocalNotionThumbnail.None;

	[JsonProperty("name", NullValueHandling = NullValueHandling.Ignore)]
	public string Name { get; set; }

	[JsonProperty("cms", NullValueHandling = NullValueHandling.Ignore)]
	public CMSProperties CMSProperties { get; set; } = null;

	[JsonProperty("created_on")]
	public DateTime CreatedOn { get; set; }

	[JsonProperty("last_edited_on")]
	public DateTime LastEditedOn { get; set; }

}
