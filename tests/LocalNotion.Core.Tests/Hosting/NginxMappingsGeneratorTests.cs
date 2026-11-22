// Copyright (c) Herman Schoenfeld 2018 - Present. All rights reserved. (https://sphere10.com/products/localnotion)
// Author: Herman Schoenfeld <herman@sphere10.com>
//
// Distributed under the GPLv3 software license, see the accompanying file LICENSE
// or visit https://github.com/HermanSchoenfeld/localnotion/blob/master/LICENSE
//
// This notice must not be removed when duplicating this file or its contents, in whole or in part.

using System;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using LocalNotion.Core;
using NUnit.Framework;
using Sphere10.Framework;

namespace LocalNotion.Core.Tests;

[TestFixture]
[Category("Integration")]
[Parallelizable(ParallelScope.Children)]
[FixtureLifeCycle(LifeCycle.InstancePerTestCase)]
public class NginxMappingsGeneratorTests {

	private string _fixtureRoot;
	private string _cmsPath;
	private CMSLocalNotionRepository _repository;
	private NginxMappingsGenerator _generator;

	[SetUp]
	public async Task SetUp() {
		_fixtureRoot = Path.Combine(Path.GetTempPath(), "localnotion-nginx-cleanup-" + Guid.NewGuid().ToString("N"));
		Directory.CreateDirectory(_fixtureRoot);
		var databaseId = Guid.NewGuid().ToString();
		_repository = (CMSLocalNotionRepository)await LocalNotionRepository.CreateNew(_fixtureRoot, cmsDatabaseID: databaseId, logger: new NoOpLogger());
		_repository.AddResource(new LocalNotionDatabase { ID = databaseId, Name = "example.invalid", Title = "CMS", PrimaryDataSourceID = Guid.NewGuid().ToString() });
		_cmsPath = _repository.Paths.GetResourceTypeFolderPath(LocalNotionResourceType.CMS, FileSystemPathType.Absolute);
		_generator = new NginxMappingsGenerator(_repository);
	}

	[TearDown]
	public void TearDown() {
		try {
			_repository?.Dispose();
		} finally {
			var fullPath = Path.GetFullPath(_fixtureRoot);
			if (!fullPath.StartsWith(Path.GetFullPath(Path.GetTempPath()), StringComparison.OrdinalIgnoreCase))
				throw new InvalidOperationException("Fixture path escaped its temporary parent.");
			if (Directory.Exists(fullPath))
				Directory.Delete(fullPath, recursive: true);
		}
	}

	[TestCase("=", "/.localnotion")]
	[TestCase("^~", "/.localnotion/")]
	[TestCase("=", "/.git")]
	[TestCase("^~", "/.git/")]
	public async Task GeneratedConfigurationDeniesPrivatePathsBeforeRouteMappings(string modifier, string privatePath) {
		await _generator.GenerateNGinxFolder();
		var configuration = await File.ReadAllTextAsync(Path.Combine(_generator.CalculateNGinxFolderPath(), "conf", "nginx.conf"));
		var locationPattern = @"^\s*location\s+" + Regex.Escape(modifier) + @"\s+" + Regex.Escape(privatePath) + @"\s*\{\s*return\s+404;\s*\}";
		var locations = Regex.Matches(configuration, locationPattern, RegexOptions.Multiline);
		Assert.That(locations.Count, Is.EqualTo(1), "Both the directory itself and its contents must be denied.");
		var mappingsInclude = configuration.IndexOf("include ln-urls.conf;", StringComparison.Ordinal);
		Assert.That(mappingsInclude, Is.GreaterThan(locations[0].Index), "Private-directory restrictions belong before the generated route mappings.");
	}

	[Test]
	public async Task GenerateNginxFolderPreservesExistingConfigurationAndMimeTypes() {
		await _generator.GenerateNGinxFolder();
		var configurationPath = Path.Combine(_generator.CalculateNGinxFolderPath(), "conf", "nginx.conf");
		var mimeTypesPath = Path.Combine(_generator.CalculateNGinxFolderPath(), "conf", "mime.types");
		const string customConfiguration = "# User-owned Nginx configuration\n";
		const string customMimeTypes = "types { text/plain custom; }\n";
		await File.WriteAllTextAsync(configurationPath, customConfiguration);
		await File.WriteAllTextAsync(mimeTypesPath, customMimeTypes);
		await _generator.GenerateNGinxFolder();
		Assert.That(await File.ReadAllTextAsync(configurationPath), Is.EqualTo(customConfiguration));
		Assert.That(await File.ReadAllTextAsync(mimeTypesPath), Is.EqualTo(customMimeTypes));
	}

	[TestCase("cms/keep.html")]
	[TestCase("./cms/keep.html")]
	[TestCase("cms/../cms/keep.html")]
	public async Task CleanupPreservesRegisteredRenderWithEquivalentPath(string renderPath) {
		var renderFile = CreateRender("keep.html", "registered render");
		RegisterRender(renderPath);
		await _generator.RemoveOldCmsRenders();
		Assert.That(File.Exists(renderFile), Is.True, "A registered render must survive hosting-file generation.");
		Assert.That(await File.ReadAllTextAsync(renderFile), Is.EqualTo("registered render"));
	}

	[TestCase(@"cms\keep.html")]
	[TestCase(@"cms\.\keep.html")]
	[TestCase(@"cms\../cms/keep.html")]
	[Platform("Win")]
	public async Task CleanupPreservesRegisteredRenderWithWindowsSeparators(string renderPath) {
		var renderFile = CreateRender("keep.html");
		RegisterRender(renderPath);
		await _generator.RemoveOldCmsRenders();
		Assert.That(File.Exists(renderFile), Is.True);
	}

	[Test]
	public async Task CleanupPreservesRegisteredAbsolutePath() {
		var renderFile = CreateRender("keep.html");
		RegisterRender(renderFile);
		await _generator.RemoveOldCmsRenders();
		Assert.That(File.Exists(renderFile), Is.True);
	}

	[Test]
	[Platform("Win")]
	public async Task CleanupComparesWindowsPathsWithoutCaseSensitivity() {
		var renderFile = CreateRender("Keep.html");
		RegisterRender("CMS/KEEP.HTML");
		await _generator.RemoveOldCmsRenders();
		Assert.That(File.Exists(renderFile), Is.True);
	}

	[Test]
	[Platform("Linux")]
	public async Task CleanupDistinguishesLinuxFileNameCase() {
		var registeredFile = CreateRender("Keep.html");
		var obsoleteFile = CreateRender("keep.html");
		RegisterRender("cms/Keep.html");
		await _generator.RemoveOldCmsRenders();
		Assert.That(File.Exists(registeredFile), Is.True);
		Assert.That(File.Exists(obsoleteFile), Is.False);
	}

	[Test]
	public async Task CleanupRemovesOnlyUnregisteredFiles() {
		var registeredFile = CreateRender("keep.html");
		var obsoleteFile = CreateRender("obsolete.html");
		var sitemapFile = CreateRender("sitemap.xml");
		RegisterRender("cms/keep.html");
		await _generator.RemoveOldCmsRenders();
		Assert.That(File.Exists(registeredFile), Is.True);
		Assert.That(File.Exists(obsoleteFile), Is.False);
		Assert.That(File.Exists(sitemapFile), Is.True);
	}

	[TestCase(null)]
	[TestCase("")]
	[TestCase(" ")]
	public async Task CleanupAllowsItemsWithoutRenders(string renderPath) {
		var obsoleteFile = CreateRender("obsolete.html");
		var sitemapFile = CreateRender("sitemap.xml");
		RegisterRender(renderPath);
		await _generator.RemoveOldCmsRenders();
		Assert.That(File.Exists(obsoleteFile), Is.False);
		Assert.That(File.Exists(sitemapFile), Is.True);
	}

	[Test]
	public async Task CleanupLeavesFilesOutsideCmsDirectoryUntouched() {
		var outsideFile = Path.Combine(_fixtureRoot, "outside.html");
		await File.WriteAllTextAsync(outsideFile, "outside CMS");
		CreateRender("obsolete.html");
		await _generator.RemoveOldCmsRenders();
		Assert.That(await File.ReadAllTextAsync(outsideFile), Is.EqualTo("outside CMS"));
	}

	[Test]
	public async Task CleanupAllowsAbsentCmsDirectory() {
		if (Directory.Exists(_cmsPath))
			Directory.Delete(_cmsPath);
		await _generator.RemoveOldCmsRenders();
		Assert.That(Directory.Exists(_cmsPath), Is.False);
	}

	[Test]
	public async Task GenerateHostingFilesKeepsMappedRendersAndDeletesObsoleteRenders() {
		var renderFile = CreateRender("keep.html", "registered render");
		var obsoleteFile = CreateRender("obsolete.html");
		RegisterRender("cms/keep.html");
		var nginxPath = await NginxMappingsGenerator.GenerateNGinxFiles(_repository);
		var mappings = await File.ReadAllTextAsync(Path.Combine(nginxPath, "conf", NginxMappingsGenerator.MappingFileName));
		Assert.That(mappings, Does.Contain("try_files \"/cms/keep.html\" =404;"));
		Assert.That(File.Exists(Path.Combine(_cmsPath, "sitemap.xml")), Is.True);
		Assert.That(File.Exists(renderFile), Is.True, "A generated Nginx mapping must retain its target file.");
		Assert.That(await File.ReadAllTextAsync(renderFile), Is.EqualTo("registered render"));
		Assert.That(File.Exists(obsoleteFile), Is.False);
	}

	[TestCase("cms/report.html", "/cms/report.html")]
	[TestCase("./cms/report.html", "/cms/report.html")]
	[TestCase("cms/../cms/report.html", "/cms/report.html")]
	[TestCase(@"cms\report.html", "/cms/report.html")]
	[TestCase("cms/private report.html", "/cms/private report.html")]
	[TestCase("cms/100% complete.html", "/cms/100% complete.html")]
	[TestCase("pages/id/Private Page.html", "/pages/id/Private Page.html")]
	[TestCase("cms/report%20.html", "/cms/report%20.html")]
	public async Task MappingProtectsFriendlyAndPhysicalCmsUrls(string renderPath, string physicalUrl) {
		var fullPath = Path.GetFullPath(renderPath.ToUnixPath(), _fixtureRoot);
		Directory.CreateDirectory(Path.GetDirectoryName(fullPath));
		await File.WriteAllTextAsync(fullPath, "protected fixture content");
		_repository.AddOrUpdateCMSItem(new CMSItem { Slug = "private/report", RenderPath = renderPath, Auth = "fixture-user:fixture-password" });
		var mappings = await GenerateMappings();
		var friendly = Locations(mappings, "/private/report");
		var physical = Locations(mappings, physicalUrl);
		Assert.That(friendly, Has.Length.EqualTo(1));
		Assert.That(physical, Has.Length.EqualTo(1), "A static-file request must not bypass the friendly URL's authentication.");
		Assert.That(physical[0], Is.EqualTo(friendly[0]), "The physical alias must use the identical credential, target and MIME policy.");
		Assert.That(physical[0], Does.Contain("if ($http_authorization != \"Basic " + "fixture-user:fixture-password".ToBase64() + "\")"));
	}

	[Test]
	public async Task MappingDoesNotChangePublicResourceUrls() {
		_repository.AddOrUpdateCMSItem(new CMSItem { Slug = "public/report", RenderPath = "cms/public.html" });
		var mappings = await GenerateMappings();
		Assert.That(Locations(mappings, "/public/report"), Has.Length.EqualTo(1));
		Assert.That(Locations(mappings, "/public/report")[0], Does.Not.Contain("$http_authorization"));
		Assert.That(Locations(mappings, "/cms/public.html"), Is.Empty);
	}

	[Test]
	public async Task MappingDoesNotDuplicateAnAlreadyProtectedPhysicalUrl() {
		_repository.AddOrUpdateCMSItem(new CMSItem { Slug = "cms/report.html", RenderPath = "cms/report.html", Auth = "fixture-user:fixture-password" });
		var mappings = await GenerateMappings();
		Assert.That(Locations(mappings, "/cms/report.html"), Has.Length.EqualTo(1));
	}

	[Test]
	public async Task MappingDeduplicatesProtectedPhysicalTargets() {
		_repository.AddOrUpdateCMSItem(new CMSItem { Slug = "private/first", RenderPath = "cms/report.html", Auth = "fixture-user:fixture-password" });
		_repository.AddOrUpdateCMSItem(new CMSItem { Slug = "private/second", RenderPath = "./cms/report.html", Auth = "fixture-user:fixture-password" });
		var mappings = await GenerateMappings();
		Assert.That(Locations(mappings, "/private/first"), Has.Length.EqualTo(1));
		Assert.That(Locations(mappings, "/private/second"), Has.Length.EqualTo(1));
		Assert.That(Locations(mappings, "/cms/report.html"), Has.Length.EqualTo(1));
	}

	[Test]
	public async Task MappingAddsDecodedPhysicalLocationDespiteEncodedFriendlySlug() {
		_repository.AddOrUpdateCMSItem(new CMSItem { Slug = "cms/Private%20Page.html", RenderPath = "cms/Private Page.html", Auth = "fixture-user:fixture-password" });
		var mappings = await GenerateMappings();
		Assert.That(Locations(mappings, "/cms/Private%20Page.html"), Has.Length.EqualTo(1));
		Assert.That(Locations(mappings, "/cms/Private Page.html"), Has.Length.EqualTo(1));
	}

	[TestCase("cms/$variable.html")]
	[TestCase("cms/$variable/../report.html")]
	[TestCase("cms/\"quote/../report.html")]
	[TestCase("cms/line\nbreak.html")]
	[TestCase("../outside.html")]
	[TestCase("cms/sitemap.xml")]
	[TestCase("sitemap.xml")]
	public async Task MappingRejectsUnsafeProtectedTargetsWithoutReplacingExistingMap(string renderPath) {
		await _generator.GenerateNGinxFolder();
		var path = Path.Combine(_generator.CalculateNGinxFolderPath(), "conf", NginxMappingsGenerator.MappingFileName);
		await File.WriteAllTextAsync(path, "previous protected mappings");
		_repository.AddOrUpdateCMSItem(new CMSItem { Slug = "private/report", RenderPath = renderPath, Auth = "fixture-user:fixture-password" });
		await Assert.ThatAsync(async () => await _generator.GenerateUrlMappingsFile(), Throws.TypeOf<InvalidOperationException>());
		Assert.That(await File.ReadAllTextAsync(path), Is.EqualTo("previous protected mappings"));
	}

	[TestCase("cms/REPORT.html")]
	[TestCase("CMS/report.html")]
	[Platform("Win")]
	public async Task MappingRejectsExistingProtectedFileWithMismatchedCase(string renderPath) {
		CreateRender("report.html");
		await _generator.GenerateNGinxFolder();
		var path = Path.Combine(_generator.CalculateNGinxFolderPath(), "conf", NginxMappingsGenerator.MappingFileName);
		await File.WriteAllTextAsync(path, "previous protected mappings");
		_repository.AddOrUpdateCMSItem(new CMSItem { Slug = "private/report", RenderPath = renderPath, Auth = "fixture-user:fixture-password" });
		await Assert.ThatAsync(async () => await _generator.GenerateUrlMappingsFile(), Throws.TypeOf<InvalidOperationException>());
		Assert.That(await File.ReadAllTextAsync(path), Is.EqualTo("previous protected mappings"));
	}

	[TestCase(null, "cms/report.html")]
	[TestCase("different-user:different-password", "cms/report.html")]
	[TestCase("fixture-user:fixture-password", "cms/other.html")]
	public async Task MappingRejectsPhysicalUrlPolicyCollisionsWithoutReplacingExistingMap(string authentication, string otherRenderPath) {
		await _generator.GenerateNGinxFolder();
		var path = Path.Combine(_generator.CalculateNGinxFolderPath(), "conf", NginxMappingsGenerator.MappingFileName);
		await File.WriteAllTextAsync(path, "previous protected mappings");
		_repository.AddOrUpdateCMSItem(new CMSItem { Slug = "private/report", RenderPath = "cms/report.html", Auth = "fixture-user:fixture-password" });
		_repository.AddOrUpdateCMSItem(new CMSItem { Slug = "cms/report.html", RenderPath = otherRenderPath, Auth = authentication });
		await Assert.ThatAsync(async () => await _generator.GenerateUrlMappingsFile(), Throws.TypeOf<InvalidOperationException>());
		Assert.That(await File.ReadAllTextAsync(path), Is.EqualTo("previous protected mappings"));
	}

	[Test]
	public async Task MappingProtectsRawChildPagePhysicalUrlWithInheritedAuthentication() {
		var root = Path.Combine(_fixtureRoot, "online");
		Directory.CreateDirectory(root);
		using var repository = await LocalNotionRepository.CreateNew(root, pathProfile: new LocalNotionPathProfile { Mode = LocalNotionMode.Online }, logger: new NoOpLogger());
		var parent = new LocalNotionPage { ID = Guid.NewGuid().ToString(), Name = "parent", CMSProperties = new CMSProperties { Authentication = "fixture-user:fixture-password" } };
		var child = new LocalNotionPage { ID = Guid.NewGuid().ToString(), Name = "child", ParentResourceID = parent.ID };
		child.Renders[RenderType.HTML] = new RenderEntry { Slug = "pages/child", LocalPath = "pages/child.html" };
		repository.AddResource(parent);
		repository.AddResource(child);
		var generator = new NginxMappingsGenerator(repository);
		await generator.GenerateNGinxFolder();
		await generator.GenerateUrlMappingsFile();
		var mappings = await File.ReadAllTextAsync(Path.Combine(generator.CalculateNGinxFolderPath(), "conf", NginxMappingsGenerator.MappingFileName));
		Assert.That(Locations(mappings, "/pages/child.html"), Has.Length.EqualTo(1));
		Assert.That(Locations(mappings, "/pages/child.html")[0], Is.EqualTo(Locations(mappings, "/pages/child")[0]));
		Assert.That(Locations(mappings, "/pages/child.html")[0], Does.Contain("$http_authorization"));
	}

	[Test]
	public async Task MappingProtectsRawAttachmentWithInheritedAuthenticationAndMimeType() {
		var root = Path.Combine(_fixtureRoot, "online");
		Directory.CreateDirectory(root);
		using var repository = await LocalNotionRepository.CreateNew(root, pathProfile: new LocalNotionPathProfile { Mode = LocalNotionMode.Online }, logger: new NoOpLogger());
		var parent = new LocalNotionPage { ID = Guid.NewGuid().ToString(), Name = "parent", CMSProperties = new CMSProperties { Authentication = "fixture-user:fixture-password" } };
		var file = new LocalNotionFile { ID = Guid.NewGuid().ToString(), Title = "attachment", ParentResourceID = parent.ID, MimeType = "application/pdf" };
		file.Renders[RenderType.File] = new RenderEntry { Slug = "files/private-document", LocalPath = "files/Private Document.pdf" };
		repository.AddResource(parent);
		repository.AddResource(file);
		var generator = new NginxMappingsGenerator(repository);
		await generator.GenerateNGinxFolder();
		await generator.GenerateUrlMappingsFile();
		var mappings = await File.ReadAllTextAsync(Path.Combine(generator.CalculateNGinxFolderPath(), "conf", NginxMappingsGenerator.MappingFileName));
		Assert.That(Locations(mappings, "/files/Private Document.pdf"), Has.Length.EqualTo(1));
		Assert.That(Locations(mappings, "/files/Private Document.pdf")[0], Is.EqualTo(Locations(mappings, "/files/private-document")[0]));
		Assert.That(Locations(mappings, "/files/Private Document.pdf")[0], Does.Contain("default_type application/pdf;"));
	}

	private async Task<string> GenerateMappings() {
		await _generator.GenerateNGinxFolder();
		await _generator.GenerateUrlMappingsFile();
		return await File.ReadAllTextAsync(Path.Combine(_generator.CalculateNGinxFolderPath(), "conf", NginxMappingsGenerator.MappingFileName));
	}

	private static string[] Locations(string mappings, string uri) => Regex.Matches(mappings, "^location = (?:\"(?<uri>[^\"]+)\"|(?<uri>[^ \\r\\n]+)) \\{(?<body>.*?)(?=^location |\\z)", RegexOptions.Multiline | RegexOptions.Singleline)
		.Where(match => match.Groups["uri"].Value == uri).Select(match => match.Groups["body"].Value.Trim()).ToArray();

	private string CreateRender(string fileName, string content = "render") {
		Directory.CreateDirectory(_cmsPath);
		var filePath = Path.Combine(_cmsPath, fileName);
		File.WriteAllText(filePath, content);
		return filePath;
	}

	private void RegisterRender(string renderPath) => _repository.AddOrUpdateCMSItem(new CMSItem { Slug = "", RenderPath = renderPath });
}
