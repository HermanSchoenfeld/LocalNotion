using JsonSubTypes;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Notion.Client;

namespace LocalNotion.Core;


[JsonConverter(typeof(JsonSubtypes), "type")]
[JsonSubtypes.KnownSubType(typeof(HtmlThemeInfo), ThemeType.Html)]
public abstract class ThemeInfo {

	[JsonProperty("type")]
	[JsonConverter(typeof(StringEnumConverter))]
	public virtual ThemeType Type { get; set; }


	[JsonIgnore]
	public string TemplatePath { get; set; }

}