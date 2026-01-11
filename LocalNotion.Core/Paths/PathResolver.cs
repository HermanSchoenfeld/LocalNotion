using Sphere10.Framework;
using System.IO;

namespace LocalNotion.Core;

public class PathResolver : IPathResolver {

	public PathResolver(string repoPath, LocalNotionPathProfile pathProfile) {
		Guard.ArgumentNotNull(repoPath, nameof(repoPath));
		Guard.ArgumentNotNull(pathProfile, nameof(pathProfile));
		Guard.DirectoryExists(repoPath);
		RepositoryPath = Tools.FileSystem.GetCaseCorrectDirectoryPath(repoPath);
		PathProfile = pathProfile;
		// ensure path profile's reference to repo dir is same as passed in repo dir
		Guard.Ensure(Path.GetFullPath(pathProfile.RepositoryPathR, Path.GetDirectoryName( Path.Combine(repoPath, pathProfile.RegistryPathR))).TrimEnd(new[] {'/','\\'}) == repoPath.TrimEnd(new[] {'/','\\'}), $"Path profile does not resolve to '{repoPath}'");
	}

	protected string RepositoryPath { get; }

	protected LocalNotionPathProfile PathProfile { get; }

	public LocalNotionMode Mode => PathProfile.Mode;
	public bool ForceDownloadExternalContent => PathProfile.ForceDownloadExternalContent;

	public string GetRegistryFilePath(FileSystemPathType pathType)
		=> pathType switch {
			FileSystemPathType.Relative => PathProfile.RegistryPathR,
			FileSystemPathType.Absolute => Path.GetFullPath(PathProfile.RegistryPathR, RepositoryPath),
			_ => throw new NotSupportedException($"{pathType}")
		};

	public string GetRepositoryPath(FileSystemPathType pathType)
		=> pathType switch {
			FileSystemPathType.Relative => PathProfile.RepositoryPathR,
			FileSystemPathType.Absolute => RepositoryPath,
			_ => throw new NotSupportedException($"{pathType}")
		};

	public string GetInternalResourceFolderPath(InternalResourceType internalResourceType, FileSystemPathType pathType) 
		=> pathType switch {
			FileSystemPathType.Relative => GetResourceTypeFolderRelativePath(internalResourceType),
			FileSystemPathType.Absolute => Path.GetFullPath(GetResourceTypeFolderRelativePath(internalResourceType), RepositoryPath),
			_ => throw new NotSupportedException($"{pathType}")
		};

	public string GetThemePath(string themeName, FileSystemPathType pathType)
		=> Path.Join( GetInternalResourceFolderPath(InternalResourceType.Themes, pathType), themeName);

	public string GetResourceTypeFolderPath(LocalNotionResourceType resourceType, FileSystemPathType pathType)
		=> resourceType switch {
			LocalNotionResourceType.File => pathType switch {
				FileSystemPathType.Relative => PathProfile.FilesPathR,
				FileSystemPathType.Absolute => Path.GetFullPath(PathProfile.FilesPathR, RepositoryPath),
				_ => throw new NotSupportedException($"{pathType}")
			},
			LocalNotionResourceType.Page => pathType switch {
				FileSystemPathType.Relative => PathProfile.PagesPathR,
				FileSystemPathType.Absolute => Path.GetFullPath(PathProfile.PagesPathR, RepositoryPath),
				_ => throw new NotSupportedException($"{pathType}")
			},
			LocalNotionResourceType.Database => pathType switch {
				FileSystemPathType.Relative => PathProfile.DatabasesPathR,
				FileSystemPathType.Absolute => Path.GetFullPath(PathProfile.DatabasesPathR, RepositoryPath),
				_ => throw new NotSupportedException($"{pathType}")
			},
			LocalNotionResourceType.Workspace => pathType switch {
				FileSystemPathType.Relative => PathProfile.WorkspacePathR,
				FileSystemPathType.Absolute => Path.GetFullPath(PathProfile.WorkspacePathR, RepositoryPath),
				_ => throw new NotSupportedException($"{pathType}")
			},
			LocalNotionResourceType.CMS => pathType switch {
				FileSystemPathType.Relative => PathProfile.CMSPathR,
				FileSystemPathType.Absolute => Path.GetFullPath(PathProfile.CMSPathR, RepositoryPath),
				_ => throw new NotSupportedException($"{pathType}")
			},
			_ => throw new ArgumentOutOfRangeException(nameof(resourceType), resourceType, null)
		};

	public bool UsesObjectIDSubFolders(LocalNotionResourceType resourceType)
		=> resourceType switch {
			LocalNotionResourceType.File => PathProfile.UseFileIDFolders,
			LocalNotionResourceType.Page => PathProfile.UsePageIDFolders,
			LocalNotionResourceType.Database => PathProfile.UseDatabaseIDFolders,
			LocalNotionResourceType.Workspace => PathProfile.UseWorkspaceIDFolders,
			LocalNotionResourceType.CMS => false,
			_ => throw new NotSupportedException(resourceType.ToString())
		};

	public string GetResourceFolderPath(LocalNotionResourceType resourceType, string resourceID, FileSystemPathType pathType) {
		Guard.ArgumentNotNull(resourceID, nameof(resourceID));
		if (resourceType != LocalNotionResourceType.CMS)
			Guard.Argument(LocalNotionHelper.TryCovertObjectIdToGuid(resourceID, out _), nameof(resourceID), "Invalid format");
		var path = GetResourceTypeFolderPath(resourceType, pathType);
		if (UsesObjectIDSubFolders(resourceType))
			path = Path.Combine(path, resourceID);

		return path;
	}

	public string CalculateResourceFilePath(LocalNotionResourceType resourceType, string resourceID, string resourceTitle, RenderType renderType, FileSystemPathType pathType) {
		if (resourceType == LocalNotionResourceType.CMS && string.IsNullOrWhiteSpace(resourceID) ) {
			resourceID = "index";
		}
		resourceTitle = resourceTitle.ToValueWhenNullOrEmpty(Constants.DefaultResourceTitle);
		var folderPath = Path.Combine(GetResourceFolderPath(resourceType, resourceID, pathType));
		var title = resourceType switch {
			LocalNotionResourceType.File => Path.GetFileNameWithoutExtension(resourceTitle),
			LocalNotionResourceType.CMS =>  resourceID.ToLowerInvariant().ReplaceMany([("/", "_"), ("#", "_"), ("-","_")]),
			_ => resourceTitle,
		};
		var ext = resourceType switch {
			LocalNotionResourceType.File => Path.GetExtension(resourceTitle),
			_ => renderType.GetAttribute<FileExtensionAttribute>().FileExtension,
		};

		var filename = $"{Tools.FileSystem.ToValidFolderOrFilename(title)}.{ext.TrimStart('.')}";
		return Path.Combine(folderPath, filename);
	}

	public string ResolveConflictingFilePath(string filepath) {
		Guard.ArgumentNotNullOrEmpty(filepath, nameof(filepath));

		if (!File.Exists(filepath))
			return filepath;

		var parentFolderPath = Path.GetDirectoryName(filepath);
		var fileName = Path.GetFileName(filepath);

		for (var attempt = 1; attempt < int.MaxValue; attempt++) {
			var conflictFree = Path.Combine(parentFolderPath, $"[LN {attempt}] {fileName}");
			if (!File.Exists(conflictFree))
				return conflictFree;
		}
		throw new InvalidOperationException($"Exhausted all candidates for conflict-free file path: {filepath}");
	}

	public string RemoveConflictResolutionFromFilePath(string filepath) {
		if (string.IsNullOrWhiteSpace(filepath))
			return filepath;
		Tools.FileSystem.SplitFilePath(filepath, out var folder, out var fileName);
		if (fileName.StartsWith("[LN ")) {
			var parts = fileName.Split(' ', StringSplitOptions.RemoveEmptyEntries);
			if (parts.Length >= 2)
				fileName = string.Join(' ', parts.Skip(2));
		}
		return Path.Combine(folder, fileName);
	}

	public string GetRemoteHostedBaseUrl() => PathProfile.BaseUrl;
	
	public static string ResolveDefaultRegistryFilePath() 
		=> ResolveDefaultRegistryFilePath(Environment.CurrentDirectory);

	public static string ResolveDefaultRegistryFilePath(string repoPath) {
		Guard.ArgumentNotNull(repoPath, nameof(repoPath));
		Guard.DirectoryExists(repoPath);
		
		return Path.Join(repoPath, LocalNotionPathProfile.Default.RegistryPathR).ToUnixPath();
	}

	private string GetResourceTypeFolderRelativePath(InternalResourceType internalResourceType)
		=> internalResourceType switch {
			InternalResourceType.Objects => PathProfile.ObjectsPathR,
			InternalResourceType.Graphs => PathProfile.GraphsPathR,
			InternalResourceType.Themes => PathProfile.ThemesPathR,
			InternalResourceType.Logs => PathProfile.LogsPathR,
			_ => throw new NotSupportedException($"{internalResourceType}")
		};
}