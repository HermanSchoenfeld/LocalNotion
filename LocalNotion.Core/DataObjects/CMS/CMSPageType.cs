using System.Runtime.Serialization;

namespace LocalNotion.Core;

public enum CMSPageType {

	[EnumMember(Value = "header")]
	Header,

	[EnumMember(Value = "menu")]
	Menu,

	[EnumMember(Value = "page")]
	Page,

	[EnumMember(Value = "section")]
	Section,

	[EnumMember(Value = "gallery")]
	Gallery, 

	[EnumMember(Value = "footer")]
	Footer

}