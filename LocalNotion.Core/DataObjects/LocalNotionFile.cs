using Newtonsoft.Json;

namespace LocalNotion.Core;

public class LocalNotionFile : LocalNotionResource {

	public override LocalNotionResourceType Type => LocalNotionResourceType.File;

	[JsonProperty("mimetype")]
	public string MimeType { get; set; }

	public static bool TryParse(string resourceID, string filename, out LocalNotionFile localNotionFile) {
		localNotionFile = new() {
			ID = resourceID,
			MimeType = Tools.Network.GetMimeType(filename),
			Title = filename,
		};
		return true;
	}

	public static LocalNotionFile Parse(string resourceID, string filename) 
		=> TryParse(resourceID, filename, out var LocalNotionFile) ? LocalNotionFile : throw new FormatException($"Unable to parse {nameof(LocalNotionFile)}");
}
