using System.Runtime.Serialization;

namespace LocalNotion.Core;

public enum CMSItemType {

	[EnumMember(Value = "page")]
	Page,

	[EnumMember(Value = "sectioned_page")]
	SectionedPage,

	[EnumMember(Value = "category_page")]
	CategoryPage,

	[EnumMember(Value = "gallery_page")]
	GalleryPage
}
