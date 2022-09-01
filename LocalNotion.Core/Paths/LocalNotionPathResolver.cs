using Hydrogen;

namespace LocalNotion.Core;

public class LocalNotionPathResolver : ILocalNotionPathResolver {

	public LocalNotionPathResolver(string repoPath, LocalNotionRepositoryPathProfile pathProfile) {
		Guard.ArgumentNotNull(repoPath, nameof(repoPath));
		Guard.ArgumentNotNull(pathProfile, nameof(pathProfile));
		Guard.DirectoryExists(repoPath);
		RepositoryPath = Tools.FileSystem.GetCaseCorrectDirectoryPath(repoPath);
		PathProfile = pathProfile;
		Guard.Argument(Path.GetFullPath(pathProfile.RepositoryPathR, repoPath) == repoPath, nameof(pathProfile), $"Path profile does not resolve to '{repoPath}'");
	}

	protected string RepositoryPath { get; }

	protected LocalNotionRepositoryPathProfile PathProfile { get; }

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

	public string GetObjectsFolderPath(FileSystemPathType pathType)
		=> pathType switch {
			FileSystemPathType.Relative => PathProfile.ObjectsPathR,
			FileSystemPathType.Absolute => Path.GetFullPath(PathProfile.ObjectsPathR, RepositoryPath),
			_ => throw new NotSupportedException($"{pathType}")
		};

	public string GetGraphsFolderPath(FileSystemPathType pathType)
		=> pathType switch {
			FileSystemPathType.Relative => PathProfile.GraphsPathR,
			FileSystemPathType.Absolute => Path.GetFullPath(PathProfile.GraphsPathR, RepositoryPath),
			_ => throw new NotSupportedException($"{pathType}")
		};

	public string GetThemesFolderPath(FileSystemPathType pathType)
		=> pathType switch {
			FileSystemPathType.Relative => PathProfile.ThemesPathR,
			FileSystemPathType.Absolute => Path.GetFullPath(PathProfile.ThemesPathR, RepositoryPath),
			_ => throw new NotSupportedException($"{pathType}")
		};

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
			_ => throw new ArgumentOutOfRangeException(nameof(resourceType), resourceType, null)
		};

	public string GetLogsFolderPath(FileSystemPathType pathType)
		=> pathType switch {
			FileSystemPathType.Relative => PathProfile.RepositoryPathR,
			FileSystemPathType.Absolute => Path.GetFullPath(PathProfile.RepositoryPathR, RepositoryPath),
			_ => throw new NotSupportedException($"{pathType}")
		};

	public bool UsesObjectIDSubFolders(LocalNotionResourceType resourceType)
		=> resourceType switch {
			LocalNotionResourceType.File => PathProfile.UseFileIDFolders,
			LocalNotionResourceType.Page => PathProfile.UsePageIDFolders,
			LocalNotionResourceType.Database => PathProfile.UseDatabaseIDFolders,
			LocalNotionResourceType.Workspace => PathProfile.UseWorkspaceIDFolders,
			_ => throw new NotSupportedException(resourceType.ToString())
		};

	public string GetResourceFolderPath(LocalNotionResourceType resourceType, string resourceId, FileSystemPathType pathType) {
		Guard.ArgumentNotNull(resourceId, nameof(resourceId));
		Guard.Argument(LocalNotionHelper.TryCovertObjectIdToGuid(resourceId, out _), nameof(resourceId), "Invalid format");
		var path = GetResourceTypeFolderPath(resourceType, pathType);
		if (UsesObjectIDSubFolders(resourceType))
			path = Path.Combine(path, resourceId);

		return path;
	}

	public string CalculateResourceFilePath(LocalNotionResourceType resourceType, string resourceID, string resourceTitle, RenderType renderType, FileSystemPathType pathType) {
		resourceTitle = resourceTitle.ToValueWhenNullOrEmpty(Constants.DefaultResourceTitle);
		var folderPath = Path.Combine(GetResourceFolderPath(resourceType, resourceID, pathType));
		var title = resourceType switch {
			LocalNotionResourceType.File => Path.GetFileNameWithoutExtension(resourceTitle),
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

	public string GetRemoteHostedBaseUrl() => PathProfile.BaseUrl;


}