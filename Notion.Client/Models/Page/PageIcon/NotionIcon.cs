using System.Collections.Generic;
using Newtonsoft.Json;

namespace Notion.Client
{
    /// <summary>
    /// An icon from Notion's built-in icon library, identified by name and color.
    /// </summary>
    public class NotionIcon
    {
        [JsonProperty("name")]
        public string Name { get; set; }

        [JsonProperty("color")]
        public string Color { get; set; }

        /// <summary>
        /// Not part of the Notion API response. Notion serves these icons from a predictable CDN
        /// path rather than sending a url, so this slot exists for consumers which rewrite the
        /// icon to a locally downloaded copy, mirroring <see cref="ExternalFileInfo.Url"/>.
        /// </summary>
        [JsonProperty("url", NullValueHandling = NullValueHandling.Ignore)]
        public string Url { get; set; }

        [JsonExtensionData]
        public IDictionary<string, object> AdditionalData { get; set; }
    }
}
