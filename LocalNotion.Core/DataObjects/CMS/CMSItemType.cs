using System.Runtime.Serialization;

namespace LocalNotion.Core;

public enum CMSItemType {

	[EnumMember(Value = "page")]
	Page,

	[EnumMember(Value = "sectioned_page")]
	SectionedPage,

	[EnumMember(Value = "category_page")]
	ArticleCategory,

	[EnumMember(Value = "gallery_page")]
	GalleryPage
}
