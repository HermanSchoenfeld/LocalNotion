using JsonSubTypes;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Notion.Client;

namespace LocalNotion;


[JsonConverter(typeof(JsonSubtypes), "type")]
[JsonSubtypes.KnownSubType(typeof(HtmlTemplateInfo), TemplateType.Html)]
public abstract class TemplateInfo {

	[JsonProperty("type")]
	[JsonConverter(typeof(StringEnumConverter))]
	public virtual TemplateType Type { get; set; }


	[JsonIgnore]
	public string TemplatePath { get; set; }

}