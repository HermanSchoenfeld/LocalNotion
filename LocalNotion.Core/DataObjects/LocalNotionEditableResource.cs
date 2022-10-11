using Newtonsoft.Json;

namespace LocalNotion.Core;

public abstract class LocalNotionEditableResource : LocalNotionResource {

	[JsonProperty("sequence", NullValueHandling = NullValueHandling.Ignore)]
	public int? Sequence { get; set; }

	[JsonProperty("last_edited_time")]
	public DateTime LastEditedTime { get; set; }
}
