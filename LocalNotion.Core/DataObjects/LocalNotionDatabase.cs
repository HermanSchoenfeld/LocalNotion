using Hydrogen;
using Newtonsoft.Json;
using Notion.Client;

namespace LocalNotion.Core;

public class LocalNotionDatabase : LocalNotionEditableResource {

	public override LocalNotionResourceType Type => LocalNotionResourceType.Database;

	[JsonProperty("description")]
	public string Description { get; set; }

	[JsonProperty("properties")]
	public IDictionary<string, Property> Properties { get; set; }
	
}