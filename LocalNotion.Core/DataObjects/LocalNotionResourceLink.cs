using Hydrogen;
using Newtonsoft.Json;

namespace LocalNotion.Core;

public class LocalNotionRenderLink {

	[JsonProperty("id")]
	public string ResourceID { get; set; }

	[JsonProperty("render")]
	public RenderType RenderType { get; set; }

	public override string ToString() => $"resource://{ResourceID}:{Tools.Enums.GetSerializableOrientedName(RenderType)}";

	public static string GenerateUrl(string resourceID, RenderType renderType) 
		=> new LocalNotionRenderLink { ResourceID = resourceID, RenderType = renderType }.ToString();

	public static bool TryParse(string url, out LocalNotionRenderLink link) {
		link = default;

		if (!url.StartsWith("resource://"))
			return false;

		url = url.TrimStart("resource://");
		var splits = url.Split(':');
		if (splits.Length != 2)
			return false;

		if (!LocalNotionHelper.IsValidObjectID(splits[0]))
			return false;

		if (!Tools.Enums.TryParseEnum<RenderType>(splits[1], out var renderType))
			return false;

		link = new LocalNotionRenderLink {
			ResourceID = splits[0],
			RenderType = renderType
		};

		return true;
	}
		
	public static LocalNotionRenderLink ParseLocalNotionResourceUrl(string url) 
		=> TryParse(url, out var link) ? link : throw new FormatException($"Url is not a validly formatted {nameof(LocalNotionRenderLink)}: {url}");

}
