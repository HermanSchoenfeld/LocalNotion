using System.Collections.Generic;
using Newtonsoft.Json;

namespace Notion.Client
{
    public class IconPageIcon : IPageIcon
    {
        public string Type { get; set; } = PageIconTypes.Icon;

        [JsonProperty(PageIconTypes.Icon)]
        public NotionIcon Icon { get; set; }

        [JsonExtensionData]
        public IDictionary<string, object> AdditionalData { get; set; }
    }
}
