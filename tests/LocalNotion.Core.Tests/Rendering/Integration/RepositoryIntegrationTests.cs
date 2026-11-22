// Copyright (c) Herman Schoenfeld 2018 - Present. All rights reserved. (https://sphere10.com/products/localnotion)
// Author: Herman Schoenfeld <herman@sphere10.com>
//
// Distributed under the GPLv3 software license, see the accompanying file LICENSE
// or visit https://github.com/HermanSchoenfeld/localnotion/blob/master/LICENSE
//
// This notice must not be removed when duplicating this file or its contents, in whole or in part.

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using AngleSharp.Html.Parser;
using LocalNotion.Core;
using Sphere10.Framework.Web.VisualRenderer;
using Newtonsoft.Json;
using Notion.Client;
using NUnit.Framework;
using Sphere10.Framework;
using CoreRenderMode = LocalNotion.Core.RenderMode;
using R = Sphere10.Framework.Web.VisualRenderer;

namespace LocalNotion.Core.Tests;

[TestFixture]
[NonParallelizable]
public class RepositoryIntegrationTests {
	[TestCase(LocalNotionMode.Online, false)]
	[TestCase(LocalNotionMode.Offline, true)]
	[Category("RepositoryIntegration")]
	[Explicit("Requires an external repository supplied through RepositoryPath or LOCALNOTION_TEST_REPOSITORY.")]
	public async Task StoredRepository_RendersWithoutChangingSource(LocalNotionMode mode, bool embedded) {
		await WithRepositoryCopy(async copy => {
			var label = embedded ? "embedded-offline" : "stored-overrides-online";
			var clone = copy.CreateClone(label, mode, embedded);
			var logger = new IntegrationLogger();
			using var repository = await LocalNotionRepository.Open(clone, logger);
			var objectFiles = Directory.EnumerateFiles(Path.Combine(clone, ".localnotion", "objects"), "*", SearchOption.AllDirectories)
				.Concat(Directory.EnumerateFiles(Path.Combine(clone, ".localnotion", "graphs"), "*", SearchOption.AllDirectories)).ToArray();
			var before = HashFiles(clone, objectFiles);
			var resourceIDs = TestContext.Parameters.Get("ExistingRendersOnly", false)
				? copy.Original.Resources.OfType<LocalNotionEditableResource>().Where(resource => resource.Renders.ContainsKey(RenderType.HTML)).Select(resource => resource.ID).ToArray()
				: null;
			await RenderAndVerify(repository, resourceIDs);
			Assert.That(HashFiles(clone, objectFiles), Is.EquivalentTo(before), "Rendering must preserve the copied source objects and graphs.");
			Assert.That(logger.Errors.Select(copy.Sanitize), Is.Empty, "Rendering must not log hidden failures.");
			if (embedded)
				Assert.That(File.Exists(Path.Combine(clone, ".localnotion", "themes", "default", ".config.json")), Is.True, "LocalNotion deploys themes even when the copy starts without them.");
		});
	}

	[Test]
	[Category("RenderingComparison")]
	[SetCulture("")]
	[Explicit("Compares stored HTML against fresh rendering in retained, credential-free copies of the configured repository.")]
	public async Task StoredRepository_ComparesExistingRendering() {
		await WithRepositoryCopy(async copy => {
			var baseline = copy.CreateClone("before", copy.Original.Paths.Mode, false);
			var clone = copy.CreateClone("after", copy.Original.Paths.Mode, false);
			var logger = new IntegrationLogger();
			using var repository = await LocalNotionRepository.Open(clone, logger);
			var resources = repository.Resources.OfType<LocalNotionEditableResource>().ToArray();
			var cmsItems = repository.CMSItems.ToArray();
			var comparisons = resources.Where(resource => resource.Renders.ContainsKey(RenderType.HTML)).Select(resource => new RepositoryRenderComparison {
				Id = resource.ID,
				Category = "Source",
				BeforePath = resource.Renders[RenderType.HTML].LocalPath
			}).Concat(cmsItems.Select(item => new RepositoryRenderComparison {
				Id = item.Slug,
				Category = "CMS",
				BeforePath = item.RenderPath
			})).ToArray();
			foreach (var comparison in comparisons)
				Assert.That(File.Exists(Path.GetFullPath(comparison.BeforePath, baseline)), Is.True, "Each registered render must have a stored baseline.");

			await RenderAndVerify(repository, comparisons.Where(item => item.Category == "Source").Select(item => item.Id));
			Assert.That(logger.Errors.Select(copy.Sanitize), Is.Empty, "Rendering must not log hidden failures.");
			foreach (var comparison in comparisons) {
				comparison.AfterPath = comparison.Category == "Source"
					? repository.GetResource(comparison.Id).Renders[RenderType.HTML].LocalPath
					: repository.CMSItems.Single(item => item.Slug == comparison.Id).RenderPath;
				comparison.Compare(baseline, clone);
			}
			var reportPath = Path.Combine(copy.DirectoryPath, "comparison.json");
			await File.WriteAllTextAsync(reportPath, JsonConvert.SerializeObject(comparisons, Formatting.Indented));
			TestContext.AddTestAttachment(reportPath, "Existing HTML versus the decoupled renderer");
			TestContext.Progress.WriteLine($"Comparison: {comparisons.Length} documents; {comparisons.Count(item => item.ExactMatch)} byte-identical; " +
				$"{comparisons.Count(item => item.DomMatch)} DOM-identical; {comparisons.Count(item => item.EquivalentMatch)} equivalent after documented normalization. Retained copies and report: {copy.DirectoryPath}");
			if (TestContext.Parameters.Get("RequireEquivalent", true))
				Assert.That(comparisons.Where(item => !item.EquivalentMatch).Select(item => item.BeforePath), Is.Empty,
					"Inspect comparison.json and the before/after copies for rendering differences.");
		}, preserve: true);
	}

	[Test]
	[Category("LiveIntegration")]
	[Explicit("Retrieves Notion data using the configured external repository; requires RepositoryPath or LOCALNOTION_TEST_REPOSITORY.")]
	public async Task LiveRepository_RefreshesAndRendersWithoutChangingSource() {
		await WithRepositoryCopy(async copy => {
			Assert.That(copy.Original.NotionApiKey, Is.Not.Null.And.Not.Empty, "The source registry must contain a Notion credential for this explicit live test.");
			Assert.That(copy.Original.CMSDatabase, Is.Not.Null.And.Not.Empty, "The source registry must designate a CMS database for this explicit live test.");
			var clone = copy.CreateClone("live-sync", LocalNotionMode.Online, true);
			var logger = new IntegrationLogger();
			using var repository = await LocalNotionRepository.Open(clone, logger);
			using var timeout = new CancellationTokenSource(TimeSpan.FromMinutes(15));
			var client = NotionClientFactory.Create(new ClientOptions { AuthToken = copy.Original.NotionApiKey });

			// Sync retrieves objects and files into this isolated copy; the credential remains in memory.
			var orchestrator = new NotionSyncOrchestrator(client, repository);
			var updated = await orchestrator.DownloadDatabaseAsync(
				copy.Original.CMSDatabase,
				options: new DownloadOptions { ForceRefresh = TestContext.Parameters.Get("ForceRefresh", true), FaultTolerant = false, Render = true },
				cancellationToken: timeout.Token
			);
			Assert.That(updated.OfType<LocalNotionPage>(), Is.Not.Empty, "Live synchronization must refresh source pages.");
			Assert.That(
				repository.Resources.OfType<LocalNotionDatabase>().Any(database => !string.IsNullOrWhiteSpace(database.PrimaryDataSourceID)),
				Is.True,
				"The synchronized database must have a resolved data source."
			);
			Assert.That(logger.Errors.Select(copy.Sanitize), Is.Empty, "Live synchronization must not log hidden failures.");
			await RenderAndVerify(repository);
			var renderFiles = repository.Resources.SelectMany(resource => resource.Renders.Values).Select(render => render.LocalPath)
				.Concat(repository.CMSItems.Select(item => item.RenderPath)).Select(path => Path.GetFullPath(path, clone)).ToArray();
			var rendersBeforeHosting = HashFiles(clone, renderFiles);
			await NginxMappingsGenerator.GenerateNGinxFiles(repository);
			Assert.That(HashFiles(clone, renderFiles), Is.EquivalentTo(rendersBeforeHosting), "Hosting generation must preserve all registered renders after a pull.");
			await repository.SaveAsync();
			var saved = JsonConvert.DeserializeObject<LocalNotionRegistry>(await File.ReadAllTextAsync(Path.Combine(clone, ".localnotion", "registry.json")));
			Assert.That(saved.NotionApiKey, Is.Null.Or.Empty, "The test registry must never persist credentials.");
			TestContext.Progress.WriteLine(
				$"Refreshed {updated.OfType<LocalNotionPage>().Count()} pages, {updated.OfType<LocalNotionDatabase>().Count()} databases " +
				$"and {updated.OfType<LocalNotionFile>().Count()} files in the isolated copy."
			);
		});
	}

	private static async Task WithRepositoryCopy(Func<RepositoryCopy, Task> test, bool preserve = false) {
		var sourceDirectory = TestContext.Parameters.Get("RepositoryPath", Environment.GetEnvironmentVariable("LOCALNOTION_TEST_REPOSITORY") ?? "");
		if (string.IsNullOrWhiteSpace(sourceDirectory))
			Assert.Ignore("Set NUnit parameter RepositoryPath or environment variable LOCALNOTION_TEST_REPOSITORY to run this explicit integration test.");
		var registryPath = Path.Combine(Path.GetFullPath(sourceDirectory), ".localnotion", "registry.json");
		Assert.That(File.Exists(registryPath), Is.True, "The selected repository must contain .localnotion/registry.json.");
		using var copy = new RepositoryCopy(sourceDirectory, preserve);
		try {
			await test(copy);
		} catch (Exception error) {
			// Neither assertion failures nor transport failures may expose a registry credential or signed URL.
			throw new AssertionException(copy.Sanitize(error.ToString()));
		} finally {
			copy.AssertSourceUnchanged();
		}
	}

	private static async Task RenderAndVerify(LocalNotionRepository repository, IEnumerable<string> resourceIDs = null) {
		var root = repository.Paths.GetRepositoryPath(FileSystemPathType.Absolute);
		var manager = new RenderingManager(repository, repository.Logger);
		using var renderBatch = manager.BeginBatch();
		var resources = repository.Resources.OfType<LocalNotionEditableResource>()
			.Where(resource => resourceIDs == null || resourceIDs.Contains(resource.ID)).ToArray();
		var cmsItems = repository.CMSItems.ToArray();
		manager.PrepareRenderPaths(resources.Select(resource => resource.ID), RenderType.HTML, cmsItems);
		var outputFiles = new List<string>();
		foreach (var resource in resources)
			outputFiles.Add(manager.RenderLocalResource(resource.ID, RenderType.HTML, CoreRenderMode.ReadOnly));
		foreach (var item in repository.CMSItems.ToArray()) {
			manager.RenderCMSItem(item);
			outputFiles.Add(Path.GetFullPath(item.RenderPath, root));
		}
		foreach (var output in outputFiles) {
			Assert.That(IsWithin(root, output), Is.True, "Rendered output must remain inside the isolated repository.");
			var html = await File.ReadAllTextAsync(output);
			Assert.That(html, Does.Not.Contain("theme://").And.Not.Contain("include://"), "All theme tokens must resolve.");
			var document = new HtmlParser().ParseDocument(html);
			var expectedCdn = TestContext.Parameters.Get("ExpectedThemeCdnBaseUrl", "");
			if (repository.Paths.Mode == LocalNotionMode.Online && !string.IsNullOrEmpty(expectedCdn)) {
				Assert.That(html, Does.Not.Contain(".localnotion/render-assets/"), "Configured online theme URLs must survive existing on-disk theme files.");
				Assert.That(document.QuerySelectorAll("script[src]").Select(element => element.GetAttribute("src")), Has.Some.StartsWith(expectedCdn),
					"The stored online repository must use its configured theme CDN.");
			}
			var unresolvedColors = document.All.SelectMany(element => element.Attributes)
				.Where(attribute => (attribute.Name is "class" or "style") && attribute.Value.Contains("{color}", StringComparison.Ordinal))
				.Select(attribute => attribute.Name + "=" + attribute.Value).ToArray();
			Assert.That(unresolvedColors, Is.Empty, $"Renderer color attributes must resolve in '{Path.GetRelativePath(root, output)}'.");
			foreach (var element in document.QuerySelectorAll("[src],[href]")) {
				var url = element.GetAttribute("src") ?? element.GetAttribute("href") ?? "";
				Assert.That(url.StartsWith("localnotion://", StringComparison.OrdinalIgnoreCase), Is.False, "Source handles must resolve before rendering.");
				var marker = url.IndexOf("render-assets/", StringComparison.Ordinal);
				if (marker < 0)
					continue;
				var relative = Uri.UnescapeDataString(url[marker..]).Split('?', '#')[0];
				Assert.That(File.Exists(Path.GetFullPath(relative, root)), Is.True, "Every generated asset reference must resolve to an exported file.");
			}
		}
		Assert.That(outputFiles.Distinct(StringComparer.OrdinalIgnoreCase).Count(), Is.EqualTo(outputFiles.Count), "Rendered output paths must be unique.");
		Assert.That(resources, Is.Not.Empty, "The repository fixture must contain renderable resources.");
		var first = resources.First();
		var renderPath = Path.GetFullPath(first.Renders[RenderType.HTML].LocalPath, root);
		R.DocumentBlock model = new NotionRenderModelBuilder(repository).Build(first.ID, Path.GetDirectoryName(renderPath));
		Assert.That(new Sphere10.Framework.Web.VisualRenderer.HtmlRenderer().Render(model).Html, Is.Not.Empty, "Projected data must render independently.");
		await repository.SaveAsync();
		TestContext.Progress.WriteLine($"Rendered {resources.Length} source documents and {repository.CMSItems.Count()} CMS documents in the isolated copy.");
	}

	private static bool IsWithin(string root, string candidate) =>
		Path.GetFullPath(candidate).StartsWith(
			Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar,
			OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal
		);

	private static IEnumerable<string> EnumerateSourceFiles(string root) =>
		Directory.EnumerateFiles(root, "*", new EnumerationOptions { RecurseSubdirectories = true, AttributesToSkip = FileAttributes.ReparsePoint })
			.Where(filePath => {
				var relative = Path.GetRelativePath(root, filePath).Replace('\\', '/');
				return !relative.StartsWith(".git/", StringComparison.Ordinal) && !relative.StartsWith(".localnotion/logs/", StringComparison.Ordinal) &&
					!relative.StartsWith(".localnotion/nginx/", StringComparison.Ordinal);
			}).OrderBy(filePath => filePath, StringComparer.Ordinal);

	private static Dictionary<string, string> HashFiles(string root, IEnumerable<string> files) =>
		files.ToDictionary(filePath => Path.GetRelativePath(root, filePath), filePath => SHA256.HashData(File.ReadAllBytes(filePath)).ToHexString(true).ToUpperInvariant());

	private sealed class RepositoryCopy : IDisposable {
		private readonly string _sourceDirectory;
		private readonly string _registryText;
		private readonly string[] _sourceFiles;
		private readonly Dictionary<string, string> _originalHashes;
		private readonly string _fixtureDirectory;
		private readonly bool _preserve;

		public RepositoryCopy(string sourceDirectory, bool preserve) {
			_preserve = preserve;
			_sourceDirectory = Path.GetFullPath(sourceDirectory);
			_registryText = File.ReadAllText(Path.Combine(_sourceDirectory, ".localnotion", "registry.json"));
			Original = JsonConvert.DeserializeObject<LocalNotionRegistry>(_registryText);
			_sourceFiles = EnumerateSourceFiles(_sourceDirectory).ToArray();
			_originalHashes = HashFiles(_sourceDirectory, _sourceFiles);
			_fixtureDirectory = Path.Combine(Path.GetTempPath(), "localnotion-real-integration-" + Guid.NewGuid().ToString("N"));
			Directory.CreateDirectory(_fixtureDirectory);
		}

		public LocalNotionRegistry Original { get; }

		public string DirectoryPath => _fixtureDirectory;

		public string CreateClone(string label, LocalNotionMode mode, bool embedded) {
			var clone = Path.Combine(_fixtureDirectory, label);
			Directory.CreateDirectory(clone);
			var registry = JsonConvert.DeserializeObject<LocalNotionRegistry>(_registryText);
			registry.NotionApiKey = null;
			registry.GitSettings = new GitSettings();
			registry.NGinxSettings = new NGinxSettings();
			registry.ApacheSettings = new ApacheSettings();
			registry.Paths.Mode = mode;
			registry.Paths.RepositoryPathR = "../";
			var storagePaths = new[] {
				registry.Paths.ObjectsPathR, registry.Paths.GraphsPathR, registry.Paths.PropertiesPathR,
				registry.Paths.ThemesPathR, registry.Paths.FilesPathR, registry.Paths.DatabasesPathR, registry.Paths.WorkspacePathR,
				registry.Paths.CMSPathR, registry.Paths.PagesPathR, registry.Paths.LogsPathR
			};
			foreach (var relative in storagePaths.Concat(registry.Resources.SelectMany(resource => resource.Renders.Values.Select(render => render.LocalPath)))
				.Concat(registry.CMSItems.Select(item => item.RenderPath)).Where(pathValue => !string.IsNullOrWhiteSpace(pathValue)))
				Assert.That(IsWithin(clone, Path.GetFullPath(relative.Replace('\\', '/'), clone)), Is.True, "Source storage paths must stay within the isolated copy.");
			foreach (var sourceFile in _sourceFiles) {
				var relative = Path.GetRelativePath(_sourceDirectory, sourceFile);
				var portable = relative.Replace('\\', '/');
				if (portable == ".localnotion/registry.json" || embedded && portable.StartsWith(registry.Paths.ThemesPathR.TrimEnd('/') + "/", StringComparison.Ordinal))
					continue;
				var destination = Path.Combine(clone, relative);
				Directory.CreateDirectory(Path.GetDirectoryName(destination));
				File.Copy(sourceFile, destination);
			}
			var destinationRegistry = Path.Combine(clone, ".localnotion", "registry.json");
			Directory.CreateDirectory(Path.GetDirectoryName(destinationRegistry));
			Tools.Json.WriteToFile(destinationRegistry, registry);
			return clone;
		}

		public void AssertSourceUnchanged() {
			var finalFiles = EnumerateSourceFiles(_sourceDirectory).ToArray();
			Assert.That(finalFiles, Is.EqualTo(_sourceFiles), "The source repository file inventory must remain unchanged.");
			Assert.That(HashFiles(_sourceDirectory, finalFiles), Is.EquivalentTo(_originalHashes), "The source repository bytes must remain unchanged.");
		}

		public string Sanitize(string value) {
			if (!string.IsNullOrEmpty(Original.NotionApiKey))
				value = value.Replace(Original.NotionApiKey, "[redacted]", StringComparison.Ordinal);
			return Regex.Replace(value, @"https?://[^\s<>]+", "[url]");
		}

		public void Dispose() {
			if (_preserve)
				return;
			var expectedParent = Path.GetFullPath(Path.GetTempPath()).TrimEnd(Path.DirectorySeparatorChar);
			Assert.That(Path.GetDirectoryName(Path.GetFullPath(_fixtureDirectory)), Is.EqualTo(expectedParent), "Only the isolated fixture directory may be removed.");
			Assert.That(Path.GetFileName(_fixtureDirectory), Does.StartWith("localnotion-real-integration-"));
			if (Directory.Exists(_fixtureDirectory))
				Directory.Delete(_fixtureDirectory, recursive: true);
		}
	}

	private sealed class IntegrationLogger : LoggerBase {
		public ConcurrentQueue<string> Errors { get; } = new();

		protected override void Log(LogLevel level, string message) {
			if (level == LogLevel.Error)
				Errors.Enqueue(message);
		}
	}
}
