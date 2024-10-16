#pragma warning disable CS8618

namespace LocalNotion.Core;

public static class Constants {
	public const string EmptyArticleName = "Unnamed Page";
	public const string PageTypePropertyName = "Type";
	public const string TitlePropertyName = "Item";
	public const string PublishOnPropertyName = "Publish On";
	public const string StatusPropertyName = "Status";
	public const string ThemesPropertyName = "Themes";
	public const string SlugPropertyName = "Custom Slug";
	public const string SequencePropertyName = "Sequence";
	public const string RootCategoryPropertyName = "Root";
	public const string Category1PropertyName = "Category1";
	public const string Category2PropertyName = "Category2";
	public const string Category3PropertyName = "Category3";
	public const string Category4PropertyName = "Category4";
	public const string Category5PropertyName = "Category5";
	public const string TagsPropertyName = "Tags";
	public const string SummaryPropertyName = "Summary";
	public const string CreatedByPropertyName = "Created By";
	public const string CreatedOnPropertyName = "Created On";
	public const string EditedByPropertyName = "Edited By";
	public const string EditedOnPropertyName = "Edited On";
	public const string DefaultResourceTitle = "Untitled";
	public const string WorkspaceId = "00000000-0000-0000-0000-000000000000";

	public const string DefaultTheme = "default";
	public const string DefaultRegistryFolderName = ".localnotion";
	public const string DefaultRegistryFileName = "registry.json";
	public const string ThemeInfoFileName = ".config.json";
	public const string DefaultObjectsFolderName = "objects";
	public const string DefaultPropertiesFolderName = "properties";
	public const string DefaultGraphsFolderName = "graphs";
	public const string DefaultThemesFolderName = "themes";
	public const string DefaultPagesFolderName = "pages";
	public const string DefaultFilesFolderName = "files";
	public const string DefaultDatabasesFolderName = "databases";
	public const string DefaultWorkspacesFolderName = "workspaces";
	public const string DefaultCMSFolderName = "cms";
	public const string DefaultLogsFolderName = "logs";
	public const string DefaultLogFilename = "localnotion.log";
	public const bool DefaultUseObjectIDFolders = true;
	public const bool DefaultDownloadExternalContent = false;

	public const string NotionCMSCategoryWildcard = "all";


	// LocalNotion CMS Tags
	public const string TagContactForm = "@ContactForm";
	public const string TagShowChildPageTitleOnBanner = "@ShowChildPageTitleOnBanner";
	public const string TagShowTitleOnBanner = "@ShowTitleOnBanner";
	public const string TagContactFormNoAnimate = "@ContactFormNoAnimate";
	public const string TagHideHeader = "@HideHeader";
	public const string TagHideNavBar = "@HideNavBar";
	public const string TagHideFooter = "@HideFooter";


	// if a file is synchronized too soon after it is edited, it is premature and resynced again later
	// Notion API only keeps minute-level accuracy
	public const int PrematureSyncThreshholdSec = 60;   

	public const int ERRORCODE_OK = 0;
	public const int ERRORCODE_CANCELLED = -1;
	public const int ERRORCODE_COMMANDLINE_ERROR = -2;
	public const int ERRORCODE_REPO_NOT_FOUND = -3;
	public const int ERRORCODE_REPO_ERROR = -4;
	public const int ERRORCODE_REPO_NO_APIKEY = -5;
	public const int ERRORCODE_NOT_IMPLEMENTED = -6;
	public const int ERRORCODE_LICENSE_ERROR = -7;
	public const int ERRORCODE_FAIL = -8;
}
