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

	[Test]
	public async Task ListingMappingSelectsOnlyFixedManifestTargetsAtExistingRoute() {
		RegisterListing("articles", ["cms/articles.html", "cms/articles-2.html", "cms/articles-3.html"]);
		_repository.AddOrUpdateCMSItem(new CMSItem { Slug = "articles/cryptography/signatures", RenderPath = "cms/signatures.html" });
		var mappings = await GenerateMappings();
		var listing = Locations(mappings, "/articles").Single();
		Assert.That(listing, Does.Contain("if ($arg_page = \"\") { set $ln_listing_path \"/cms/articles.html\"; }"));
		Assert.That(listing, Does.Contain("if ($arg_page = \"1\") { set $ln_listing_path \"/cms/articles.html\"; }"));
		Assert.That(listing, Does.Contain("if ($arg_page = \"2\") { set $ln_listing_path \"/cms/articles-2.html\"; }"));
		Assert.That(listing, Does.Contain("if ($arg_page = \"3\") { set $ln_listing_path \"/cms/articles-3.html\"; }"));
		Assert.That(listing, Does.Contain("if ($ln_listing_path = \"\") { return 404; }"));
		Assert.That(listing, Does.Contain("rewrite ^ $ln_listing_path last;"));
		Assert.That(listing, Does.Not.Contain("try_files $ln_listing_path"));
		Assert.That(listing, Does.Not.Contain("rewrite ^ $arg_page"));
		Assert.That(listing, Does.Not.Contain("$args ="), "Unrelated tracking arguments must remain intact.");
		Assert.That(Locations(mappings, "/articles/page/2"), Is.Empty);
		Assert.That(Locations(mappings, "/articles/2"), Is.Empty);
		Assert.That(Locations(mappings, "/cms/articles-2.html"), Is.Empty, "Public backing files retain the existing static-root policy.");
		var article = Locations(mappings, "/articles/cryptography/signatures").Single();
		Assert.That(article, Does.Contain("try_files \"/cms/signatures.html\" =404;"));
		Assert.That(article, Does.Not.Contain("$arg_page"), "Article routes must remain standalone documents.");
	}

	[Test]
	public async Task RenamedCollectionsAtDifferentDepthsSelectTheirOwnStaticPages() {
		RegisterListing("updates", ["cms/updates.html", "cms/updates-2.html"]);
		RegisterListing("fixed/fixed2/field-notes/device-setup", ["cms/device-setup.html", "cms/device-setup-2.html"]);
		_repository.AddOrUpdateCMSItem(new CMSItem { Slug = "fixed/fixed2/field-notes/device-setup/installing-sensors", RenderPath = "cms/sensors.html" });
		var mappings = await GenerateMappings();
		var updates = Locations(mappings, "/updates").Single();
		var devices = Locations(mappings, "/fixed/fixed2/field-notes/device-setup").Single();
		Assert.That(updates, Does.Contain("if ($arg_page = \"2\") { set $ln_listing_path \"/cms/updates-2.html\"; }"));
		Assert.That(updates, Does.Not.Contain("device-setup-2.html"));
		Assert.That(devices, Does.Contain("if ($arg_page = \"2\") { set $ln_listing_path \"/cms/device-setup-2.html\"; }"));
		Assert.That(devices, Does.Not.Contain("updates-2.html"));
		Assert.That(Locations(mappings, "/fixed/fixed2/field-notes/device-setup/").Single(),
			Does.Contain("return 301 \"/fixed/fixed2/field-notes/device-setup$is_args$args\";"));
		Assert.That(Locations(mappings, "/articles"), Is.Empty);
		var article = Locations(mappings, "/fixed/fixed2/field-notes/device-setup/installing-sensors").Single();
		Assert.That(article, Does.Contain("try_files \"/cms/sensors.html\" =404;"));
		Assert.That(article, Does.Not.Contain("$arg_page"));
		Assert.That(Locations(mappings, "/fixed/fixed2/field-notes/device-setup/2/installing-sensors"), Is.Empty);
	}

	[Test]
	public async Task ListingWithOnePageWhitelistsOnlyDefaultAndPageOne() {
		RegisterListing("articles", ["cms/articles.html"]);
		var listing = Locations(await GenerateMappings(), "/articles").Single();
		var allowedPages = Regex.Matches(listing, """if \(\$arg_page = "(?<page>[^"]*)"\)""")
			.Select(match => match.Groups["page"].Value).ToArray();
		Assert.That(allowedPages, Is.EqualTo(new[] { "", "1" }));
		Assert.That(listing, Does.Contain("set $ln_listing_path \"\";"));
		Assert.That(listing, Does.Contain("if ($ln_listing_path = \"\") { return 404; }"));
	}

	[Test]
	public async Task ListingTrailingSlashAliasPreservesAllQueryArgumentsWithoutChangingOtherRedirects() {
		RegisterListing("articles/cryptography", ["cms/articles.html", "cms/articles-2.html"]);
		var mappings = await GenerateMappings();
		var alias = Locations(mappings, "/articles/cryptography/").Single();
		Assert.That(alias, Does.Contain("return 301 \"/articles/cryptography$is_args$args\";"));
		Assert.That(mappings, Does.Contain("return 301 $1;"), "The existing global redirect policy remains unchanged.");
		Assert.That(Locations(mappings, "/unrelated/"), Is.Empty);
	}

	[TestCase("page=1&page=2", true)]
	[TestCase("page=&source=mail&PAGE=2", true)]
	[TestCase("page&page=2", true)]
	[TestCase("source=mail&page=2&other=3&page=1&end=4", true)]
	[TestCase("page=2", false)]
	[TestCase("page=", false)]
	[TestCase("source=mail&page=2&campaign=release", false)]
	[TestCase("webpage=1&page=2", false)]
	[TestCase("page_size=1&page=2", false)]
	[TestCase("source=page=1&page=2", false)]
	[TestCase("page=2&next=%26page%3D1", false)]
	public async Task ListingMappingRejectsOnlyDuplicatePageArguments(string arguments, bool rejected) {
		RegisterListing("articles", ["cms/articles.html", "cms/articles-2.html"]);
		var listing = Locations(await GenerateMappings(), "/articles").Single();
		var duplicateCheck = Regex.Match(listing, """if \(\$args ~\* "(?<pattern>[^"]+)"\) \{ return 400; \}""");
		Assert.That(duplicateCheck.Success, Is.True);
		Assert.That(Regex.IsMatch(arguments, duplicateCheck.Groups["pattern"].Value, RegexOptions.IgnoreCase), Is.EqualTo(rejected));
	}

	[Test]
	public async Task ProtectedListingAppliesAuthenticationToQueryRouteAndEveryPhysicalPage() {
		RegisterListing("private/articles", ["cms/articles.html", "cms/articles-2.html", "cms/articles-3.html"], "fixture-user:fixture-password");
		var mappings = await GenerateMappings();
		var listing = Locations(mappings, "/private/articles").Single();
		var authentication = "if ($http_authorization != \"Basic " + "fixture-user:fixture-password".ToBase64() + "\")";
		Assert.That(listing, Does.Contain(authentication));
		Assert.That(listing.IndexOf(authentication, StringComparison.Ordinal), Is.LessThan(listing.IndexOf("$arg_page", StringComparison.Ordinal)));
		foreach (var file in new[] { "articles.html", "articles-2.html", "articles-3.html" }) {
			var physical = Locations(mappings, "/cms/" + file).Single();
			Assert.That(physical, Does.Contain(authentication));
			Assert.That(physical, Does.Contain("try_files \"/cms/" + file + "\" =404;"));
			Assert.That(physical, Does.Not.Contain("$arg_page"), "Physical URLs serve their fixed file with the same credentials.");
		}
	}

	[TestCase("cms/../cms/articles.html")]
	[TestCase("./cms/articles.html")]
	public async Task ListingMappingAcceptsEquivalentPrimaryRenderPath(string firstPath) {
		_repository.AddOrUpdateCMSItem(new CMSItem {
			Slug = "articles", RenderPath = "cms/articles.html", ListingRenderPaths = [firstPath, "cms/articles-2.html"]
		});
		var mappings = await GenerateMappings();
		Assert.That(Locations(mappings, "/articles").Single(), Does.Contain("set $ln_listing_path \"/cms/articles.html\";"));
	}

	[TestCase(null)]
	[TestCase("")]
	[TestCase(" ")]
	[TestCase("../outside.html")]
	[TestCase("cms/$variable.html")]
	[TestCase("cms/\"quote.html")]
	[TestCase("cms/line\nbreak.html")]
	[TestCase("cms/articles.html")]
	[TestCase("cms/sitemap.xml")]
	[TestCase("pages/raw-page.html")]
	[TestCase(".localnotion/repository.json")]
	[TestCase("cms/source.json")]
	[TestCase("cms/query?.html")]
	[TestCase("cms/fragment#.html")]
	public async Task ListingMappingRejectsInvalidExtraTargetsWithoutReplacingPreviousMap(string extraPath) {
		await _generator.GenerateNGinxFolder();
		var mappingsPath = Path.Combine(_generator.CalculateNGinxFolderPath(), "conf", NginxMappingsGenerator.MappingFileName);
		await File.WriteAllTextAsync(mappingsPath, "previous mappings");
		RegisterListing("articles", ["cms/articles.html", extraPath]);
		await Assert.ThatAsync(async () => await _generator.GenerateUrlMappingsFile(), Throws.TypeOf<InvalidOperationException>());
		Assert.That(await File.ReadAllTextAsync(mappingsPath), Is.EqualTo("previous mappings"));
	}

	[Test]
	public async Task ListingMappingRejectsManifestWhoseFirstPageIsNotPrimaryRender() {
		_repository.AddOrUpdateCMSItem(new CMSItem { Slug = "articles", RenderPath = "cms/articles.html", ListingRenderPaths = ["cms/wrong.html"] });
		await Assert.ThatAsync(async () => await GenerateMappings(), Throws.TypeOf<InvalidOperationException>());
	}

	[TestCase("another", "cms/articles.html")]
	[TestCase("another", "cms/articles-2.html")]
	[TestCase("cms/articles-2.html", "cms/another.html")]
	public async Task ListingMappingRejectsOtherRouteAndBackingFileCollisions(string otherSlug, string otherPath) {
		RegisterListing("articles", ["cms/articles.html", "cms/articles-2.html"]);
		_repository.AddOrUpdateCMSItem(new CMSItem { Slug = otherSlug, RenderPath = otherPath });
		await Assert.ThatAsync(async () => await GenerateMappings(), Throws.TypeOf<InvalidOperationException>());
	}

	[Test]
	public async Task ListingMappingRejectsDuplicateFriendlyRouteFromRawPage() {
		var root = Path.Combine(_fixtureRoot, "online-collision");
		Directory.CreateDirectory(root);
		using var repository = (CMSLocalNotionRepository)await LocalNotionRepository.CreateNew(root, cmsDatabaseID: Guid.NewGuid().ToString(),
			pathProfile: new LocalNotionPathProfile { Mode = LocalNotionMode.Online }, logger: new NoOpLogger());
		repository.AddOrUpdateCMSItem(new CMSItem { Slug = "articles", RenderPath = "cms/articles.html", ListingRenderPaths = ["cms/articles.html"] });
		var page = new LocalNotionPage { ID = Guid.NewGuid().ToString(), Name = "other" };
		page.Renders[RenderType.HTML] = new RenderEntry { Slug = "articles", LocalPath = "pages/other.html" };
		repository.AddResource(page);
		var generator = new NginxMappingsGenerator(repository);
		await generator.GenerateNGinxFolder();
		await Assert.ThatAsync(async () => await generator.GenerateUrlMappingsFile(), Throws.TypeOf<InvalidOperationException>());
	}

	[Test]
	public async Task ListingMappingRejectsSharedPagesBetweenManifests() {
		RegisterListing("articles", ["cms/articles.html", "cms/shared.html"]);
		RegisterListing("articles/cryptography", ["cms/cryptography.html", "cms/shared.html"]);
		await Assert.ThatAsync(async () => await GenerateMappings(), Throws.TypeOf<InvalidOperationException>());
	}

	[TestCase("cms/articles.html")]
	[TestCase("cms/articles-2.html")]
	public async Task ListingMappingRejectsFriendlyRouteToItsOwnPhysicalPage(string slug) {
		RegisterListing(slug, ["cms/articles.html", "cms/articles-2.html"]);
		await Assert.ThatAsync(async () => await GenerateMappings(), Throws.TypeOf<InvalidOperationException>());
	}

	[Test]
	public async Task ListingMappingRejectsTrailingSlashAliasCollision() {
		RegisterListing("articles", ["cms/articles.html", "cms/articles-2.html"]);
		_repository.AddOrUpdateCMSItem(new CMSItem { Slug = "articles/", RenderPath = "cms/another.html" });
		await Assert.ThatAsync(async () => await GenerateMappings(), Throws.TypeOf<InvalidOperationException>());
	}

	[Test]
	public async Task ProtectedListingRejectsPublicRouteToExtraPage() {
		RegisterListing("private/articles", ["cms/articles.html", "cms/articles-2.html"], "fixture-user:fixture-password");
		_repository.AddOrUpdateCMSItem(new CMSItem { Slug = "public-copy", RenderPath = "cms/articles-2.html" });
		await Assert.ThatAsync(async () => await GenerateMappings(), Throws.TypeOf<InvalidOperationException>());
	}

	[Test]
	[Platform("Win")]
	public async Task ListingMappingRejectsExtraRenderWithMismatchedFileNameCase() {
		CreateRender("articles.html");
		CreateRender("articles-2.html");
		RegisterListing("articles", ["cms/articles.html", "cms/ARTICLES-2.html"]);
		await Assert.ThatAsync(async () => await GenerateMappings(), Throws.TypeOf<InvalidOperationException>());
	}

	[Test]
	public async Task InvalidListingPreservesPreviousMappingsAndSkipsRenderCleanup() {
		var first = CreateRender("articles.html");
		var previousPage = CreateRender("previous-page.html");
		await _generator.GenerateNGinxFolder();
		var mappingsPath = Path.Combine(_generator.CalculateNGinxFolderPath(), "conf", NginxMappingsGenerator.MappingFileName);
		await File.WriteAllTextAsync(mappingsPath, "previous mappings");
		RegisterListing("articles", ["cms/articles.html", "../outside.html"]);
		await Assert.ThatAsync(async () => await NginxMappingsGenerator.GenerateNGinxFiles(_repository), Throws.TypeOf<InvalidOperationException>());
		Assert.That(await File.ReadAllTextAsync(mappingsPath), Is.EqualTo("previous mappings"));
		Assert.That(File.Exists(first), Is.True);
		Assert.That(File.Exists(previousPage), Is.True, "Failed routing validation must not delete the prior deployment's files.");
	}

	[Test]
	public async Task ListingCleanupKeepsEveryManifestPageAndRemovesPagesAfterManifestShrinks() {
		var first = CreateRender("articles.html");
		var second = CreateRender("articles-2.html");
		var third = CreateRender("articles-3.html");
		var obsolete = CreateRender("obsolete.html");
		RegisterListing("articles", ["cms/articles.html", "cms/articles-2.html", "cms/articles-3.html"]);
		await NginxMappingsGenerator.GenerateNGinxFiles(_repository);
		Assert.That(new[] { first, second, third }.All(File.Exists), Is.True);
		Assert.That(File.Exists(obsolete), Is.False);
		RegisterListing("articles", ["cms/articles.html", "cms/articles-2.html"]);
		await NginxMappingsGenerator.GenerateNGinxFiles(_repository);
		Assert.That(File.Exists(first), Is.True);
		Assert.That(File.Exists(second), Is.True);
		Assert.That(File.Exists(third), Is.False, "A removed manifest entry becomes an obsolete render.");
		var mappings = await GenerateMappings();
		Assert.That(Locations(mappings, "/articles").Single(), Does.Not.Contain("articles-3.html"));
	}

	[Test]
	[Category("NginxIntegration")]
	[Explicit("Exports dummy routing files for an isolated Nginx container; set NginxPaginationFixturePath to a new directory.")]
	public async Task ExportPaginationRoutingFixture() {
		var exportPath = TestContext.Parameters.Get("NginxPaginationFixturePath", "");
		Assert.That(Path.IsPathFullyQualified(exportPath), Is.True, "Supply an absolute fixture export directory.");
		Assert.That(Directory.Exists(exportPath), Is.False, "The fixture exporter must not overwrite an existing directory.");
		RegisterListing("articles", ["cms/public-1.html", "cms/public-2.html", "cms/public-3.html"]);
		RegisterListing("private/articles", ["cms/private-1.html", "cms/private-2.html"], "pagination-fixture:pagination-fixture");
		RegisterListing("articles/filenames", ["cms/100% ready 1.html", "cms/100% ready 2.html"]);
		RegisterListing("private/filenames", ["cms/private 100% ready 1.html", "cms/private 100% ready 2.html"], "pagination-fixture:pagination-fixture");
		_repository.AddOrUpdateCMSItem(new CMSItem { Slug = "articles/unchanged-article", RenderPath = "cms/article.html" });
		CreateRender("public-1.html", "<html><body>PUBLIC_PAGE_1</body></html>");
		CreateRender("public-2.html", "<html><body>PUBLIC_PAGE_2</body></html>");
		CreateRender("public-3.html", "<html><body>PUBLIC_PAGE_3</body></html>");
		CreateRender("private-1.html", "<html><body>Dummy authenticated fixture page</body></html>");
		CreateRender("private-2.html", "<html><body>Dummy authenticated fixture page</body></html>");
		CreateRender("article.html", "<html><body>PUBLIC_ARTICLE</body></html>");
		CreateRender("100% ready 1.html", "<html><body>PUBLIC_FILENAME_1</body></html>");
		CreateRender("100% ready 2.html", "<html><body>PUBLIC_FILENAME_2</body></html>");
		CreateRender("private 100% ready 1.html", "<html><body>Dummy authenticated fixture page</body></html>");
		CreateRender("private 100% ready 2.html", "<html><body>Dummy authenticated fixture page</body></html>");
		await GenerateMappings();

		// Export generated hosting files and dummy HTML only, never the repository registry or source data.
		var exportCmsPath = Path.Combine(exportPath, "cms");
		var exportConfPath = Path.Combine(exportPath, "conf");
		Directory.CreateDirectory(exportCmsPath);
		Directory.CreateDirectory(exportConfPath);
		foreach (var file in Directory.EnumerateFiles(_cmsPath, "*.html"))
			File.Copy(file, Path.Combine(exportCmsPath, Path.GetFileName(file)));
		foreach (var file in Directory.EnumerateFiles(Path.Combine(_generator.CalculateNGinxFolderPath(), "conf")))
			File.Copy(file, Path.Combine(exportConfPath, Path.GetFileName(file)));
		TestContext.AddTestAttachment(Path.Combine(exportConfPath, NginxMappingsGenerator.MappingFileName), "Generated dummy pagination mappings");
	}

	private void RegisterListing(string slug, string[] paths, string authentication = null)
		=> _repository.AddOrUpdateCMSItem(new CMSItem { Slug = slug, RenderPath = paths[0], ListingRenderPaths = paths, Auth = authentication });

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
