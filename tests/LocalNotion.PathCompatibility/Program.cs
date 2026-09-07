using LocalNotion.Core;
using Newtonsoft.Json;
using Sphere10.Framework;

// Dependency-free integration regression: dotnet run --project tests/LocalNotion.PathCompatibility
var fixtureRoot = Path.Combine(Path.GetTempPath(), "localnotion-path-compatibility-" + Guid.NewGuid().ToString("N"));
Directory.CreateDirectory(fixtureRoot);
try {
	await VerifyWindowsRepositoryRoundTrip();
	await VerifyWindowsProfileCreation();
	Console.WriteLine("PASS: Windows repository load, existing render replacement, save/reload, metadata preservation, and Windows profile creation.");
} finally {
	// The only deleted tree is the unique fixture directory created above.
	Directory.Delete(fixtureRoot, recursive: true);
}

async Task VerifyWindowsRepositoryRoundTrip() {
	var repositoryRoot = Path.Combine(fixtureRoot, "existing repository");
	Directory.CreateDirectory(repositoryRoot);
	var id = Guid.NewGuid().ToString("N");
	var paths = WindowsProfile();
	var registryFile = Path.Combine(repositoryRoot, ".localnotion", "registry.json");
	var resourceRender = Path.Combine(repositoryRoot, "files", id, "existing.txt");
	var cmsRender = Path.Combine(repositoryRoot, "cms", "existing.html");
	Directory.CreateDirectory(Path.GetDirectoryName(registryFile)!);
	Directory.CreateDirectory(Path.GetDirectoryName(resourceRender)!);
	Directory.CreateDirectory(Path.GetDirectoryName(cmsRender)!);
	await File.WriteAllTextAsync(resourceRender, "original render");
	await File.WriteAllTextAsync(cmsRender, "original CMS render");

	var unchangedPaths = new[] { @"C:\external\page.html", @"\\server\share\page.html", @"\rooted\page.html", "/external/page.html", @"https://example.invalid/file\name" };
	var registry = new LocalNotionRegistry {
		Paths = paths,
		NotionApiKey = @"test-only\key",
		GitSettings = new GitSettings { Enabled = true, Push = true },
		NGinxSettings = new NGinxSettings { Enabled = true, ReloadCommand = @"C:\nginx\nginx.exe -s reload" },
		ApacheSettings = new ApacheSettings { Enabled = true },
		Resources = new[] {
			new LocalNotionFile {
				ID = id, Title = @"unchanged\title",
				Renders = new Dictionary<RenderType, RenderEntry> {
					[RenderType.File] = new() { LocalPath = $@"files\{id}\existing.txt", Slug = @"unchanged\slug" }
				}
			}
		}.Concat(unchangedPaths.Select((path, index) => new LocalNotionFile {
			ID = Guid.NewGuid().ToString("N"), Title = "External " + index,
			Renders = new Dictionary<RenderType, RenderEntry> {
				[RenderType.File] = new() { LocalPath = path, Slug = "external-" + index }
			}
		})).ToArray(),
		CMSItems = [
			new CMSItem { Slug = @"cms\unchanged", RenderPath = @"cms\existing.html", Image = @"https://example.invalid/image\name" },
			new CMSItem { Slug = "empty", RenderPath = "" },
			new CMSItem { Slug = "missing", RenderPath = null }
		]
	};
	Tools.Json.WriteToFile(registryFile, registry);
	var beforeLoad = await File.ReadAllBytesAsync(registryFile);
	using (var repository = await LocalNotionRepository.OpenRegistry(registryFile)) {
		Equal(repositoryRoot, Path.TrimEndingDirectorySeparator(repository.Paths.GetRepositoryPath(FileSystemPathType.Absolute)), "repository root");
		Equal(Path.Combine(repositoryRoot, ".localnotion", "objects"), repository.Paths.GetInternalResourceFolderPath(InternalResourceType.Objects, FileSystemPathType.Absolute), "objects folder");
		var file = repository.Resources.Single(resource => resource.ID == id);
		Equal($"files/{id}/existing.txt", file.Renders[RenderType.File].LocalPath, "portable resource render");
		Equal(@"unchanged\slug", file.Renders[RenderType.File].Slug, "resource slug");
		Equal(@"unchanged\title", file.Title, "resource title");
		Equal("cms/existing.html", repository.CMSItems.Single(item => item.Slug == @"cms\unchanged").RenderPath, "portable CMS render");
		Equal("", repository.CMSItems.Single(item => item.Slug == "empty").RenderPath, "empty render");
		Equal(null, repository.CMSItems.Single(item => item.Slug == "missing").RenderPath, "missing render");
		Check(File.Exists(Path.Join(repositoryRoot, file.Renders[RenderType.File].LocalPath)), "existing resource resolves");
		Check(File.Exists(Path.Join(repositoryRoot, repository.CMSItems.First().RenderPath)), "existing CMS render resolves");
		Check(!repository.RequiresSave, "loading must not request a registry write");
		await repository.SaveAsync();
		Check(beforeLoad.SequenceEqual(await File.ReadAllBytesAsync(registryFile)), "load and no-op save preserve registry bytes");
		for (var index = 0; index < unchangedPaths.Length; index++)
			Equal(unchangedPaths[index], repository.Resources.Single(resource => resource.Title == "External " + index).Renders[RenderType.File].LocalPath, "absolute/URI value preserved");

		var replacement = Path.Combine(fixtureRoot, "replacement.txt");
		await File.WriteAllTextAsync(replacement, "replacement render");
		var actualRender = repository.ImportResourceRender(id, RenderType.File, replacement);
		Equal(Path.GetFullPath(resourceRender), Path.GetFullPath(actualRender), "replace the existing render path");
		Equal("replacement render", await File.ReadAllTextAsync(resourceRender), "existing file replaced");
		await repository.SaveAsync();
	}

	var saved = JsonConvert.DeserializeObject<LocalNotionRegistry>(await File.ReadAllTextAsync(registryFile))!;
	Equal("../", saved.Paths.RepositoryPathR, "saved repository path");
	Equal(".localnotion/objects", saved.Paths.ObjectsPathR, "saved objects path");
	Equal(".localnotion/graphs", saved.Paths.GraphsPathR, "saved graphs path");
	Equal(".localnotion/properties", saved.Paths.PropertiesPathR, "saved properties path");
	Equal(".localnotion/themes", saved.Paths.ThemesPathR, "saved themes path");
	Equal(".localnotion/logs", saved.Paths.LogsPathR, "saved logs path");
	Equal("files/", saved.Paths.FilesPathR, "saved files path");
	Equal("databases/", saved.Paths.DatabasesPathR, "saved databases path");
	Equal("pages/", saved.Paths.PagesPathR, "saved pages path");
	Equal("workspaces/", saved.Paths.WorkspacePathR, "saved workspace path");
	Equal("cms/", saved.Paths.CMSPathR, "saved CMS path");
	Equal(paths.BaseUrl, saved.Paths.BaseUrl, "base URL preserved");
	Equal(registry.NotionApiKey, saved.NotionApiKey, "key preserved");
	Check(saved.GitSettings.Enabled && saved.GitSettings.Push, "Git settings preserved");
	Check(saved.NGinxSettings.Enabled && saved.NGinxSettings.ReloadCommand == registry.NGinxSettings.ReloadCommand, "nginx settings preserved");
	Check(saved.ApacheSettings.Enabled, "Apache settings preserved");
	Equal($"files/{id}/existing.txt", saved.Resources.Single(resource => resource.ID == id).Renders[RenderType.File].LocalPath, "newly saved render is portable");
	using (var reopened = await LocalNotionRepository.OpenRegistry(registryFile))
		Check(File.Exists(Path.Join(repositoryRoot, reopened.Resources.Single(resource => resource.ID == id).Renders[RenderType.File].LocalPath)), "saved repository reopens");
}

async Task VerifyWindowsProfileCreation() {
	var root = Path.Combine(fixtureRoot, "new repository");
	Directory.CreateDirectory(root);
	using var repository = await LocalNotionRepository.CreateNew(root, pathProfile: WindowsProfile());
	Check(File.Exists(repository.Paths.GetRegistryFilePath(FileSystemPathType.Absolute)), "Windows profile creates actual nested registry");
	Check(Directory.Exists(Path.Combine(root, ".localnotion", "objects")), "Windows profile creates actual nested objects");
}

LocalNotionPathProfile WindowsProfile() => new() {
	RepositoryPathR = @"..\",
	RegistryPathR = @".localnotion\registry.json",
	ObjectsPathR = @".localnotion\objects",
	GraphsPathR = @".localnotion\graphs",
	PropertiesPathR = @".localnotion\properties",
	ThemesPathR = @".localnotion\themes",
	LogsPathR = @".localnotion\logs",
	FilesPathR = @"files\",
	DatabasesPathR = @"databases\",
	PagesPathR = @"pages\",
	WorkspacePathR = @"workspaces\",
	CMSPathR = @"cms\",
	BaseUrl = @"https://example.invalid/base\literal"
};

void Equal(string expected, string actual, string description) {
	Check(expected == actual, $"{description}: expected '{expected}', got '{actual}'");
}

void Check(bool condition, string description) {
	if (!condition)
		throw new InvalidOperationException(description);
}
