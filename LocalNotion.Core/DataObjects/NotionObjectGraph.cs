using Newtonsoft.Json;
using Notion.Client;

namespace LocalNotion.Core;

public class NotionObjectGraph {

	[JsonProperty("objectID")]
	public virtual string ObjectID { get; set; }

	[JsonProperty("children", NullValueHandling = NullValueHandling.Ignore)]
	public virtual NotionObjectGraph[] Children { get; set; } = Array.Empty<NotionObjectGraph>();

	public IEnumerable<NotionObjectGraph> VisitAll() {
		yield return this;
		foreach (var child in Children) {
			foreach (var childVal in child.VisitAll())
				yield return childVal;
		}
	}

}
