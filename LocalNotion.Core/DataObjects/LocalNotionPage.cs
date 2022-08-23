using System.Dynamic;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;

namespace LocalNotion;

public class LocalNotionPage : LocalNotionResource {

	public override LocalNotionResourceType Type => LocalNotionResourceType.Page;

	[JsonProperty("cover", NullValueHandling = NullValueHandling.Ignore)]
	public string Cover { get; set; }

	[JsonProperty("thumbnail")]
	public LocalNotionThumbnail Thumbnail { get; set; } = LocalNotionThumbnail.None;

	[JsonProperty("renders", NullValueHandling = NullValueHandling.Ignore)]
	public IDictionary<RenderOutput, string> Renders { get; set; } = new Dictionary<RenderOutput, string>();
	
	[JsonProperty("parent", NullValueHandling = NullValueHandling.Ignore)]
	public string Parent { get; set; }

	[JsonProperty("cms", NullValueHandling = NullValueHandling.Ignore)]
	public CMSProperties CMSProperties { get; set; } = null;

	[JsonProperty("last_edited_time")]
	public DateTime LastEditedTime { get; set; }

}