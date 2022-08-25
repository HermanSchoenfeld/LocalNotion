using JsonSubTypes;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;

namespace LocalNotion.Core;

[JsonConverter(typeof(JsonSubtypes), "type")]
[JsonSubtypes.KnownSubType(typeof(LocalNotionFile), LocalNotionResourceType.File)]
[JsonSubtypes.KnownSubType(typeof(LocalNotionPage), LocalNotionResourceType.Page)]
public abstract class LocalNotionResource {

	[JsonProperty("type")]
	[JsonConverter(typeof(StringEnumConverter))]
	public abstract LocalNotionResourceType Type { get; }

	[JsonProperty("id")]
	public string ID { get; set; }

	[JsonProperty("title")]
	public string Title { get; set; }

	[JsonProperty("local_path")]
	public string? LocalPath { get; set; }
	
	[JsonProperty("default_slug")]
	public string DefaultSlug { get; set; }

}
