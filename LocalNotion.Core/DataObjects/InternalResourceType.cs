using System.Runtime.Serialization;

namespace LocalNotion.Core;

public enum InternalResourceType {
	[EnumMember(Value = "object")]
	Objects,

	[EnumMember(Value = "graph")]
	Graphs,

	[EnumMember(Value = "themes")]
	Themes,

	[EnumMember(Value = "logs")]
	Logs
}
