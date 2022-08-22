using System.Runtime.Serialization;

namespace LocalNotion;

public enum LocalNotionMode {

	[EnumMember(Value = "offline")]
	Offline,

	[EnumMember(Value = "online")]
	Online,

}