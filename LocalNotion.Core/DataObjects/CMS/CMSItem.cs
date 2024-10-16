using Hydrogen;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;

namespace LocalNotion.Core;

public class CMSItem {
	
	[JsonProperty("slug", Required = Required.Always)]
	public string Slug { get; set; } 

	[JsonProperty("type", NullValueHandling = NullValueHandling.Ignore)]
	[JsonConverter(typeof(StringEnumConverter))]
	public CMSItemType ItemType { get; set; }

	[JsonProperty("dirty")]
	public bool Dirty { get; set; }

	[JsonIgnore]
	public bool IsEmpty => Parts.Length == 0;

	[JsonProperty("title", NullValueHandling = NullValueHandling.Ignore)]
	public string Title  { get; set; }

	[JsonProperty("description", NullValueHandling = NullValueHandling.Ignore)]
	public string Description { get; set; }
	
	[JsonProperty("keywords", NullValueHandling = NullValueHandling.Ignore)]
	public string[] Keywords { get; set; }
	
	[JsonProperty("image", NullValueHandling = NullValueHandling.Ignore)]
	public string Image { get; set; }

	[JsonProperty("author", NullValueHandling = NullValueHandling.Ignore)]
	public string Author { get; set; }

	[JsonProperty("header", NullValueHandling = NullValueHandling.Ignore)]
	public string HeaderID { get; set; }

	[JsonProperty("menu", NullValueHandling = NullValueHandling.Ignore)]
	public string MenuID { get; set; }

	[JsonProperty("parts", NullValueHandling = NullValueHandling.Ignore)]
	public string[] Parts { get; set; } = Array.Empty<string>();

	[JsonProperty("footer", NullValueHandling = NullValueHandling.Ignore)]
	public string FooterID { get; set; }

	[JsonProperty("render_path", NullValueHandling = NullValueHandling.Ignore)]
	public string RenderPath { get; set; } = null;

	public bool ReferencesResource(string resourceID) 
		=> ReferencesAnyResources([resourceID]);

	public bool ReferencesAnyResource(IEnumerable<string> resourceIDs) 
		=> ReferencesAnyResources(resourceIDs.ToHashSet());

	public bool ReferencesAnyResources(HashSet<string> resourceIDs) {
		
		if (HeaderID != null && resourceIDs.Contains(HeaderID))
			return true;
		
		if (MenuID != null && resourceIDs.Contains(MenuID))
			return true;
		
		if (FooterID != null && resourceIDs.Contains(FooterID))
			return true;

		return Parts != null && Parts.Any(resourceIDs.Contains);

	}

	public void RemovePageReference(string page) {
		if (HeaderID == page)
			HeaderID = null;

		if (MenuID == page)
			MenuID = null;

		if (FooterID == page)
			FooterID = null;

		Parts = Parts.Except(page).ToArray();
	}
}