using Hydrogen;
using Newtonsoft.Json;
using Notion.Client;

namespace LocalNotion.Core;

public class LocalNotionDatabase : LocalNotionEditableResource {

	public override LocalNotionResourceType Type => LocalNotionResourceType.Database;

	[JsonProperty("cover", NullValueHandling = NullValueHandling.Ignore)]
	public string Cover { get; set; }

	[JsonProperty("thumbnail")]
	public LocalNotionThumbnail Thumbnail { get; set; } = LocalNotionThumbnail.None;

	[JsonProperty("description")]
	public string Description { get; set; }

	[JsonProperty("properties")]
	public Dictionary<string, Property> Properties { get; set; }
	
}