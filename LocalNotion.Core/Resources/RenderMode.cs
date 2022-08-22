using System.Runtime.Serialization;

namespace LocalNotion;

public enum RenderMode {

	[EnumMember(Value = "readonly")]
	ReadOnly,

	[EnumMember(Value = "editable")]
	Editable,

}
