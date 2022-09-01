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
		if (renderType != null && !Renders.TryGetValue(renderType.Value, out render)) {
			return false;
		} else if (Renders.Count > 0) {
			// Client didn't specify render, select first render 
			render = Renders.OrderBy(x => x.Key).First().Value;
		} else return false;
		return false;
	}
}


public class RenderEntry {

	[JsonProperty("local_path")]
	public string LocalPath { get; set; }

	[JsonProperty("slug")]
	public string Slug { get; set; }

}
