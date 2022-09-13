using Newtonsoft.Json;
using Newtonsoft.Json.Converters;

namespace LocalNotion.Core;

public class LocalNotionPathProfile {

	/// <summary>
	/// The mode which determines how links are generated.
	/// </summary>
	[JsonProperty("mode")]
	[JsonConverter(typeof(StringEnumConverter))]
	public LocalNotionMode Mode { get; set; } = LocalNotionMode.Offline;

	/// <summary>
	/// The path to the repository relative to the **REGISTRY FILE**.
	/// </summary>
	[JsonProperty("repository_path")]
	public string RepositoryPathR { get; set; } = "../";

	/// <summary>
	/// The path to the registry file relative to the repository.
	/// </summary>
	[JsonIgnore]
	public string RegistryPathR { get; set; } = Constants.DefaultRegistryFolderName + "/" + Constants.DefaultRegistryFileName;

	/// <summary>
	/// Prefix to urls when generating links
	/// </summary>
	[JsonProperty("base_url")]
	public string BaseUrl { get; set; } = string.Empty;

	/// <summary>
	/// The path to the objects folder relative to the repository.
	/// </summary>
	[JsonProperty("objects_path")]
	public string ObjectsPathR { get; set; } = Constants.DefaultRegistryFolderName + "/" + Constants.DefaultObjectsFolderName;

	/// <summary>
	///  Path to the graphs folder relative to the repository.
	/// </summary>
	[JsonProperty("graphs_path")]
	public string GraphsPathR { get; set; } = Constants.DefaultRegistryFolderName + "/" + Constants.DefaultGraphsFolderName;

	/// <summary>
	///  Path to the page properties folder relative to the repository.
	/// </summary>
	[JsonProperty("properties_path")]
	public string PropertiesPathR { get; set; } = Constants.DefaultRegistryFolderName + "/" + Constants.DefaultPropertiesFolderName;

	/// <summary>
	/// Path to the themes folder relative to the repository.
	/// </summary>
	[JsonProperty("themes_path")]
	public string ThemesPathR { get; set; } = Constants.DefaultRegistryFolderName + "/" + Constants.DefaultThemesFolderName;

	/// <summary>
	/// Path to the downloaded files folder relative to the repository.
	/// </summary>
	[JsonProperty("files_path")]
	public string FilesPathR { get; set; } = Constants.DefaultFilesFolderName;

	/// <summary>
	/// Path to the rendered databases relative to the repository.
	/// </summary>
	[JsonProperty("databases_path")]
	public string DatabasesPathR { get; set; } = Constants.DefaultDatabasesFolderName;

	/// <summary>
	/// Path to the rendered workspace/index pages relative to the repository.
	/// </summary>
	[JsonProperty("workspace_path")]
	public string WorkspacePathR { get; set; } = Constants.DefaultWorkspacesFolderName;

	/// <summary>
	/// Path to the rendered pages relative to the repository.
	/// </summary>
	[JsonProperty("pages_path")]
	public string PagesPathR { get; set; } = Constants.DefaultPagesFolderName;

	/// <summary>
	/// Path to the logs relative to the repository
	/// </summary>
	[JsonProperty("logs_path")]
	public string LogsPathR { get; set; } = Constants.DefaultRegistryFolderName + "/" + Constants.DefaultLogsFolderName;

	/// <summary>
	/// When true, rendered pages will be contained within a dedicated sub-folder inside <see cref="PagesPathR"/> named after page id. When false, all pages will be rendered side-by-side in <see cref="PagesPathR"/>.
	/// </summary>
	[JsonProperty("use_page_id_folders")]
	public bool UsePageIDFolders { get; set; } = Constants.DefaultUseObjectIDFolders;

	/// <summary>
	/// When true, rendered pages will be contained within a dedicated sub-folder inside <see cref="FilesPathR"/> named after page id. When false, all pages will be rendered side-by-side in <see cref="FilesPathR"/>.
	/// </summary>
	[JsonProperty("use_file_id_folders")]
	public bool UseFileIDFolders { get; set; } = Constants.DefaultUseObjectIDFolders;

	/// <summary>
	/// When true, rendered pages will be contained within a dedicated sub-folder inside <see cref="DatabasesPathR"/> named after page id. When false, all pages will be rendered side-by-side in <see cref="DatabasesPathR"/>.
	/// </summary>
	[JsonProperty("use_database_id_folders")]
	public bool UseDatabaseIDFolders { get; set; } = Constants.DefaultUseObjectIDFolders;

	/// <summary>
	/// When true, rendered pages will be contained within a dedicated sub-folder inside <see cref="WorkspacePathR"/> named after page id. When false, all pages will be rendered side-by-side in <see cref="WorkspacePathR"/>.
	/// </summary>
	[JsonProperty("use_workspace_id_folders")]
	public bool UseWorkspaceIDFolders { get; set; } = Constants.DefaultUseObjectIDFolders;

	/// <summary>
	/// When true, Local Notion will download external images and files as well.
	/// </summary>
	[JsonProperty("download_external")]
	public bool ForceDownloadExternalContent { get; set; } = Constants.DefaultDownloadExternalContent;

	/// <summary>
	/// Default profile for standard Local Notion repository.
	/// </summary>
	public static LocalNotionPathProfile Default => new ();

	/// <summary>
	/// Backup profile for standard Local Notion repository. Currently, this is same as default.
	/// </summary>
	public static LocalNotionPathProfile Backup => Default;

	/// <summary>
	/// Offline profile for a Local Notion repository. Same as <see cref="Backup"/> but downloads external content.
	/// </summary>
	public static LocalNotionPathProfile Offline => new () {
		ForceDownloadExternalContent = true,
	};

	/// <summary>
	/// Publishing profile for Local Notion repository. Suitable for create distributable content such as eBooks, manuals, etc.
	/// </summary>
	public static LocalNotionPathProfile Publishing => new () {
		PagesPathR = string.Empty,
		FilesPathR = Constants.DefaultFilesFolderName,
		DatabasesPathR = string.Empty,
		WorkspacePathR = string.Empty,
		ThemesPathR = Constants.DefaultThemesFolderName,
		UsePageIDFolders = false,
		UseFileIDFolders = false,
		UseDatabaseIDFolders = false,
		UseWorkspaceIDFolders = false,
		ForceDownloadExternalContent = true,
	};

	/// <summary>
	/// Web hosting profile for a Local Notion repository. Suitable for content that is hosted on a web server.
	/// </summary>
	public static LocalNotionPathProfile WebHosting => new () {
		BaseUrl = "/",
		PagesPathR = string.Empty,
		FilesPathR = string.Empty,
		DatabasesPathR = string.Empty,
		WorkspacePathR = string.Empty,
	};

}