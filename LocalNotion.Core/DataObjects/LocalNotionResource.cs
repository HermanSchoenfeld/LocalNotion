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

	[JsonProperty("renders", NullValueHandling = NullValueHandling.Ignore)]
	public IDictionary<RenderType, RenderEntry> Renders { get; set; } = new Dictionary<RenderType, RenderEntry>();

	public bool TryGetRender(out RenderEntry render, RenderType? renderType = null) {
		render = null;

		// No best match render found
		if (renderType == null) {
			if (Renders.Count <= 0)
				return false;
		
			// Get best match
			render = Renders.MinBy(x => x.Key).Value;
			return true;
		}

		// Specifically requested render not found
		if (!Renders.TryGetValue(renderType.Value, out render)) 
			return false;

		return true;

	}
}


public class RenderEntry {

	[JsonProperty("local_path")]
	public string LocalPath { get; set; }

	[JsonProperty("slug")]
	public string Slug { get; set; }

}
