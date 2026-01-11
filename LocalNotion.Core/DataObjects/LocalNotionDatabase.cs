using Sphere10.Framework;
using Newtonsoft.Json;
using Notion.Client;

namespace LocalNotion.Core;

public class LocalNotionDatabase : LocalNotionEditableResource {

	public override LocalNotionResourceType Type => LocalNotionResourceType.Database;

	[JsonProperty("primary_datasource_id")]
	public string PrimaryDataSourceID { get; set; }

	[JsonProperty("description")]
	public string Description { get; set; }

	[JsonProperty("properties")]
	public IDictionary<string, DataSourcePropertyConfig> Properties { get; set; }
	
}