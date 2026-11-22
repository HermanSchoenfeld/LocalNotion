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
public class RedirectMappingsTests {
	private string _fixtureRoot;
	private CMSLocalNotionRepository _repository;
	private NginxMappingsGenerator _generator;

	[SetUp]
	public async Task SetUp() {
		_fixtureRoot = Path.Combine(Path.GetTempPath(), "localnotion-redirect-map-" + Guid.NewGuid().ToString("N"));
		Directory.CreateDirectory(_fixtureRoot);
		var databaseId = Guid.NewGuid().ToString();
		_repository = (CMSLocalNotionRepository)await LocalNotionRepository.CreateNew(_fixtureRoot, cmsDatabaseID: databaseId, logger: new NoOpLogger());
		_repository.AddResource(new LocalNotionDatabase { ID = databaseId, Name = "example.invalid", Title = "CMS", PrimaryDataSourceID = Guid.NewGuid().ToString() });
		_generator = new NginxMappingsGenerator(_repository);
		await _generator.GenerateNGinxFolder();
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

	[Test]
	public async Task HardAndSoftRedirectsLeadToLiveRoutesWithTheirQuery() {
		Route("articles/new");
		_repository.AddRedirect("articles/old", "articles/new");
		_repository.AddRedirect("articles/older", "articles/new", RedirectType.Soft);

		var redirects = await Generate();

		Assert.That(Lines(redirects), Is.EqualTo(new[] {
			"""location = "/articles/old" { absolute_redirect off; return 301 "/articles/new$is_args$args"; }""",
			"""location = "/articles/older" { absolute_redirect off; return 302 "/articles/new$is_args$args"; }"""
		}));
	}

	[Test]
	public async Task MappingsIncludeTheRedirectsFileEvenWithoutRedirects() {
		var redirects = await Generate();

		Assert.That(Lines(redirects), Is.Empty);
		var mappings = await File.ReadAllTextAsync(Path.Combine(_generator.CalculateNGinxFolderPath(), "conf", NginxMappingsGenerator.MappingFileName));
		Assert.That(Regex.Matches(mappings, @"^include ln-redirects\*\.conf;\s*$", RegexOptions.Multiline).Count, Is.EqualTo(1),
			"A mask include keeps Nginx starting where the redirects file is not mounted yet.");
	}

	[Test]
	public async Task RedirectsToAbsentRoutesStayInTheRegistryButAreNotServed() {
		_repository.AddRedirect("articles/old", "articles/deleted");

		var redirects = await Generate();

		Assert.That(Lines(redirects), Is.Empty);
		Assert.That(_repository.Redirects.Single().To, Is.EqualTo("articles/deleted"), "The registry keeps it for culling.");
	}

	[Test]
	public async Task RedirectsNeverReplaceAGeneratedLocation() {
		Route("articles/new");
		_repository.AddRedirect("sitemap.xml", "articles/new");

		var redirects = await Generate();

		Assert.That(Lines(redirects), Is.Empty);
	}

	[Test]
	public async Task UnicodeAddressesMatchDecodedAndRedirectEncoded() {
		Route("مقالات/جديد");
		_repository.AddRedirect("مقالات/قديم", "مقالات/جديد");

		var redirects = await Generate();

		Assert.That(Lines(redirects), Is.EqualTo(new[] {
			"location = \"/مقالات/قديم\" { absolute_redirect off; return 301 \"/" + Uri.EscapeDataString("مقالات") + "/" + Uri.EscapeDataString("جديد") + "$is_args$args\"; }"
		}));
	}

	private void Route(string slug) {
		var renderPath = "cms/" + Guid.NewGuid().ToString("N") + ".html";
		var file = Path.Combine(_fixtureRoot, renderPath);
		Directory.CreateDirectory(Path.GetDirectoryName(file));
		File.WriteAllText(file, "render");
		_repository.AddOrUpdateCMSItem(new CMSItem { Slug = slug, RenderPath = renderPath, Parts = [] });
	}

	private async Task<string> Generate() {
		await _generator.GenerateUrlMappingsFile();
		return await File.ReadAllTextAsync(Path.Combine(_generator.CalculateNGinxFolderPath(), "conf", NginxMappingsGenerator.RedirectsFileName));
	}

	private static string[] Lines(string redirects) => redirects.Split('\n').Select(line => line.TrimEnd('\r')).Where(line => line.StartsWith("location ", StringComparison.Ordinal)).ToArray();
}
