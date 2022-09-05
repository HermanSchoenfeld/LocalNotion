using Hydrogen;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;

namespace LocalNotion.Core;

public class LocalNotionRegistry {
	private IList<LocalNotionResource> _resources = new List<LocalNotionResource>();
	
	[JsonProperty("version")]
	public int Version { get; set; } = 1;

	[JsonProperty("notion_api_key", NullValueHandling = NullValueHandling.Ignore)]
	public string NotionApiKey { get; set; } = null;

	[JsonProperty("default_theme")]
	public string DefaultTheme { get; set; } = "default";

	[JsonProperty("theme_maps", NullValueHandling = NullValueHandling.Ignore)]
	public IDictionary<string, string> ThemeMaps { get; set; } = new Dictionary<string, string>();

	[JsonProperty("paths")]
	public LocalNotionPathProfile Paths { get; set; } = LocalNotionPathProfile.Backup;

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