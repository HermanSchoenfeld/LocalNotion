using System.Runtime.Serialization;

namespace LocalNotion.Core;

public enum CMSContentType {
	
	[EnumMember(Value = "none")]
	None,

	[EnumMember(Value = "page")]
	Page,

	[EnumMember(Value = "file")]
	File,

	[EnumMember(Value = "section")]
	SectionedPage,

	[EnumMember(Value = "gallery")]
	Gallery,

	[EnumMember(Value = "book")]
	Book

}
