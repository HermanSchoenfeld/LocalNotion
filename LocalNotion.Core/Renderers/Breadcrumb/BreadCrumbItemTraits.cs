namespace LocalNotion.Core;

[Flags]
public enum BreadCrumbItemTraits : uint {
	IsCurrentPage = 1 << 0,
	IsPage = 1 << 1,
	IsCMSPage = 1 << 2,
	IsFile = 1 << 3,
	IsDatabase = 1 << 4,
	IsCategory = 1 << 5,
	IsRoot = 1 << 6,
	IsWorkspace = 1 << 7,
	HasUrl = 1 << 8,
	HasIcon = 1 << 9,
	HasEmojiIcon = 1 << 10,
	HasImageIcon = 1 << 11
}
