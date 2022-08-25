using System.Runtime.CompilerServices;
using Hydrogen;
using Newtonsoft.Json;
using Notion.Client;

namespace LocalNotion.Core;

public class LocalNotionBookmark {

	[JsonProperty("title")]
	public string Title { get; init; }

	[JsonProperty("summary", NullValueHandling = NullValueHandling.Ignore)]
	public string Summary { get; init; }

	[JsonProperty("url", NullValueHandling = NullValueHandling.Ignore)]
	public string Url { get; init; }

	[JsonProperty("image_url", NullValueHandling = NullValueHandling.Ignore)]
	public string ImageUrl { get; set; }

	[JsonProperty("thumbnail")]
	public LocalNotionThumbnail Thumbnail { get; set; } = LocalNotionThumbnail.None;
}