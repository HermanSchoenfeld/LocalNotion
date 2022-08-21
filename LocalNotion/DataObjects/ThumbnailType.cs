using System.Runtime.Serialization;

namespace LocalNotion;

public enum ThumbnailType {

	[EnumMember(Value = "none")]
	None,

	[EnumMember(Value = "emoji")]
	Emoji,

	[EnumMember(Value = "image")]
	Image
}
