using Newtonsoft.Json;

namespace LocalNotion.Core;

public class NGinxSettings {

	[JsonProperty("enabled")]
	public bool Enabled { get; set; }

	[JsonProperty("reload_cmd")]
	public string ReloadCommand { get; set; }

	public static NGinxSettings Default { get; set; } = new ();
}