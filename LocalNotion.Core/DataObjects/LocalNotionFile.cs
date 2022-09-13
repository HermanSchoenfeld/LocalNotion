using Newtonsoft.Json;

namespace LocalNotion.Core;

public class LocalNotionFile : LocalNotionResource {

	public override LocalNotionResourceType Type => LocalNotionResourceType.File;

	[JsonProperty("mimetype")]
	public string MimeType { get; set; }

	public static bool TryParse(string resourceID, string filename, string parentResourceID, string mimeType, out LocalNotionFile localNotionFile) {
		localNotionFile = new() {
			ID = resourceID,
			MimeType = mimeType ?? (Tools.Network.TryGetMimeType(filename, out var mt) ? mt : "application/octet-stream"),
			Title = filename,
			ParentResourceID = parentResourceID
		};
		return true;
	}

	public static LocalNotionFile Parse(string resourceID, string filename, string parentResourceID, string mimeType) 
		=> TryParse(resourceID, filename, parentResourceID, mimeType, out var LocalNotionFile) ? LocalNotionFile : throw new FormatException($"Unable to parse {nameof(LocalNotionFile)}");
}
