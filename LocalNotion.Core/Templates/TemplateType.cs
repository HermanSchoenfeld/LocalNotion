using System.Runtime.Serialization;

namespace LocalNotion.Core;

public enum TemplateType {
	[EnumMember(Value = "html")]
	Html,

	[EnumMember(Value = "razor")]
	Razor,
}
