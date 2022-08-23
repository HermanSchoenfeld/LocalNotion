using Hydrogen;
using Hydrogen.Data;
using Notion.Client;

namespace LocalNotion;


// Make ThreadSafe
public class LocalNotionRepository : ILocalNotionRepository, IAsyncLoadable, IAsyncSaveable {


	public event EventHandlerEx<object> Loading;
	public event EventHandlerEx<object> Loaded;
	public event EventHandlerEx<object> Saving;
	public event EventHandlerEx<object> Saved;

	private LocalNotionRegistry _registry;
	private GuidFileStore _objectStore;
	private IDictionary<string, LocalNotionResource> _resourcesByNID;
	private readonly ICache<string, string> _resourceBySlug;
	private readonly ICache<string, string[]> _articlesByCategorySlug;
	private readonly ICache<string, string[]> _categoriesByCategorySlug;
	private readonly MulticastLogger _logger;

	public LocalNotionRepository(string repoFile, ILogger logger = null) {
		Guard.ArgumentNotNull(repoFile, nameof(repoFile));
		Guard.FileExists(repoFile);
		_logger = new MulticastLogger();
		if (logger != null)
			_logger.Add(logger);
		
		_registry = null;
		_objectStore = null;
		_resourcesByNID = null;

		_resourceBySlug = new BulkFetchActionCache<string, string>(
			() => {
				var result = new Dictionary<string, string>(StringComparer.InvariantCultureIgnoreCase);
				foreach (var resource in _registry.Resources) {
					result[resource.DefaultSlug] = resource.ID;
					if (resource is LocalNotionPage {CMSProperties: not null } lnp)
						result[lnp.CMSProperties.Slug] = resource.ID;
				}
				return result;
			},
			keyComparer: StringComparer.InvariantCultureIgnoreCase
		);

		_articlesByCategorySlug = new BulkFetchActionCache<string, string[]>(
			() => {
				var result = new LookupEx<string, string>(StringComparer.InvariantCultureIgnoreCase);
				foreach (var article in _registry.Articles) {
					var categoryKey = LocalNotionHelper.CreateCategorySlug(article.CMSProperties.Root, article.CMSProperties.Category1, article.CMSProperties.Category2, article.CMSProperties.Category3, article.CMSProperties.Category4, article.CMSProperties.Category5);
					result.Add(categoryKey, article.ID);
				}
				return result.ToDictionary();
			},
			keyComparer: StringComparer.InvariantCultureIgnoreCase
		);

		_categoriesByCategorySlug = new BulkFetchActionCache<string, string[]>(
			() => {
				var result = new LookupEx<string, string>(StringComparer.InvariantCultureIgnoreCase);
				foreach (var article in _registry.Articles) {
					if (string.IsNullOrWhiteSpace(article.CMSProperties.Root))
						continue;
					result.Add(string.Empty, article.CMSProperties.Root);

					if (string.IsNullOrWhiteSpace(article.CMSProperties.Category1))
						continue;
					result.Add(LocalNotionHelper.CreateCategorySlug(article.CMSProperties.Root, null, null, null, null, null), article.CMSProperties.Category1);

					if (string.IsNullOrWhiteSpace(article.CMSProperties.Category2))
						continue;
					result.Add(LocalNotionHelper.CreateCategorySlug(article.CMSProperties.Root, article.CMSProperties.Category1, null, null, null, null), article.CMSProperties.Category2);

					if (string.IsNullOrWhiteSpace(article.CMSProperties.Category3))
						continue;
					result.Add(LocalNotionHelper.CreateCategorySlug(article.CMSProperties.Root, article.CMSProperties.Category1, article.CMSProperties.Category2, null, null, null), article.CMSProperties.Category3);

					if (string.IsNullOrWhiteSpace(article.CMSProperties.Category4))
						continue;
					result.Add(LocalNotionHelper.CreateCategorySlug(article.CMSProperties.Root, article.CMSProperties.Category1, article.CMSProperties.Category2, article.CMSProperties.Category3, null, null), article.CMSProperties.Category4);

					if (string.IsNullOrWhiteSpace(article.CMSProperties.Category5))
						continue;
					result.Add(LocalNotionHelper.CreateCategorySlug(article.CMSProperties.Root, article.CMSProperties.Category1, article.CMSProperties.Category2, article.CMSProperties.Category3, article.CMSProperties.Category4, null), article.CMSProperties.Category5);

				}
				return result.ToDictionary();
			},
			keyComparer: StringComparer.InvariantCultureIgnoreCase
		);
		RequiresLoad = true;
		RepositoryPath = repoFile;
	}

	public int Version => _registry.Version;

	public ILogger Logger => _logger;

	public string DefaultTemplate => _registry.DefaultTemplate;

	public LocalNotionMode Mode => _registry.Mode;

	public IReadOnlyDictionary<string, string> RootTemplates => _registry.RootTemplates.AsReadOnly();

	public string RepositoryPath { get; init; }

	public bool RequiresLoad { get; private set; }

	public bool RequiresSave { get; private set; }

	public IEnumerable<string> Objects => _objectStore.FileKeys.Select(LocalNotionHelper.ObjectGuidToId);

	public IEnumerable<LocalNotionResource> Resources => _registry.Resources;

	public string BaseUrl {
		get => _registry.BaseUrl;
		set {
			if (_registry.BaseUrl == value)
				return;
			_registry.BaseUrl = value;
			RequiresSave = true;
		}
	}

	public string ObjectsPath => Path.GetFullPath(_registry.ObjectsRelPath, Tools.FileSystem.GetParentDirectoryPath(RepositoryPath));

	public string PagesPath => Path.GetFullPath(_registry.PagesRelPath, Tools.FileSystem.GetParentDirectoryPath(RepositoryPath));

	public string FilesPath => Path.GetFullPath(_registry.FilesRelPath, Tools.FileSystem.GetParentDirectoryPath(RepositoryPath));

	public string TemplatesPath => Path.GetFullPath(_registry.TemplatesRelPath, Tools.FileSystem.GetParentDirectoryPath(RepositoryPath));

	public string LogsPath => Path.GetFullPath(_registry.LogsRelPath, Tools.FileSystem.GetParentDirectoryPath(RepositoryPath));

	public string DefaultNotionApiKey => _registry.NotionApiKey;

	public static async Task<LocalNotionRepository> CreateNew(
		string repoFile,
		string notionApiKey = null,
		string objectsPath = null,
		string pagesPath = null,
		string filesPath = null,
		string templatesPath = null,
		string logsPath = null,
		LocalNotionMode mode = LocalNotionMode.Offline,
		string baseUrl = null,
		LogLevel logLevel = LogLevel.Info,
		IDictionary<string, string> rootTemplates = null,
		ILogger logger = null
	) {
		Guard.ArgumentNotNull(repoFile, nameof(repoFile));
		Guard.FileNotExists(repoFile);
		// normalize arguments
		repoFile =  Tools.FileSystem.ToAbsolutePathIfRelative(repoFile);
		var repoParentFolder = Tools.FileSystem.GetParentDirectoryPath(repoFile);
		objectsPath = Tools.FileSystem.ToAbsolutePathIfRelative(objectsPath ?? Constants.DefaultObjectsFolder, repoParentFolder);
		pagesPath = Tools.FileSystem.ToAbsolutePathIfRelative(pagesPath ?? Constants.DefaultPagesFolder, repoParentFolder);
		filesPath = Tools.FileSystem.ToAbsolutePathIfRelative(filesPath ?? Constants.DefaultFilesFolder, repoParentFolder);
		templatesPath = Tools.FileSystem.ToAbsolutePathIfRelative(templatesPath ?? Constants.DefaultTemplatesFolder, repoParentFolder);
		logsPath = Tools.FileSystem.ToAbsolutePathIfRelative(logsPath ?? Constants.DefaultLogsFolder, repoParentFolder);
		baseUrl ??= mode == LocalNotionMode.Offline ? Constants.DefaultOfflineBaseUrl : Constants.DefaultOnlineBaseUrl;

		if (File.Exists(repoFile))
			await Tools.FileSystem.DeleteFileAsync(repoFile);

		if (Directory.Exists(objectsPath))
			await Tools.FileSystem.DeleteDirectoryAsync(objectsPath);

		if (Directory.Exists(pagesPath))
			await Tools.FileSystem.DeleteDirectoryAsync(pagesPath);

		if (Directory.Exists(filesPath))
			await Tools.FileSystem.DeleteDirectoryAsync(filesPath);

		if (Directory.Exists(templatesPath))
			await Tools.FileSystem.DeleteDirectoryAsync(templatesPath);

		if (Directory.Exists(logsPath))
			await Tools.FileSystem.DeleteDirectoryAsync(logsPath);

		var resourcesCollection = new LocalNotionRegistry {
			NotionApiKey = notionApiKey,
			Mode = mode,
			BaseUrl = baseUrl,
			ObjectsRelPath = Tools.FileSystem.GetRelativePath(repoParentFolder, objectsPath),
			PagesRelPath = Tools.FileSystem.GetRelativePath(repoParentFolder, pagesPath),
			FilesRelPath = Tools.FileSystem.GetRelativePath(repoParentFolder, filesPath),
			TemplatesRelPath = Tools.FileSystem.GetRelativePath(repoParentFolder, templatesPath),
			LogsRelPath = Tools.FileSystem.GetRelativePath(repoParentFolder, logsPath),
			LogLevel = logLevel,
			Resources = Array.Empty<LocalNotionResource>()
		};

		if (rootTemplates != null)
			resourcesCollection.RootTemplates = rootTemplates.ToDictionary();

		var repoParentDir = Tools.FileSystem.GetParentDirectoryPath(repoFile);
		if (!Directory.Exists(repoParentDir))
			await Tools.FileSystem.CreateDirectoryAsync(repoParentDir);

		Tools.Json.WriteToFile(repoFile, resourcesCollection);

		var repo = new LocalNotionRepository(repoFile, logger);
		await repo.Load();
		return repo;
	}

	public static async Task Remove(string repoFile, ILogger logger = null) {
		// normalize arguments
		repoFile =  Tools.FileSystem.ToAbsolutePathIfRelative(repoFile ?? Constants.DefaultRepositoryFilename, Environment.CurrentDirectory);
		logger ??= new NoOpLogger();

		var repo = await LocalNotionRepository.Open(repoFile, logger);

		logger.Info("Removing objects");
		await Tools.FileSystem.DeleteDirectoryAsync(repo.ObjectsPath, true);
		if (Tools.FileSystem.IsDirectoryEmpty(Tools.FileSystem.GetParentDirectoryPath(repo.ObjectsPath)))
			await Tools.FileSystem.DeleteDirectoryAsync(Tools.FileSystem.GetParentDirectoryPath(repo.ObjectsPath));

		logger.Info("Removing files");
		await Tools.FileSystem.DeleteDirectoryAsync(repo.FilesPath, true);
		if (Tools.FileSystem.IsDirectoryEmpty(Tools.FileSystem.GetParentDirectoryPath(repo.FilesPath)))
			await Tools.FileSystem.DeleteDirectoryAsync(Tools.FileSystem.GetParentDirectoryPath(repo.FilesPath));

		logger.Info("Removing pages");
		await Tools.FileSystem.DeleteDirectoryAsync(repo.PagesPath, true);
		if (Tools.FileSystem.IsDirectoryEmpty(Tools.FileSystem.GetParentDirectoryPath(repo.PagesPath)))
			await Tools.FileSystem.DeleteDirectoryAsync(Tools.FileSystem.GetParentDirectoryPath(repo.PagesPath));

		logger.Info("Removing templates");
		await Tools.FileSystem.DeleteDirectoryAsync(repo.TemplatesPath, true);
		if (Tools.FileSystem.IsDirectoryEmpty(Tools.FileSystem.GetParentDirectoryPath(repo.TemplatesPath)))
			await Tools.FileSystem.DeleteDirectoryAsync(Tools.FileSystem.GetParentDirectoryPath(repo.TemplatesPath));

		logger.Info("Removing logs");
		await Tools.FileSystem.DeleteDirectoryAsync(repo.LogsPath, true);
		if (Tools.FileSystem.IsDirectoryEmpty(Tools.FileSystem.GetParentDirectoryPath(repo.LogsPath)))
			await Tools.FileSystem.DeleteDirectoryAsync(Tools.FileSystem.GetParentDirectoryPath(repo.LogsPath));

		logger.Info("Removing repo");
		await Tools.FileSystem.DeleteFileAsync(repoFile);

		// TODO: need to remove system logger logger when repo closes
	}

	public static async Task<LocalNotionRepository> Open(string repoFile, ILogger logger = null) {
		Guard.ArgumentNotNull(repoFile, nameof(repoFile));
		if (!File.Exists(repoFile))
			throw new FileNotFoundException(repoFile);
		var repo = new LocalNotionRepository(repoFile, logger);
		await repo.Load();
		return repo;
	}

	public async Task Load() {
		CheckNotLoaded();
		_resourceBySlug.Flush();
		_articlesByCategorySlug.Flush();
		_categoriesByCategorySlug.Flush();
		if (File.Exists(RepositoryPath)) {
			_registry = await Task.Run(() => Tools.Json.ReadFromFile<LocalNotionRegistry>(RepositoryPath));
		} else {
			_registry = new LocalNotionRegistry();
		}

		// Load the resource lookup
		_resourcesByNID = _registry.Resources.ToDictionary(x => x.ID);

		// Ensure resource folders exist
		var parentFolder = Tools.FileSystem.GetParentDirectoryPath(RepositoryPath);

		if (!Directory.Exists(ObjectsPath))
			await Tools.FileSystem.CreateDirectoryAsync(ObjectsPath).ConfigureAwait(false);

		if (!Directory.Exists(PagesPath))
			await Tools.FileSystem.CreateDirectoryAsync(PagesPath);

		if (!Directory.Exists(FilesPath))
			await Tools.FileSystem.CreateDirectoryAsync(FilesPath);

		if (!Directory.Exists(TemplatesPath))
			await Tools.FileSystem.CreateDirectoryAsync(TemplatesPath);

		if (!Directory.Exists(LogsPath))
			await Tools.FileSystem.CreateDirectoryAsync(LogsPath);

		// Prepare repository logger
		_logger.Add(
			new ThreadIdLogger(new TimestampLogger(new RollingFileLogger(Path.Combine(LogsPath, Constants.DefaultLogFileName)))) {
				Options = _registry.LogLevel.ToLogOptions()
			}
		);

		// Create object manager
		_objectStore = new GuidFileStore(ObjectsPath) { FileExtension = ".json" };

		// Create template manager (will extract missing templates on ctor)
		new HtmlTemplateManager(TemplatesPath, _logger);

		// Go through and ensure no dangling resources
		var resourceToRemove = new List<LocalNotionResource>();
		var foldersToRemove = new List<string>();
		foreach (var resource in _registry.Resources) {
			var folder = DetermineResourceFolder(resource.Type, resource.ID, FileSystemPathType.Absolute);
			if (!Directory.Exists(folder)) {
				// registered resource but no folder exists for it
				resourceToRemove.Add(resource);
			} else if (Directory.GetFiles(folder).Length == 0) {
				// registered resource but folder is empty
				resourceToRemove.Add(resource);
				foldersToRemove.Add(folder);
			}
		}
		var savedPageIDs = Tools.FileSystem.GetSubDirectories(DetermineResourceTypeFolder(LocalNotionResourceType.Page, FileSystemPathType.Absolute), FileSystemPathType.Absolute);
		var savedFileIDs = Tools.FileSystem.GetSubDirectories(DetermineResourceTypeFolder(LocalNotionResourceType.File, FileSystemPathType.Absolute), FileSystemPathType.Absolute);
		foreach (var resourceFolder in savedPageIDs.Concat(savedFileIDs)) {
			var resourceID = Path.GetFileName(resourceFolder);
			// resource folder exists but is not registered
			if (!_resourcesByNID.ContainsKey(resourceID))
				foldersToRemove.Add(resourceFolder);
		}

		foreach (var resource in resourceToRemove) {
			RequiresSave = true;
			_registry.Remove(resource);
			_resourcesByNID.Remove(resource.ID);
		}

		foreach (var folder in foldersToRemove) {
			await Tools.FileSystem.DeleteDirectoryAsync(folder);
		}
		RequiresLoad = false;
		if (RequiresSave)
			await Save();

	}

	public async Task Save() {
		CheckLoaded();
		await Task.Run(() => Tools.Json.WriteToFile(RepositoryPath, _registry));
		RequiresSave = false;
	}

	public IUrlResolver CreateUrlResolver()
		=> _registry.Mode switch {
			LocalNotionMode.Offline => new OfflinePathResolver(this),
			LocalNotionMode.Online => new OnlineUrlResolver(this, $"{BaseUrl.TrimEnd('/')}/{{slug}}"),
			_ => throw new ArgumentOutOfRangeException()
		};

	public bool TryGetObject(string objectId, out IFuture<IObject> @object) {
		CheckLoaded();
		var path = _objectStore.GetFilePath(LocalNotionHelper.ObjectIdToGuid(objectId));
		if (!File.Exists(path)) {
			@object = null;
			return false;
		}
		@object = LazyLoad<IObject>.From(() => Tools.Json.ReadFromFile<IObject>(path));
		return true;
	}

	public void AddObject(IObject @object) {
		CheckLoaded();
		var objectGuid = LocalNotionHelper.ObjectIdToGuid(@object.Id);
		_objectStore.RegisterFile(objectGuid);
		Tools.Json.WriteToFile(_objectStore.GetFilePath(objectGuid), @object);
	}

	public virtual void DeleteObject(string objectId) {
		CheckLoaded();
		_objectStore.Delete(LocalNotionHelper.ObjectIdToGuid(objectId));

	}

	public virtual bool TryGetResource(string resourceID, out LocalNotionResource localNotionResource) {
		CheckLoaded();
		if (!_resourcesByNID.TryGetValue(resourceID, out var resource)) {
			localNotionResource = null;
			return false;
		}
		localNotionResource = resource;
		return true;
	}

	public bool TryLookupResourceBySlug(string slug, out string resourceID) {
		CheckLoaded();
		resourceID = _resourceBySlug[slug];
		return resourceID != null;		
	}

	public IEnumerable<LocalNotionResource> GetResourceAncestry(string resourceId) {
		var visited = new HashSet<string>();
		return GetResourceAncestryInternal(resourceId);

		IEnumerable<LocalNotionResource> GetResourceAncestryInternal(string resourceId) {
			if (resourceId is null || visited.Contains(resourceId))
				yield break;

			if (!TryGetResource(resourceId, out var resource))
				yield break;

			visited.Add(resourceId);

			yield return resource;

			// TODO: need Resource base for Page & DB
			if (resource is LocalNotionPage lnp) {
				foreach (var ancestor in GetResourceAncestryInternal(lnp.Parent)) {
					yield return ancestor;
				}
			}
		}
	}

	public virtual void AddResource(LocalNotionResource resource) {
		CheckLoaded();
		Guard.ArgumentNotNull(resource, nameof(resource));
		Guard.Argument(string.IsNullOrWhiteSpace(resource.LocalPath), nameof(resource), "Path must be null (or whitespace)");
		Guard.Against(_resourcesByNID.ContainsKey(resource.ID), $"Resource '{resource.ID}' already registered");
		var resourceFolder = DetermineResourceFolder(resource.Type, resource.ID, FileSystemPathType.Absolute);
		Guard.Against(Directory.Exists(resourceFolder), $"Resource '{resource.ID}' path was already existing (dangling resource)");
		Directory.CreateDirectory(resourceFolder);

		resource.LocalPath =
			resource switch {
				LocalNotionPage => resource.LocalPath = resource.ID,  // page has multiple filenames depending on the render tyype
				_ => Path.Combine(resource.ID, Tools.FileSystem.ToValidFolderOrFilename(resource.Title ?? Constants.DefaultResourceTitle))
			};

		RequiresSave = true;
		_registry.Add(resource);
		_resourcesByNID.Add(resource.ID, resource);
		_resourceBySlug.Flush();
		if (resource is LocalNotionPage { CMSProperties: not null } ) {
			_articlesByCategorySlug.Flush();
			_categoriesByCategorySlug.Flush();
		}
	}

	public virtual void DeleteResource(string resourceID) {
		CheckLoaded();
		Guard.ArgumentNotNullOrWhitespace(resourceID, nameof(resourceID));
		if (_resourcesByNID.TryGetValue(resourceID, out var resource)) {
			//var resourcePath = Path.GetFullPath(resource.LocalPath, DetermineResourceTypeFolder(resource.Type, FileSystemPathType.Absolute));
			//var resourceParentFolder = Tools.FileSystem.GetParentDirectoryPath(resourcePath);

			var resourceFolder = resource.Type switch {
				LocalNotionResourceType.File => Tools.FileSystem.GetParentDirectoryPath(Path.Combine(FilesPath, resource.LocalPath)),
				LocalNotionResourceType.Page => Path.Combine(PagesPath, resource.LocalPath),
				_ => throw new NotSupportedException(resource.Type.ToString())
			};

			if (Directory.Exists(resourceFolder))
				Tools.FileSystem.DeleteDirectory(resourceFolder);
			RequiresSave = true;
			_registry.Remove(resource);
			_resourcesByNID.Remove(resourceID);
			_resourceBySlug.Flush();
			if (resource is  LocalNotionPage { CMSProperties: not null }) {
				_articlesByCategorySlug.Flush();
				_categoriesByCategorySlug.Flush();
			}
		} else throw new InvalidOperationException($"Resource {resourceID} not found");
	}

	public virtual bool TryGetPage(string pageId, out LocalNotionPage page) {
		CheckLoaded();
		if (!TryGetResource(pageId, out var resource)) {
			page = default;
			return false;
		}
		Guard.Ensure(resource is LocalNotionPage, $"Not a {nameof(LocalNotionPage)}");
		page = (LocalNotionPage)resource;
		return true;
	}

	public virtual void AddPage(LocalNotionPage page) {
		CheckLoaded();
		AddResource(page);
	}

	public virtual void DeletePage(string pageId) {
		CheckLoaded();
		DeleteResource(pageId);
	}

	public virtual bool TryGetPageGraph(string pageId, out IFuture<NotionObjectGraph> page) {
		CheckLoaded();
		var pageGraphPath = CalculatePageGraphPath(pageId);
		if (!File.Exists(pageGraphPath)) {
			page = default;
			return false;
		}
		page = LazyLoad<NotionObjectGraph>.From(() => Tools.Json.ReadFromFile<NotionObjectGraph>(pageGraphPath));
		return true;
	}

	public virtual void AddPageGraph(string pageId, NotionObjectGraph pageGraph) {
		CheckLoaded();
		Guard.ArgumentNotNull(pageId, nameof(pageId));
		Guard.ArgumentNotNull(pageGraph, nameof(pageGraph));
		Guard.Ensure(pageId == pageGraph.ObjectID, $"Mismatch between argument object identifiers");
		var pageGraphPath = CalculatePageGraphPath(pageGraph.ObjectID);
		Tools.Json.WriteToFile(pageGraphPath, pageGraph);
	}

	public virtual void DeletePageGraph(string pageId) {
		CheckLoaded();
		var renderPath = CalculatePageGraphPath(pageId);
		File.Delete(renderPath);
	}

	public virtual string ImportPageRender(string pageId, RenderOutput renderOutput, string renderedFile) {
		Guard.ArgumentNotNull(pageId, nameof(pageId));
		Guard.ArgumentNotNull(renderedFile, nameof(renderedFile));
		Guard.FileExists(renderedFile);
		CheckLoaded();
		var page = this.GetPage(pageId);
		if (page.Renders.ContainsKey(renderOutput)) {
			var file = page.Renders[renderOutput];
			if (File.Exists(file))
				File.Delete(file);
			page.Renders.Remove(renderOutput);
		}

		var renderFilename = CalculatePageRenderFilename(pageId, renderOutput);
		var filePath = Path.Combine(PagesPath, page.LocalPath, renderFilename);
		Tools.FileSystem.CopyFile(renderedFile, filePath, true, true);
		page.Renders[renderOutput] = renderFilename;
		RequiresSave = true;
		return filePath;
	}

	public virtual void DeletePageRender(string pageId, RenderOutput renderOutput) {
		CheckLoaded();
		string fileToDelete;
		var page = this.GetPage(pageId);
		if (page.Renders.TryGetValue(renderOutput, out var relPath)) {
			fileToDelete = Path.GetFullPath(relPath, DetermineResourceTypeFolder(LocalNotionResourceType.Page, FileSystemPathType.Absolute));
			if (File.Exists(fileToDelete))
				File.Delete(fileToDelete);
			page.Renders.Remove(renderOutput);
			RequiresSave = true;
		} 
	}

	public virtual string CalculatePageRenderFilename(string pageID, RenderOutput renderOutput) {
		var page = this.GetPage(pageID);
		var ext = renderOutput.GetAttribute<FileExtensionAttribute>().FileExtension;
		var pageFilename = Tools.FileSystem.ToValidFolderOrFilename(page.Title.ToNullWhenWhitespace() ?? Constants.DefaultResourceTitle) + $".{ext.TrimStart('.')}";
		return pageFilename;
	}

	public virtual string CalculatePageRenderPath(string pageID, RenderOutput renderOutput)
		=> Path.Combine(PagesPath, pageID, CalculatePageRenderFilename(pageID, renderOutput));

	public virtual bool TryGetFile(string fileId, out LocalNotionFile localFile) {
		CheckLoaded();
		if (!TryGetResource(fileId, out var resource)) {
			localFile = default;
			return false;
		}
		Guard.Ensure(resource is LocalNotionFile, $"Not a {nameof(LocalNotionFile)}");
		localFile = (LocalNotionFile)resource;
		return true;
	}

	public virtual LocalNotionFile RegisterFile(string fileId, string filename) {
		CheckLoaded();
		var localNotionFile = LocalNotionHelper.ParseFile(fileId, filename, _registry.FilesRelPath);
		AddResource(localNotionFile);
		return localNotionFile;
	}

	public virtual bool TryGetFileContents(string fileID, out string internalFile) {
		CheckLoaded();
		var file = this.GetFile(fileID);
		internalFile = Path.GetFullPath(file.LocalPath, DetermineResourceTypeFolder(LocalNotionResourceType.File, FileSystemPathType.Absolute));
		return File.Exists(internalFile);
	}

	public virtual void ImportFileContents(string fileId, string localFilePath) {
		Guard.ArgumentNotNull(fileId, nameof(fileId));
		Guard.ArgumentNotNull(localFilePath, nameof(localFilePath));
		Guard.FileExists(localFilePath);
		CheckLoaded();
		var destPath = Path.GetFullPath(this.GetFile(fileId).LocalPath, DetermineResourceTypeFolder(LocalNotionResourceType.File, FileSystemPathType.Absolute));
		Tools.FileSystem.CopyFile(localFilePath, destPath, true, true);
	}

	public virtual void DeleteFile(string fileId) {
		CheckLoaded();
		DeleteResource(fileId); // deletes all files in folder
	}

	public IEnumerable<LocalNotionPage> GetNotionCMSPages(string root, params string[] categories) {
		CheckLoaded();
		return _articlesByCategorySlug[LocalNotionHelper.CreateCategorySlug(root, categories)].Select(nid => _resourcesByNID[nid]).Cast<LocalNotionPage>();
	}

	public string[] GetRoots() {
		CheckLoaded();
		return _categoriesByCategorySlug[string.Empty];
	}

	public string[] GetSubCategories(string root, params string[] categories) {
		CheckLoaded();
		return _categoriesByCategorySlug[LocalNotionHelper.CreateCategorySlug(root, categories)];
	}

	//protected string DeterminePageRenderPath(string pageID, string renderFileExt, string renderFileNameOverride = null) {
	//	CheckLoaded();
	//	Guard.ArgumentNotNull(pageID, nameof(pageID));
	//	Guard.ArgumentNotNull(renderFileExt, nameof(renderFileExt));
	//	Guard.Ensure(this.ContainsPage(pageID));
	//	if (!string.IsNullOrWhiteSpace(renderFileNameOverride)) {
	//		return Path.Combine(DetermineResourceFolder(LocalNotionResourceType.Page, pageID, FileSystemPathType.Absolute), renderFileNameOverride + renderFileExt);
	//	}
	//	var page = this.GetResource(pageID);
	//	var pageFilename = (renderFileNameOverride ?? Tools.FileSystem.ToValidFolderOrFilename(page.Title.ToNullWhenWhitespace() ?? Constants.DefaultResourceTitle)) + $".{renderFileExt.TrimStart('.')}";
	//	return Path.GetFullPath(Path.Combine(page.LocalPath, pageFilename), DetermineResourceTypeFolder(LocalNotionResourceType.Page, FileSystemPathType.Absolute));
	//}

	protected string CalculatePageGraphPath(string pageID)
		=> Path.Combine(PagesPath, pageID, Constants.PageGraphFilename);

	private string DetermineResourceTypeFolder(LocalNotionResourceType resourceType, FileSystemPathType pathType) {
		var resourceTypeFolder = resourceType switch {
			// these are relative
			LocalNotionResourceType.File => _registry.FilesRelPath,
			LocalNotionResourceType.Page => _registry.PagesRelPath,
			_ => throw new ArgumentOutOfRangeException(nameof(resourceType), resourceType, null)
		};

		return pathType switch {
			FileSystemPathType.Absolute => Path.GetFullPath(resourceTypeFolder, Tools.FileSystem.GetParentDirectoryPath(RepositoryPath)),
			FileSystemPathType.Relative => resourceTypeFolder,
			_ => throw new ArgumentOutOfRangeException(nameof(pathType), pathType, null)
		};
	}

	private string DetermineResourceFolder(LocalNotionResourceType resourceType, string objectId, FileSystemPathType pathType)
		=> Path.Combine(DetermineResourceTypeFolder(resourceType, pathType), objectId);

	private void CheckNotLoaded() {
		if (!RequiresLoad)
			throw new InvalidOperationException($"{nameof(LocalNotionRepository)} was loaded");
	}

	private void CheckLoaded() {
		if (RequiresLoad)
			throw new InvalidOperationException($"{nameof(LocalNotionRepository)} was not loaded");
	}

}