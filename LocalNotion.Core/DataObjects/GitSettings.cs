using Newtonsoft.Json;

namespace LocalNotion.Core;

public class GitSettings {

	[JsonProperty("enabled")]
	public bool Enabled { get; set; }

	[JsonProperty("push")]
	public bool Push { get; set; }

	public static GitSettings Default { get; set; } = new ();

}