// Copyright (c) Herman Schoenfeld 2018 - Present. All rights reserved. (https://sphere10.com/products/localnotion)
// Author: Herman Schoenfeld <herman@sphere10.com>
//
// Distributed under the GPLv3 software license, see the accompanying file LICENSE
// or visit https://github.com/HermanSchoenfeld/localnotion/blob/master/LICENSE
//
// This notice must not be removed when duplicating this file or its contents, in whole or in part.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using LocalNotion.Core;
using Microsoft.Extensions.FileProviders;
using NUnit.Framework;
using Sphere10.Framework;

namespace LocalNotion.Core.Tests;

[TestFixture]
[Category("Integration")]
[Parallelizable(ParallelScope.Children)]
[FixtureLifeCycle(LifeCycle.InstancePerTestCase)]
public class RepositoryThemeDeploymentTests {
	private string _fixtureRoot;

	[SetUp]
	public void SetUp() {
		_fixtureRoot = Path.Combine(Path.GetTempPath(), "localnotion-theme-deployment-" + Guid.NewGuid().ToString("N"));
		Directory.CreateDirectory(_fixtureRoot);
	}

	[TearDown]
	public void TearDown() {
		Assert.That(Path.GetDirectoryName(Path.GetFullPath(_fixtureRoot)), Is.EqualTo(Path.TrimEndingDirectorySeparator(Path.GetFullPath(Path.GetTempPath()))));
		Assert.That(Path.GetFileName(_fixtureRoot), Does.StartWith("localnotion-theme-deployment-"));
		if (Directory.Exists(_fixtureRoot))
			Directory.Delete(_fixtureRoot, recursive: true);
	}

	[Test]
	public async Task CreateDeploysCompleteEmbeddedThemeTree([Values] bool customPath, [Values] bool cmsRepository) {
		var profile = new LocalNotionPathProfile { ThemesPathR = customPath ? @"custom themes\nested" : ".localnotion/themes" };
		using var repository = await LocalNotionRepository.CreateNew(_fixtureRoot, pathProfile: profile, cmsDatabaseID: cmsRepository ? Guid.NewGuid().ToString("N") : null);
		var themesDirectory = repository.Paths.GetInternalResourceFolderPath(InternalResourceType.Themes, FileSystemPathType.Absolute);
		var expectedDirectory = Path.Combine(_fixtureRoot, customPath ? "custom themes/nested" : ".localnotion/themes");
		Assert.That(themesDirectory, Is.EqualTo(Path.GetFullPath(expectedDirectory)));
		var embeddedFiles = ReadEmbeddedFiles();
		var deployedFiles = Directory.EnumerateFiles(themesDirectory, "*", SearchOption.AllDirectories)
			.Select(path => Path.GetRelativePath(themesDirectory, path).Replace('\\', '/')).ToArray();
		Assert.That(deployedFiles, Is.EquivalentTo(embeddedFiles.Keys), "Every built-in template, config, script, style and font must be available on disk.");
		foreach (var file in embeddedFiles)
			Assert.That(await File.ReadAllBytesAsync(Path.Combine(themesDirectory, file.Key)), Is.EqualTo(file.Value), file.Key);
		if (customPath)
			Assert.That(Directory.Exists(Path.Combine(_fixtureRoot, ".localnotion", "themes")), Is.False, "Deployment must follow the configured path.");
	}

	[TestCase("default/paragraph.html")]
	[TestCase("cms/resources/js/feather.min.js")]
	[TestCase("default/.config.json")]
	[TestCase("navbar_refined/page_content.html")]
	[TestCase("navbar_bg_aux/resources/css/navbar-palette-aux.css")]
	public async Task ReopeningRepairsMissingFilesAndPreservesOverrides(string missingFile) {
		string themesDirectory;
		using (var repository = await LocalNotionRepository.CreateNew(_fixtureRoot, pathProfile: new LocalNotionPathProfile { ThemesPathR = "custom themes" }))
			themesDirectory = repository.Paths.GetInternalResourceFolderPath(InternalResourceType.Themes, FileSystemPathType.Absolute);
		var removedPath = Path.Combine(themesDirectory, missingFile);
		var expected = await File.ReadAllBytesAsync(removedPath);
		File.Delete(removedPath);
		var overriddenPath = Path.Combine(themesDirectory, "default", "heading_1.html");
		var emptyOverridePath = Path.Combine(themesDirectory, "default", "heading_2.html");
		await File.WriteAllTextAsync(overriddenPath, "<h1>Customized theme</h1>");
		await File.WriteAllTextAsync(emptyOverridePath, "");
		var timestamp = new DateTime(2021, 2, 3, 4, 5, 6, DateTimeKind.Utc);
		File.SetLastWriteTimeUtc(overriddenPath, timestamp);
		using var reopened = await LocalNotionRepository.Open(_fixtureRoot);
		Assert.That(await File.ReadAllBytesAsync(removedPath), Is.EqualTo(expected), "The missing file is restored from its assembly resource.");
		Assert.That(await File.ReadAllTextAsync(overriddenPath), Is.EqualTo("<h1>Customized theme</h1>"));
		Assert.That(File.GetLastWriteTimeUtc(overriddenPath), Is.EqualTo(timestamp), "Existing files must not be rewritten.");
		Assert.That(await File.ReadAllBytesAsync(emptyOverridePath), Is.Empty, "An empty file is still an intentional override.");
		Assert.That(reopened.RequiresSave, Is.False, "Theme repair must not change the registry.");
	}

	[Test]
	public async Task CMSMenuAssetsUseRepositoryRootAndPreservePublicOverrides() {
		var profile = new LocalNotionPathProfile { ThemesPathR = "custom themes/nested" };
		using (var repository = await LocalNotionRepository.CreateNew(_fixtureRoot, pathProfile: profile)) {
			var files = ReadEmbeddedFiles().Where(entry => entry.Key.StartsWith("navbar_refined/resources/", StringComparison.Ordinal)
				|| entry.Key.StartsWith("navbar_bg_aux/resources/", StringComparison.Ordinal)).ToArray();
			Assert.That(files.Length, Is.EqualTo(4), "The menu CSS, JavaScript and license must all be available at their online URLs.");
			foreach (var file in files)
				Assert.That(await File.ReadAllBytesAsync(Path.Combine(_fixtureRoot, "themes", file.Key)), Is.EqualTo(file.Value), file.Key);
		}

		// Public resources and private renderer definitions are independent override owners.
		var css = Path.Combine(_fixtureRoot, "themes", "navbar_refined", "resources", "css", "navbar-refined.css");
		var script = Path.Combine(_fixtureRoot, "themes", "navbar_refined", "resources", "js", "navbar-refined.js");
		var missing = Path.Combine(_fixtureRoot, "themes", "navbar_bg_aux", "resources", "css", "navbar-palette-aux.css");
		var expected = await File.ReadAllBytesAsync(missing);
		await File.WriteAllTextAsync(css, "/* User menu palette */");
		await File.WriteAllTextAsync(script, "");
		var timestamp = new DateTime(2021, 2, 3, 4, 5, 6, DateTimeKind.Utc);
		File.SetLastWriteTimeUtc(css, timestamp);
		File.Delete(missing);
		using var reopened = await LocalNotionRepository.Open(_fixtureRoot);
		Assert.That(await File.ReadAllTextAsync(css), Is.EqualTo("/* User menu palette */"));
		Assert.That(File.GetLastWriteTimeUtc(css), Is.EqualTo(timestamp));
		Assert.That(await File.ReadAllBytesAsync(script), Is.Empty);
		Assert.That(await File.ReadAllBytesAsync(missing), Is.EqualTo(expected));
		Assert.That(reopened.RequiresSave, Is.False);
	}

	[TestCase("navbar_refined")]
	[TestCase("navbar_standard")]
	public async Task FreshRepositoryRendersAuxiliaryMenuWithoutExternalTemplateInstallation(string layout) {
		using var repository = await LocalNotionRepository.CreateNew(_fixtureRoot);
		var themesDirectory = repository.Paths.GetInternalResourceFolderPath(InternalResourceType.Themes, FileSystemPathType.Absolute);
		var renderer = new Sphere10.Framework.Web.VisualRenderer.HtmlRenderer(new Sphere10.Framework.Web.VisualRenderer.ThemeCatalog(new Sphere10.Framework.Web.VisualRenderer.ThemeOptions { ThemesDirectory = themesDirectory }));
		var result = renderer.Render(new Sphere10.Framework.Web.VisualRenderer.DocumentBlock {
			RenderFrame = false,
			Themes = ["cms_navbar", layout, "color_aux_primary", "navbar_bg_aux"]
		}, new Sphere10.Framework.Web.VisualRenderer.RenderOptions { Environment = Sphere10.Framework.Web.VisualRenderer.RenderEnvironment.Online });
		Assert.That(result.Html, Does.Contain("ln-navbar-palette-aux text-bg-primary").And.Contain("aria-label=\"Main navigation\""));
		Assert.That(result.Html, Does.Contain("/themes/navbar_bg_aux/resources/css/navbar-palette-aux.css?v="));
		Assert.That(File.Exists(Path.Combine(_fixtureRoot, "themes", "navbar_bg_aux", "resources", "css", "navbar-palette-aux.css")), Is.True);
	}

	[Test]
	public async Task OpeningExistingRegistryDeploysMissingThemeDirectory() {
		string themesDirectory;
		string registryPath;
		using (var repository = await LocalNotionRepository.CreateNew(_fixtureRoot)) {
			themesDirectory = repository.Paths.GetInternalResourceFolderPath(InternalResourceType.Themes, FileSystemPathType.Absolute);
			registryPath = repository.Paths.GetRegistryFilePath(FileSystemPathType.Absolute);
		}
		Assert.That(Path.GetFullPath(themesDirectory), Is.EqualTo(Path.GetFullPath(Path.Combine(_fixtureRoot, ".localnotion", "themes"))));
		Directory.Delete(themesDirectory, recursive: true);
		var registryBytes = await File.ReadAllBytesAsync(registryPath);
		using var reopened = await LocalNotionRepository.OpenRegistry(registryPath);
		Assert.That(File.Exists(Path.Combine(themesDirectory, "default", "paragraph.html")), Is.True);
		Assert.That(File.Exists(Path.Combine(themesDirectory, "cms", "resources", "js", "feather.min.js")), Is.True);
		Assert.That(await File.ReadAllBytesAsync(registryPath), Is.EqualTo(registryBytes));
	}

	[Test]
	public async Task ExplicitLoadPreservesPreexistingReadOnlyOverride() {
		string registryPath;
		string overridePath;
		using (var repository = await LocalNotionRepository.CreateNew(_fixtureRoot)) {
			registryPath = repository.Paths.GetRegistryFilePath(FileSystemPathType.Absolute);
			overridePath = Path.Combine(repository.Paths.GetInternalResourceFolderPath(InternalResourceType.Themes, FileSystemPathType.Absolute), "default", "paragraph.html");
		}
		await File.WriteAllTextAsync(overridePath, "Read-only user template");
		File.SetAttributes(overridePath, FileAttributes.ReadOnly);
		try {
			using var repository = new LocalNotionRepository(registryPath);
			await repository.LoadAsync();
			Assert.That(await File.ReadAllTextAsync(overridePath), Is.EqualTo("Read-only user template"));
		} finally {
			File.SetAttributes(overridePath, FileAttributes.Normal);
		}
	}

	private static Dictionary<string, byte[]> ReadEmbeddedFiles() {
		var files = new Dictionary<string, byte[]>(StringComparer.Ordinal);
		using var archive = Sphere10.Framework.Web.VisualRenderer.EmbeddedThemes.OpenArchive();
		foreach (var entry in archive.Entries) {
			if (entry.Name.Length == 0)
				continue;
			using var source = entry.Open();
			using var buffer = new MemoryStream();
			source.CopyTo(buffer);
			files.Add(entry.FullName, buffer.ToArray());
		}
		ReadDirectory(new ManifestEmbeddedFileProvider(typeof(CMSDatabaseTemplate).Assembly, "CMS/Themes"), "");
		return files;

		void ReadDirectory(IFileProvider provider, string directory) {
			foreach (var entry in provider.GetDirectoryContents(string.IsNullOrEmpty(directory) ? "/" : directory)) {
				var path = string.IsNullOrEmpty(directory) ? entry.Name : directory + "/" + entry.Name;
				if (entry.IsDirectory) {
					ReadDirectory(provider, path);
					continue;
				}
				using var source = entry.CreateReadStream();
				using var buffer = new MemoryStream();
				source.CopyTo(buffer);
				// Renderer templates deploy first and an existing file is never replaced, so the renderer's entry wins a shared path.
				if (!files.ContainsKey(path))
					files.Add(path, buffer.ToArray());
			}
		}
	}
}
