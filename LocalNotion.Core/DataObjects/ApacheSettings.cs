using Newtonsoft.Json;

namespace LocalNotion.Core;

public class ApacheSettings {

	[JsonProperty("enabled")]
	public bool Enabled { get; set; }

	public static ApacheSettings Default { get; set; } = new ();
}
