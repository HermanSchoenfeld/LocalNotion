using System.Runtime.Serialization;

namespace LocalNotion;

public enum TemplateType {
	[EnumMember(Value = "html")]
	Html,

	[EnumMember(Value = "razor")]
	Razor,
}
