using System.Xml.Serialization;

namespace LocalNotion.Core;

[XmlRoot("urlset", Namespace = "http://www.sitemaps.org/schemas/sitemap/0.9")]
public class SitemapXml {
	private Dictionary<string, Node> _nodes = new();

	public bool HasNode(string url) => _nodes.ContainsKey(url);

	public void Add(string url, DateTime? lastModified = null, Frequency? frequency = null, double? priority = null)
		=> _nodes.Add(
			url,
			new Node {
				Url = url,
				LastModified = lastModified,
				Frequency = frequency,
				Priority = priority
			}
		);

	[XmlElement("url", typeof(Node))]
	public Node[] Nodes {
		get => _nodes.Values.ToArray();
		set => _nodes = value != null ? value.ToDictionary(x => x.Url) : new();
	}

	public class Node {

		[XmlElement("loc")] public string Url { get; set; }

		[XmlElement("lastmod", DataType = "date")]
		public DateTime? LastModified { get; set; }

		[XmlElement("changefreq")] 
		public Frequency? Frequency { get; set; }

		[XmlElement("priority")] 
		public double? Priority { get; set; }

		public bool LastModifiedSpecified => LastModified != null;

		public bool FrequencySpecified => Frequency != null;

		public bool PrioritySpecified => Priority != null;

	}

	public enum Frequency {

		[XmlEnum("never")] 
		Never,

		[XmlEnum("yearly")] 
		Yearly,

		[XmlEnum("monthly")] 
		Monthly,

		[XmlEnum("weekly")] 
		Weekly,

		[XmlEnum("daily")] 
		Daily,

		[XmlEnum("hourly")] 
		Hourly,

		[XmlEnum("always")] 
		Always
	}
}