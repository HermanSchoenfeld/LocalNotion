using System.Runtime.Serialization;

namespace LocalNotion.Core;

public enum ThemeType {
	[EnumMember(Value = "html")]
	Html,

	[EnumMember(Value = "razor")]
	Razor,
}
