
using Hydrogen;

namespace LocalNotion.Core;
public class RepoPaths {

	public string RepositoryFolderRelPath { get; set; }

	public string ObjectsFolderRelPath { get; set; }

	public string GraphFolderRelPath { get; set; }

	public string PagesFolderRelPath { get; set; }

	public string FilesFolderRelPath { get; set; }

	public string DatabasesFolderRelPath { get; set; }

	public string ThemesFolderRelPath { get; set; }

	public string LogsFolderRelPath { get; set; }
	

	public bool UseObjectIDParentFolder { get; set; }

	public string GetRepositoryPath(FileSystemPathType pathType) { return RepositoryFolderRelPath; }

	public string GeObjectsPath(FileSystemPathType pathType) { return ObjectsFolderRelPath; }



	
}


/*
 
	public string PagesPath => Path.GetFullPath(_registry.PagesRelPath, Tools.FileSystem.GetParentDirectoryPath(RepositoryPath));

	public string FilesPath => Path.GetFullPath(_registry.FilesRelPath, Tools.FileSystem.GetParentDirectoryPath(RepositoryPath));

	public string TemplatesPath => Path.GetFullPath(_registry.TemplatesRelPath, Tools.FileSystem.GetParentDirectoryPath(RepositoryPath));

	public string LogsPath => Path.GetFullPath(_registry.LogsRelPath, Tools.FileSystem.GetParentDirectoryPath(RepositoryPath));
 
 */
