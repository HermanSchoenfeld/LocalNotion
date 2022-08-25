using System.Runtime.Serialization;

namespace LocalNotion.Core;

public enum LocalNotionMode {

	[EnumMember(Value = "offline")]
	Offline,

	[EnumMember(Value = "online")]
	Online,

}