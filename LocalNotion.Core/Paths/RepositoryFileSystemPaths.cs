// Copyright (c) Herman Schoenfeld 2018 - Present. All rights reserved. (https://sphere10.com/products/localnotion)
// Author: Herman Schoenfeld <herman@sphere10.com>
//
// Distributed under the GPLv3 software license, see the accompanying file LICENSE
// or visit https://github.com/HermanSchoenfeld/localnotion/blob/master/LICENSE
//
// This notice must not be removed when duplicating this file or its contents, in whole or in part.

namespace LocalNotion.Core;

/// <summary>
/// Makes persisted relative filesystem paths usable on both Windows and Unix.
/// This does not remap absolute paths or validate container mount boundaries.
/// </summary>
internal static class RepositoryFileSystemPaths {
	internal static void Normalize(LocalNotionRegistry registry) {
		Normalize(registry.Paths);

		foreach (var resource in registry.Resources) {
			if (resource.Renders is null)
				continue;
			foreach (var render in resource.Renders.Values) {
				if (render is not null)
					render.LocalPath = NormalizeRelativePath(render.LocalPath);
			}
		}

		if (registry.CMSItemsBySlug is not null) {
			foreach (var item in registry.CMSItemsBySlug.Values)
				item.RenderPath = NormalizeRelativePath(item.RenderPath);
		}
	}

	internal static void Normalize(LocalNotionPathProfile profile) {
		if (profile is null)
			return;

		profile.RepositoryPathR = NormalizeRelativePath(profile.RepositoryPathR);
		profile.RegistryPathR = NormalizeRelativePath(profile.RegistryPathR);
		profile.ObjectsPathR = NormalizeRelativePath(profile.ObjectsPathR);
		profile.GraphsPathR = NormalizeRelativePath(profile.GraphsPathR);
		profile.PropertiesPathR = NormalizeRelativePath(profile.PropertiesPathR);
		profile.ThemesPathR = NormalizeRelativePath(profile.ThemesPathR);
		profile.FilesPathR = NormalizeRelativePath(profile.FilesPathR);
		profile.DatabasesPathR = NormalizeRelativePath(profile.DatabasesPathR);
		profile.WorkspacePathR = NormalizeRelativePath(profile.WorkspacePathR);
		profile.CMSPathR = NormalizeRelativePath(profile.CMSPathR);
		profile.PagesPathR = NormalizeRelativePath(profile.PagesPathR);
		profile.LogsPathR = NormalizeRelativePath(profile.LogsPathR);
	}

	private static string NormalizeRelativePath(string path) {
		// Check both platforms' root syntax explicitly: Path.IsPathRooted alone
		// treats drive letters and backslashes as relative filenames on Unix.
		// Colons also exclude URI and drive-relative syntax from conversion.
		if (string.IsNullOrEmpty(path) || path[0] is '/' or '\\' || path.Contains(':'))
			return path;
		return path.Replace('\\', '/');
	}
}
