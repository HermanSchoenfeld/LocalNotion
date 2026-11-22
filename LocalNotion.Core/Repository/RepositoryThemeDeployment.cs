// Copyright (c) Herman Schoenfeld 2018 - Present. All rights reserved. (https://sphere10.com/products/localnotion)
// Author: Herman Schoenfeld <herman@sphere10.com>
//
// Distributed under the GPLv3 software license, see the accompanying file LICENSE
// or visit https://github.com/HermanSchoenfeld/localnotion/blob/master/LICENSE
//
// This notice must not be removed when duplicating this file or its contents, in whole or in part.

using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.Extensions.FileProviders;
using Sphere10.Framework;

namespace LocalNotion.Core;

internal static class RepositoryThemeDeployment {
	private static readonly ManifestEmbeddedFileProvider _cmsThemes = new(typeof(RepositoryThemeDeployment).Assembly, "CMS/Themes");

	internal static async Task DeployMissing(string themesDirectory, string repositoryDirectory) {
		// The renderer supplies generic templates; LocalNotion supplies its CMS presentation presets.
		await DeployEmbeddedThemes(themesDirectory);
		await DeployDirectory(_cmsThemes, "", themesDirectory);
		foreach (var theme in _cmsThemes.GetDirectoryContents("/")) {
			if (!theme.IsDirectory || !_cmsThemes.GetDirectoryContents(theme.Name + "/resources").Exists)
				continue;
			// Local CMS themes use /themes/<name>; private templates remain under the configured theme path.
			await DeployDirectory(_cmsThemes, theme.Name + "/resources", Path.Combine(repositoryDirectory, "themes", theme.Name, "resources"));
		}
	}

	private static async Task DeployEmbeddedThemes(string themesDirectory) {
		using var archive = Sphere10.Framework.Web.VisualRenderer.EmbeddedThemes.OpenArchive();
		foreach (var entry in archive.Entries) {
			if (entry.Name.Length == 0)
				continue;
			var destination = Path.Combine(themesDirectory, entry.FullName);
			await DeployFile(entry.Open, destination);
		}
	}

	private static async Task DeployDirectory(IFileProvider provider, string sourceDirectory, string destinationDirectory) {
		Directory.CreateDirectory(destinationDirectory);
		foreach (var entry in provider.GetDirectoryContents(string.IsNullOrEmpty(sourceDirectory) ? "/" : sourceDirectory)) {
			var destination = Path.Combine(destinationDirectory, entry.Name);
			if (entry.IsDirectory) {
				var source = string.IsNullOrEmpty(sourceDirectory) ? entry.Name : sourceDirectory + "/" + entry.Name;
				await DeployDirectory(provider, source, destination);
				continue;
			}
			await DeployFile(entry.CreateReadStream, destination);
		}
	}

	private static async Task DeployFile(Func<Stream> openSource, string destination) {
		if (File.Exists(destination))
			return;
		var directory = Path.GetDirectoryName(destination);
		Directory.CreateDirectory(directory);
		// Publish complete files atomically and preserve user overrides, including empty files.
		var temporaryFile = Path.Combine(directory, ".localnotion-theme-" + Guid.NewGuid().ToString("N") + ".tmp");
		using var cleanup = new ActionScope(() => File.Delete(temporaryFile));
		using (var source = openSource()) {
			await using var output = new FileStream(temporaryFile, FileMode.CreateNew, FileAccess.Write, FileShare.None, 81920, useAsync: true);
			await source.CopyToAsync(output);
		}
		try {
			File.Move(temporaryFile, destination);
		} catch (IOException) when (File.Exists(destination)) {
			// Another opener or the user supplied the destination during extraction.
		}
	}
}
