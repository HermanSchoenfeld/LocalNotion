using System.Collections.Generic;
using JsonSubTypes;
using Newtonsoft.Json;

namespace Notion.Client
{
    [JsonConverter(typeof(JsonSubtypes), "type")]
    [JsonSubtypes.KnownSubTypeAttribute(typeof(SinglePropertyRelationInfo), "single_property")]
    [JsonSubtypes.KnownSubTypeAttribute(typeof(DualPropertyRelationInfo), "dual_property")]
    public abstract class RelationInfo
    {
        [JsonProperty("database_id")]
        public string DatabaseId { get; set; }

        [JsonProperty("data_source_id")]
        public string DataSourceId { get; set; }

        // No StringEnumConverter here: Type is a string, and the converter casts its value to
        // System.Enum when writing, which throws InvalidCastException on serialization. Any
        // database carrying a relation property would otherwise be unserializable.
        [JsonProperty("type")]
        public virtual string Type { get; set; }

        [JsonExtensionData]
        public IDictionary<string, object> AdditionalData { get; set; }
    }
}
