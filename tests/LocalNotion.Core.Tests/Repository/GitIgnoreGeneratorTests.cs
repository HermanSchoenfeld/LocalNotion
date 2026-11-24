// Copyright (c) Herman Schoenfeld 2018 - Present. All rights reserved. (https://sphere10.com/products/localnotion)
// Author: Herman Schoenfeld <herman@sphere10.com>
//
// Distributed under the GPLv3 software license, see the accompanying file LICENSE
// or visit https://github.com/HermanSchoenfeld/localnotion/blob/master/LICENSE
//
// This notice must not be removed when duplicating this file or its contents, in whole or in part.

using System;
using System.Linq;
using LocalNotion.Core;
using NUnit.Framework;

namespace LocalNotion.Core.Tests;

[TestFixture]
[Parallelizable(ParallelScope.Children)]
public class GitIgnoreGeneratorTests {

	[Test]
	public void TheRegistryAndSyncedStateAreIgnoredWhileThemesAreKept() {
		var lines = Lines(GitIgnoreGenerator.Generate(new LocalNotionPathProfile()));
		Assert.That(lines, Is.SupersetOf(new[] {
			"/.localnotion/registry.json", "/.localnotion/objects/", "/.localnotion/graphs/", "/.localnotion/properties/", "/.localnotion/logs/",
			"/.localnotion/nginx/logs/"
		}));
		Assert.That(lines.Any(line => line.Contains("themes") && !line.StartsWith("#")), Is.False, "Themes stay in git.");
		Assert.That(lines.Any(line => line == "/.localnotion/" || line == "/.localnotion/nginx/"), Is.False, "The NGINX configuration hosts serve from stays in git.");
	}

	[Test]
	public void OverriddenPathsAreIgnoredWhereTheyAreAndPathsOutsideTheRepositoryAreSkipped() {
		var profile = new LocalNotionPathProfile { ObjectsPathR = @"state\objects", GraphsPathR = "../shared/graphs" };
		var lines = Lines(GitIgnoreGenerator.Generate(profile));
		Assert.That(lines, Does.Contain("/state/objects/"));
		Assert.That(lines.Any(line => line.Contains("shared/graphs")), Is.False);
	}

	private static string[] Lines(string text) => text.Split(new[] { "\r\n", "\n" }, StringSplitOptions.None);
}
