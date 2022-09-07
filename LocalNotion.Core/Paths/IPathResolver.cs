using Hydrogen;


namespace LocalNotion.Core;

public interface IPathResolver {

	LocalNotionMode Mode { get; }

	/// <summary>
	/// Gets the path to the registry file. When <paramref name="pathType" /> is <see cref="FileSystemPathType.Relative"/> the path is relative to
	/// the repository folder.
	/// </summary>
	/// <param name="pathType">Absolute ore Relative</param>
	/// <returns>Path to the registry file</returns>
	string GetRegistryFilePath(FileSystemPathType pathType);

	/// <summary>
	/// Gets the path to the repository folder. When <paramref name="pathType" /> is <see cref="FileSystemPathType.Relative"/> the path is relative to
	/// the registry folder (i.e. typically a ".localnotion" subfolder within the repository).
	/// </summary>
	/// <param name="pathType">Absolute ore Relative</param>
	/// <returns>Path to the registry file</returns>
	string GetRepositoryPath(FileSystemPathType pathType);

	/// <summary>
	/// Gets the path to the folder which contains renders of resources of type <paramref name="resourceType"/>.
	/// When <paramref name="pathType" /> is <see cref="FileSystemPathType.Relative"/> the path is relative to
	/// the repository folder.
	/// </summary>
	/// <param name="resourceType">The type of resource being enquired /></param>
	/// <param name="pathType">Absolute ore Relative</param>
	/// <returns>Path to the registry file</returns>
	string GetResourceTypeFolderPath(LocalNotionResourceType resourceType, FileSystemPathType pathType);

	/// <summary>
	/// Gets the path to the repository folder. When <paramref name="pathType" /> is <see cref="FileSystemPathType.Relative"/> the path is relative to
	/// the repository folder.
	/// </summary>
	/// <param name="pathType">Absolute ore Relative</param>
	/// <returns>Path to the registry file</returns>
	string GetResourceFolderPath(LocalNotionResourceType resourceType, string resourceId, FileSystemPathType pathType);

	/// <summary>
	/// Gets the path to the object folder. When <paramref name="pathType" /> is <see cref="FileSystemPathType.Relative"/> the path is relative to
	/// the repository folder.
	/// </summary>
	/// <param name="pathType">Absolute ore Relative</param>
	/// <returns>Path to the theme folder</returns>
	string GetInternalResourceFolderPath(InternalResourceType internalResourceType, FileSystemPathType pathType);


	bool UsesObjectIDSubFolders(LocalNotionResourceType resourceType);

	/// <summary>
	/// Calculates the file path intended for a resource.
	/// </summary>
	/// <param name="resourceType"></param>
	/// <param name="resourceID"></param>
	/// <param name="resourceTitle"></param>
	/// <param name="renderType"></param>
	/// <param name="pathType"></param>
	/// <returns></returns>
	string CalculateResourceFilePath(LocalNotionResourceType resourceType, string resourceID, string resourceTitle, RenderType renderType, FileSystemPathType pathType);

	string ResolveConflictingFilePath(string filepath);


	string GetRemoteHostedBaseUrl();


}
