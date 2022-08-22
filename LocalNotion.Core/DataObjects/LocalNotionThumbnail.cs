using Newtonsoft.Json;
using Newtonsoft.Json.Converters;

namespace LocalNotion;

public class LocalNotionThumbnail {

	[JsonProperty("type")]
	[JsonConverter(typeof(StringEnumConverter))]
	public ThumbnailType Type { get; set; }

	[JsonProperty("data", NullValueHandling = NullValueHandling.Ignore)]
	public string Data { get; set; }

	public static LocalNotionThumbnail None { get; } = new() { Type = ThumbnailType.None };
}
