// Copyright (c) Herman Schoenfeld 2018 - Present. All rights reserved. (https://sphere10.com/products/localnotion)
// Author: Herman Schoenfeld <herman@sphere10.com>
//
// Distributed under the GPLv3 software license, see the accompanying file LICENSE
// or visit https://github.com/HermanSchoenfeld/localnotion/blob/master/LICENSE
//
// This notice must not be removed when duplicating this file or its contents, in whole or in part.

using System;
using System.Collections.Generic;
using System.Linq;
using Sphere10.Framework;

namespace LocalNotion.Core;

/// <summary>
/// Writes the .gitignore of a new git-enabled repository. Local Notion's own state (the registry, downloaded objects, graphs, properties and
/// logs) is rebuilt by a sync and stays out of git; the themes and rendered content are kept, as is the NGINX configuration hosts serve from.
/// </summary>
public static class GitIgnoreGenerator {

	public static string Generate(LocalNotionPathProfile pathProfile) {
		Guard.ArgumentNotNull(pathProfile, nameof(pathProfile));
		var lines = new List<string> {
			"# Local Notion state, rebuilt by a sync; themes are kept",
			Entry(pathProfile.RegistryPathR, false),
			Entry(pathProfile.ObjectsPathR, true),
			Entry(pathProfile.GraphsPathR, true),
			Entry(pathProfile.PropertiesPathR, true),
			Entry(pathProfile.LogsPathR, true),
			string.Empty,
			"# NGINX logs",
			Entry(Constants.DefaultRegistryFolderName + "/nginx/logs", true)
		};
		return string.Join(Environment.NewLine, lines.Where(line => line != null)) + Environment.NewLine;
	}

	// Paths are relative to the repository; one outside it (or the repository itself) has nothing to ignore here.
	private static string Entry(string relativePath, bool folder) {
		if (string.IsNullOrWhiteSpace(relativePath))
			return null;
		var path = relativePath.Replace('\\', '/').Trim('/');
		if (path.Length == 0 || path == "." || path.StartsWith("../", StringComparison.Ordinal) || path == "..")
			return null;
		return "/" + path + (folder ? "/" : string.Empty);
	}
}
