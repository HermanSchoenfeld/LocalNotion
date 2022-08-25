using Newtonsoft.Json;

namespace LocalNotion.Core;

public class LocalNotionFile : LocalNotionResource {

	public override LocalNotionResourceType Type => LocalNotionResourceType.File;

	[JsonProperty("mimetype")]
	public string MimeType { get; set; }

	// Add content hash?

}
