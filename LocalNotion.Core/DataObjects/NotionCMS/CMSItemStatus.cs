using System.Runtime.Serialization;

namespace LocalNotion.Core;

public enum CMSItemStatus {
	[EnumMember(Value = "draft")]
	Draft,

	[EnumMember(Value = "QA")]
	QA,

	[EnumMember(Value = "published")]
	Published,

	[EnumMember(Value = "hidden")]
	Hidden
}
