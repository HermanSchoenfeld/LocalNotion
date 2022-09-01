using System.Runtime.Serialization;

namespace LocalNotion.Core;

public enum RenderMode {

	[EnumMember(Value = "readonly")]
	ReadOnly,

	[EnumMember(Value = "editable")]
	Editable,

}
