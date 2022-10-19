using Newtonsoft.Json;
using Newtonsoft.Json.Converters;

namespace LocalNotion.Core;

public class CMSProperties {

	[JsonProperty("type", NullValueHandling = NullValueHandling.Ignore)]
	[JsonConverter(typeof(StringEnumConverter))]
	public CMSPageType PageType { get; set; }

	[JsonProperty("publish_on", NullValueHandling = NullValueHandling.Ignore)]
	public DateTime? PublishOn { get; set; }

	[JsonProperty("status", NullValueHandling = NullValueHandling.Ignore)]
	[JsonConverter(typeof(StringEnumConverter))]
	public CMSItemStatus Status { get; set; }

	[JsonProperty("themes", NullValueHandling = NullValueHandling.Ignore)]
	public string[] Themes { get; set; }

	[JsonProperty("custom_slug")]
	public string CustomSlug { get; set; }

	[JsonProperty("sequence", NullValueHandling = NullValueHandling.Ignore)]
	public int? Sequence { get; set; }

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

	[JsonProperty("is_partial", NullValueHandling = NullValueHandling.Ignore)]
	public bool IsPartial { get; set; }

}
