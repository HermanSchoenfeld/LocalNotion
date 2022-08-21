using System.Collections;
using Hydrogen;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;

namespace LocalNotion;

public class LocalNotionRegistry {
	private IList<LocalNotionResource> _resources = new List<LocalNotionResource>();
	
	[JsonProperty("version")]
	public int Version { get; set; } = 1;

	[JsonProperty("notion_api_key", NullValueHandling = NullValueHandling.Ignore)]
	public string NotionApiKey { get; set; } = null;

	[JsonProperty("mode")]
	[JsonConverter(typeof(StringEnumConverter))]
	public LocalNotionMode Mode { get; set; } = LocalNotionMode.Offline;

	[JsonProperty("default_template")]
	public string DefaultTemplate { get; set; } = "default";

	[JsonProperty("root_templates", NullValueHandling = NullValueHandling.Ignore)]
	public IDictionary<string, string> RootTemplates { get; set; } = new Dictionary<string, string>();

	[JsonProperty("base_url")]
	public string BaseUrl { get; set; }

	[JsonProperty("objects_rel_path")]
	public string ObjectsRelPath { get; set; }

	[JsonProperty("templates_rel_path")]
	public string TemplatesRelPath { get; set; }

	[JsonProperty("files_rel_path")]
	public string FilesRelPath { get; set; }

	[JsonProperty("pages_rel_path")]
	public string PagesRelPath { get; set; }

	[JsonProperty("logs_rel_path")]
	public string LogsRelPath { get; set; }

	[JsonProperty("log_level")]
	[JsonConverter(typeof(StringEnumConverter))]
	public LogLevel LogLevel { get; set; }

	[JsonProperty("resources")]
	public LocalNotionResource[] Resources {
		get => _resources.ToArray();
		set => _resources = (value ?? Array.Empty<LocalNotionResource>()).ToList();
	}
	

	[JsonIgnore]
	public IEnumerable<LocalNotionPage> Articles =>
		Resources
		.Where(x => x.Type == LocalNotionResourceType.Page && x is LocalNotionPage { CMSProperties: not null })
		.Cast<LocalNotionPage>();


	public void Add(LocalNotionResource resource) {
		_resources.Add(resource);
	}

	public void Remove(LocalNotionResource resource) {
		_resources.Remove(resource);
	}
}
