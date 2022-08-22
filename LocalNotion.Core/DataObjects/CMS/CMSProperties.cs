using Newtonsoft.Json;
using Newtonsoft.Json.Converters;

namespace LocalNotion;

public class CMSProperties {

	[JsonProperty("publish_on", NullValueHandling = NullValueHandling.Ignore)]
	public DateTime? PublishOn { get; set; }

	[JsonProperty("status", NullValueHandling = NullValueHandling.Ignore)]
	[JsonConverter(typeof(StringEnumConverter))]
	public CMSItemStatus Status { get; set; }

	[JsonProperty("slug")]
	public string Slug { get; set; }

	[JsonProperty("location", NullValueHandling = NullValueHandling.Ignore)]
	public string Location { get; set; }

	[JsonProperty("root", NullValueHandling = NullValueHandling.Ignore)]
	public string Root { get; set; }

	[JsonProperty("category1", NullValueHandling = NullValueHandling.Ignore)]
	public string Category1 { get; set; }

	[JsonProperty("category2", NullValueHandling = NullValueHandling.Ignore)]
	public string Category2 { get; set; }

	[JsonProperty("category3", NullValueHandling = NullValueHandling.Ignore)]
	public string Category3 { get; set; }

	[JsonProperty("category4", NullValueHandling = NullValueHandling.Ignore)]
	public string Category4 { get; set; }

	[JsonProperty("category5", NullValueHandling = NullValueHandling.Ignore)]
	public string Category5 { get; set; }
	
	[JsonProperty("summary", NullValueHandling = NullValueHandling.Ignore)]
	public string Summary { get; set; }

}
