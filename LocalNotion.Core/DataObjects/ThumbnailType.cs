using System.Runtime.Serialization;

namespace LocalNotion.Core;

public enum ThumbnailType {

	[EnumMember(Value = "none")]
	None,

	[EnumMember(Value = "emoji")]
	Emoji,

	[EnumMember(Value = "image")]
	Image
}
