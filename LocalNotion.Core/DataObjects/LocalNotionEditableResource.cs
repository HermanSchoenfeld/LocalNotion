using Newtonsoft.Json;

namespace LocalNotion.Core;

public abstract class LocalNotionEditableResource : LocalNotionResource {

	[JsonProperty("name", NullValueHandling = NullValueHandling.Ignore)]
	public string Name { get; set; }

	[JsonProperty("last_edited_time")]
	public DateTime LastEditedTime { get; set; }
}
