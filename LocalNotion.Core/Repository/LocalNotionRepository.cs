using System.Runtime.CompilerServices;
using AngleSharp.Html.Dom;
using Hydrogen;
using Hydrogen.Data;
using Microsoft.Win32;
using Notion.Client;

namespace LocalNotion.Core;


// Make ThreadSafe
public class LocalNotionRepository : ILocalNotionRepository, IAsyncLoadable, IAsyncSaveable {

	public event EventHandlerEx<object> Loading;
	public event EventHandlerEx<object> Loaded;
	public event EventHandlerEx<object> Changing;
	public event EventHandlerEx<object> Changed;
	public event EventHandlerEx<object> Saving;
	public event EventHandlerEx<object> Saved;
	public event EventHandlerEx<object> Clearing;
	public event EventHandlerEx<object> Cleared;
	public event EventHandlerEx<object, string> ResourceAdding;
	public event EventHandlerEx<object, LocalNotionResource> ResourceAdded;
	public event EventHandlerEx<object, string> ResourceUpdating;
	public event EventHandlerEx<object, LocalNotionResource> ResourceUpdated;
	public event EventHandlerEx<object, string> ResourceRemoving;
	public event EventHandlerEx<object, LocalNotionResource> ResourceRemoved;

	private LocalNotionRegistry _registry;
	private GuidStringFileStore _objectStore;
	private GuidStringFileStore _graphStore;
	
	private IDictionary<string, LocalNotionResource> _resourcesByNID;
	private readonly MulticastLogger _logger;
	private readonly string _registryPath;

	public LocalNotionRepository(string registryFile, ILogger logger = null) {
		Guard.ArgumentNotNull(registryFile, nameof(registryFile));
		Guard.FileExists(registryFile);
		_logger = new MulticastLogger();
		if (logger != null)
			_logger.Add(logger);
		
		RequiresLoad = true;

		// These are set when loaded
		_registryPath = registryFile;
		_registry = null;
		_objectStore = null;
		_graphStore = null;
		_resourcesByNID = null;
		Paths = null;
	}

	public int Version => _registry.Version;

	public ILogger Logger => _logger;

	public string DefaultTemplate => _registry.DefaultTheme;

	public IReadOnlyDictionary<string, string> ThemeMaps => _registry.ThemeMaps.AsReadOnly();

	public IPathResolver Paths { get; private set; }

	public string DefaultNotionApiKey => _registry.NotionApiKey;

	public IEnumerable<string> Objects => _objectStore.FileKeys;

	public IEnumerable<string> Graphs => _graphStore.FileKeys;

	public IEnumerable<LocalNotionResource> Resources => _registry.Resources;

	public bool RequiresLoad { get; private set; }

	public bool RequiresSave { get; private set; }

	protected bool SuppressNotifications { get; set; }

	public static async Task<LocalNotionRepository> CreateNew(
		string repoPath,
		string notionApiKey = null,
		string theme = null,
		LogLevel logLevel = LogLevel.Info,
		LocalNotionPathProfile pathProfile = null,
		IDictionary<string, string> rootTemplates = null,
		ILogger logger = null
	) {
		Guard.ArgumentNotNull(repoPath, nameof(repoPath));
		Guard.DirectoryExists(repoPath);
	
		// Backup theme
		theme ??= Constants.DefaultTheme;

		// The registry file is computed from the profile
		pathProfile ??= LocalNotionPathProfile.Default;

		var registryFile = Path.GetFullPath(pathProfile.RegistryPathR, repoPath);
		Guard.FileNotExists(registryFile);

		// Remove dangling files
		await Remove(repoPath, logger);

		// create registry objects
		var registry = new LocalNotionRegistry {
			NotionApiKey = notionApiKey,
			DefaultTheme = theme,
			Paths = pathProfile,
			LogLevel = logLevel,
			Resources = Array.Empty<LocalNotionResource>()
		};

		if (rootTemplates != null)
			registry.ThemeMaps = rootTemplates.ToDictionary();

		
		// Create folders
		var pathResolver = new PathResolver(repoPath, pathProfile);

		// Registry folder
		var registryParentFolder = Tools.FileSystem.GetParentDirectoryPath(registryFile);
		if (!Directory.Exists(registryParentFolder)) {
			if (Path.GetFileName(registryParentFolder) == Constants.DefaultRegistryFolderName)
				Directory.CreateDirectory(registryParentFolder);
			else
				// LN only creates .localnotion folders. For use-cases involving a custom path profile that changes
				// this foldername, the user must manually create this folder. This i 
				throw new DirectoryNotFoundException(registryParentFolder);  
		} else {
			Guard.Ensure(Tools.FileSystem.IsDirectoryEmpty(registryParentFolder), "Registry was not empty");
		}
		
		// internal resource paths
		foreach (var internalResourceType in Enum.GetValues<InternalResourceType>()) {
			var internalResourceFolderPath = pathResolver.GetInternalResourceFolderPath(internalResourceType, FileSystemPathType.Absolute);	
			if (!Directory.Exists(internalResourceFolderPath))
				await Tools.FileSystem.CreateDirectoryAsync(internalResourceFolderPath);
		}

		// resource paths
		foreach (var resourceType in Enum.GetValues<LocalNotionResourceType>()) {
			var resourceTypeFolderPath = pathResolver.GetResourceTypeFolderPath(resourceType, FileSystemPathType.Absolute);	
			if (!Directory.Exists(resourceTypeFolderPath))
				await Tools.FileSystem.CreateDirectoryAsync(resourceTypeFolderPath);
		}

		// create registry
		Tools.Json.WriteToFile(registryFile, registry);

		var repo = new LocalNotionRepository(registryFile, logger);
		await repo.Load();
		return repo;
	}

	public static async Task Remove(string repoPath, ILogger logger = null) {
		Guard.ArgumentNotNullOrEmpty(repoPath, nameof(repoPath));
		Guard.DirectoryExists(repoPath);
		logger ??= new NoOpLogger();
		var registryFilePath = Path.Join(repoPath, LocalNotionPathProfile.Default.RegistryPathR);
		if (File.Exists(registryFilePath))
			await RemoveUsingRegistry(registryFilePath, logger);

		var registryParentFolder = Path.Join(repoPath, Tools.FileSystem.GetParentDirectoryPath(registryFilePath));
		if (Directory.Exists(registryParentFolder))
			await Tools.FileSystem.DeleteDirectoryAsync(registryParentFolder); // this can happen if registry file is deleted but this folder was left
	}

	public static async Task RemoveUsingRegistry(string registryPath, ILogger logger = null) {
		Guard.ArgumentNotNull(registryPath, nameof(registryPath));
		Guard.FileExists(registryPath);
		logger ??= new NoOpLogger();
		var registry = Tools.Json.ReadFromFile<LocalNotionRegistry>(registryPath);
		var pathResolver = new PathResolver(Path.GetFullPath(registry.Paths.RepositoryPathR, Path.GetDirectoryName(registryPath)), registry.Paths);
		await RemoveInternal(pathResolver, logger);
		
		// Delete any dangling renders
		foreach(var render in registry.Resources.SelectMany(x => x.Renders.Values).Select(x=>x.LocalPath)) {
			var fullRenderPath = Path.Combine(pathResolver.GetRepositoryPath(FileSystemPathType.Absolute), render);
			if (File.Exists(fullRenderPath)) {
				logger.Info($"Removing {fullRenderPath}");
				try {
					await Tools.FileSystem.DeleteFileAsync(fullRenderPath);
				} catch (Exception error) {
					logger.Exception(error);
				}
			}
		}
	}

	private static async Task RemoveInternal(IPathResolver pathResolver, ILogger logger = null) {
		logger ??= new NoOpLogger();

		var registryFile = pathResolver.GetRegistryFilePath(FileSystemPathType.Absolute);
		
		// Cleanup parent folder
		var registryParentFolder = Tools.FileSystem.GetParentDirectoryPath(registryFile);
		if (Directory.Exists(registryParentFolder) && !Tools.FileSystem.IsDirectoryEmpty(registryParentFolder)) {
			logger.Info($"Removing {registryParentFolder}");
			await Tools.FileSystem.DeleteDirectoryAsync(registryParentFolder);
		}
		
		// Delete resource paths if exists
		var repoPath = Tools.FileSystem.GetCaseCorrectDirectoryPath(pathResolver.GetRepositoryPath(FileSystemPathType.Absolute));
		foreach(var resourceType in Enum.GetValues<LocalNotionResourceType>()) {
			var resourceTypePath = pathResolver.GetResourceTypeFolderPath(resourceType, FileSystemPathType.Absolute);
			if (Directory.Exists(resourceTypePath) && Tools.FileSystem.GetCaseCorrectDirectoryPath(resourceTypePath) != repoPath ) {
				logger.Info($"Removing {resourceTypePath}");
				await Tools.FileSystem.DeleteDirectoryAsync(resourceTypePath);
			}
		}

		// Delete internal resource paths if exists
		foreach(var internalResourceType in Enum.GetValues<InternalResourceType>()) {
			var resourceTypePath = pathResolver.GetInternalResourceFolderPath(internalResourceType, FileSystemPathType.Absolute);
			if (Directory.Exists(resourceTypePath) && !Tools.FileSystem.IsDirectoryEmpty(resourceTypePath)) {
				logger.Info($"Removing {resourceTypePath}");
				await Tools.FileSystem.DeleteDirectoryAsync(resourceTypePath);
			}
		}
	}

	public static Task<LocalNotionRepository> Open(string repositoryFolder, ILogger logger = null) {
		Guard.ArgumentNotNull(repositoryFolder, nameof(repositoryFolder));
		if (!Directory.Exists(repositoryFolder))
			throw new DirectoryNotFoundException(repositoryFolder);
		return OpenRegistry(PathResolver.ResolveDefaultRegistryFilePath(repositoryFolder), logger);
	}
	
	public static async Task<LocalNotionRepository> OpenRegistry(string registryFile, ILogger logger = null) {
		Guard.ArgumentNotNull(registryFile, nameof(registryFile));
		if (!File.Exists(registryFile))
			throw new FileNotFoundException(registryFile);
		var repo = new LocalNotionRepository(registryFile, logger);
		await repo.Load();
		return repo;
	}

	public async Task Load() {
		CheckNotLoaded();
		Guard.FileExists(_registryPath);

		// load the registry
		_registry = await Task.Run(() => Tools.Json.ReadFromFile<LocalNotionRegistry>(_registryPath));

		// create path resolver
		Paths = new PathResolver(Path.GetFullPath(_registry.Paths.RepositoryPathR, Path.GetDirectoryName(_registryPath)), _registry.Paths);

		// create the resource lookup table
		_resourcesByNID = _registry.Resources.ToDictionary(x => x.ID);

		// Prepare repository logger
		_logger.Add(
			new ThreadIdLogger(new TimestampLogger(new RollingFileLogger(Path.Combine(Paths.GetInternalResourceFolderPath(InternalResourceType.Logs, FileSystemPathType.Absolute), Constants.DefaultLogFilename)))) {
				Options = _registry.LogLevel.ToLogOptions()
			}
		);
		SystemLog.RegisterLogger(_logger);

		// Create object store
		_objectStore = new GuidStringFileStore(Paths.GetInternalResourceFolderPath(InternalResourceType.Objects, FileSystemPathType.Absolute), LocalNotionHelper.ObjectGuidToId, LocalNotionHelper.ObjectIdToGuid, fileExtension: ".json" );
		
		// Create graph store
		_graphStore = new GuidStringFileStore(Paths.GetInternalResourceFolderPath(InternalResourceType.Graphs, FileSystemPathType.Absolute), LocalNotionHelper.ObjectGuidToId, LocalNotionHelper.ObjectIdToGuid, fileExtension: ".json" );

		// Create template manager (will extract missing templates on ctor)
		HtmlThemeManager.ExtractEmbeddedThemes(Paths.GetInternalResourceFolderPath(InternalResourceType.Themes, FileSystemPathType.Absolute), false, _logger);

		RequiresLoad = false;

		await Clean();
		
	}

	public async Task Save() {
		CheckLoaded();
		var registryFile = Paths.GetRegistryFilePath(FileSystemPathType.Absolute);
		await Task.Run(() => Tools.Json.WriteToFile(registryFile, _registry));
		RequiresSave = false;
	}

	public async Task Clear() {
		NotifyClearing();
		SuppressNotifications = true;
		try {
			await Task.Run(() =>_objectStore.Clear());
			await Task.Run(() => _graphStore.Clear());
			await Task.Run(() => Resources.Select(r => r.ID).ToArray().ForEach(DeleteResource));
			if (RequiresSave)
				await Save();
		} finally {
			SuppressNotifications = false;
		}
		NotifyCleared();
	}

	public async Task Clean() {
		// TODO: only cleans when ObjectID folders are used, need to 
		// - clean dangling renders

		// Fix any dangling/missing resources
		var resourceToRemove = new List<LocalNotionResource>();
		var filesToRemove = new List<string>();
		var foldersToRemove = new List<string>();

		// Scan through
		foreach (var resource in _registry.Resources) {
			if (Paths.UsesObjectIDSubFolders(resource.Type)) {
				var folder = Paths.GetResourceFolderPath(resource.Type, resource.ID, FileSystemPathType.Absolute);
				if (!Directory.Exists(folder)) {
					// registered resource but no folder exists for it
					resourceToRemove.Add(resource);
				} else if (Directory.GetFiles(folder).Length == 0) {
					// registered resource but folder is empty
					resourceToRemove.Add(resource);
					foldersToRemove.Add(folder);
				}
			}
		}

		var resourceFolders = Enumerable.Empty<string>();
		foreach(var resourceType in Enum.GetValues<LocalNotionResourceType>()) {
			if (Paths.UsesObjectIDSubFolders(resourceType)) {
				resourceFolders = resourceFolders.Union(Tools.FileSystem.GetSubDirectories(Paths.GetResourceTypeFolderPath(resourceType, FileSystemPathType.Absolute), FileSystemPathType.Absolute));
			}
		}

		foreach (var resourceFolder in resourceFolders) {
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

		foreach (var file in filesToRemove) {
			await Tools.FileSystem.DeleteFileAsync(file);
		}

		foreach (var folder in foldersToRemove) {
			await Tools.FileSystem.DeleteDirectoryAsync(folder);
		}

		// This can happen when resources are fixed
		if (RequiresSave) 
			await Save();
	}

	public bool TryGetObject(string objectId, out IFuture<IObject> @object) {
		CheckLoaded();
		var path = _objectStore.GetFilePath(objectId);
		if (!File.Exists(path)) {
			@object = null;
			return false;
		}
		@object = LazyLoad<IObject>.From(() => Tools.Json.ReadFromFile<IObject>(path));
		return true;
	}

	public void AddObject(IObject @object) {
		CheckLoaded();
		_objectStore.RegisterFile(@object.Id);
		Tools.Json.WriteToFile(_objectStore.GetFilePath(@object.Id), @object);
	}

	public virtual void DeleteObject(string objectId) {
		CheckLoaded();
		_objectStore.Delete(objectId);
	}

	public virtual bool TryGetResourceGraph(string resourceID, out IFuture<NotionObjectGraph> page) {
		CheckLoaded();
		if (!_graphStore.ContainsFile(resourceID)) {
			page = default;
			return false;
		}
		page = LazyLoad<NotionObjectGraph>.From(() => Tools.Json.ReadFromFile<NotionObjectGraph>(_graphStore.GetFilePath(resourceID)));
		return true;
	}

	public virtual void AddResourceGraph(string resourceID, NotionObjectGraph pageGraph) {
		CheckLoaded();
		Guard.ArgumentNotNull(resourceID, nameof(resourceID));
		Guard.ArgumentNotNull(pageGraph, nameof(pageGraph));
		Guard.Ensure(resourceID == pageGraph.ObjectID, $"Mismatch between argument object identifiers");
		_graphStore.RegisterFile(resourceID);
		Tools.Json.WriteToFile(_graphStore.GetFilePath(resourceID), pageGraph);
	}

	public virtual void DeleteResourceGraph(string resourceID) {
		CheckLoaded();
		_graphStore.Delete(resourceID);
	}

	public virtual bool ContainsResource(string resourceID) => _resourcesByNID.ContainsKey(resourceID);

	public virtual bool TryGetResource(string resourceID, out LocalNotionResource localNotionResource) {
		CheckLoaded();
		if (!_resourcesByNID.TryGetValue(resourceID, out var resource)) {
			localNotionResource = null;
			return false;
		}
		localNotionResource = resource;

		//if (localNotionResource is LocalNotionPage lnp) 
		//	lnp.PropertyObjects = Tools.Values.Future.LazyLoad( 
		//		() => lnp.Properties.ToDictionary(property => property.Key, property => (IPropertyItemObject) this.GetObject(property.Value))
		//	);

		return true;
	}

	public virtual void AddResource(LocalNotionResource resource) {
		CheckLoaded();
		Guard.ArgumentNotNull(resource, nameof(resource));
		Guard.Against(_resourcesByNID.ContainsKey(resource.ID), $"Resource '{resource.ID}' already registered");
		
		NotifyResourceAdding(resource.ID);

		var resourceFolder = Paths.GetResourceFolderPath(resource.Type, resource.ID, FileSystemPathType.Absolute);
		var useObjectIDFolder = resource.Type switch {
			LocalNotionResourceType.File => _registry.Paths.UseFileIDFolders,
			LocalNotionResourceType.Page => _registry.Paths.UsePageIDFolders,
			LocalNotionResourceType.Database => _registry.Paths.UseDatabaseIDFolders,
			LocalNotionResourceType.Workspace => _registry.Paths.UseWorkspaceIDFolders,
			_ => throw new NotSupportedException(resource.Type.ToString())
		};

		if (useObjectIDFolder) {
			Guard.Against(Directory.Exists(resourceFolder), $"Resource '{resource.ID}' path was already existing (dangling resource)");
			Directory.CreateDirectory(resourceFolder);
		}

		// Try to determine the object parent by calculating the path back up to the 
		resource.ParentResourceID = CalculateResourceParent(resource.ID);

		RequiresSave = true;
		_registry.Add(resource);
		_resourcesByNID.Add(resource.ID, resource);
		NotifyResourceAdded(resource);
	}

	public virtual void DeleteResource(string resourceID) {
		CheckLoaded();
		Guard.ArgumentNotNullOrWhitespace(resourceID, nameof(resourceID));
		if (!_resourcesByNID.TryGetValue(resourceID, out var resource))
			throw new InvalidOperationException($"Resource {resourceID} not found");

		NotifyResourceRemoving(resourceID);

		// Delete resource renders
		foreach(var render in resource.Renders.Values) {
			var fileToDelete = Path.GetFullPath(render.LocalPath, Paths.GetRepositoryPath(FileSystemPathType.Absolute));
			if (File.Exists(fileToDelete))
				File.Delete(fileToDelete);
		}

		// Delete resource folder if any
		if (Paths.UsesObjectIDSubFolders(resource.Type)) {
			var resourceFolderPath = Paths.GetResourceFolderPath(resource.Type, resource.ID, FileSystemPathType.Absolute);
			if (Directory.Exists(resourceFolderPath))
				Tools.FileSystem.DeleteDirectory(resourceFolderPath);
		}

		// Delete graph
		if (_graphStore.ContainsFile(resourceID))
			_graphStore.Delete(resourceID);

		RequiresSave = true;
		_registry.Remove(resource);
		_resourcesByNID.Remove(resourceID);
		NotifyResourceRemoved(resource);
	}

	public virtual bool ContainsResourceRender(string resourceID, RenderType renderType) {
		Guard.ArgumentNotNull(resourceID, nameof(resourceID));
		if (!TryGetResource(resourceID, out var resource))
			return false;

		if (!resource.Renders.TryGetValue(renderType, out var render))
			return false;

		var file = Path.GetFullPath(render.LocalPath, Paths.GetRepositoryPath(FileSystemPathType.Absolute));
		return File.Exists(file);
	}

	public virtual string ImportResourceRender(string resourceID, RenderType renderType, string renderedFile) {
		Guard.ArgumentNotNull(resourceID, nameof(resourceID));
		Guard.ArgumentNotNull(renderedFile, nameof(renderedFile));
		Guard.FileExists(renderedFile);
		CheckLoaded();
		
		var resource = this.GetResource(resourceID);
		NotifyResourceUpdating(resourceID);
		string resourceRenderPath = null;
		if (resource.Renders.ContainsKey(renderType)) {
			var render = resource.Renders[renderType];
			resourceRenderPath = Path.GetFullPath(render.LocalPath, Paths.GetRepositoryPath(FileSystemPathType.Absolute));
			if (File.Exists(resourceRenderPath))
				File.Delete(resourceRenderPath);
			resource.Renders.Remove(renderType);
		}

		// Try to re-use prior render path (in ebook path profiles, we may have conflicting filenames so this ensures same filename is used)
		if (resourceRenderPath == null) {
			resourceRenderPath = Paths.CalculateResourceFilePath(resource.Type, resource.ID, resource.Title, renderType, FileSystemPathType.Absolute);
			resourceRenderPath = Paths.ResolveConflictingFilePath(resourceRenderPath);
		}
		Tools.FileSystem.CopyFile(renderedFile, resourceRenderPath, true, true);
		resource.Renders[renderType] = new RenderEntry {
			LocalPath = Path.GetRelativePath(Paths.GetRepositoryPath(FileSystemPathType.Absolute), resourceRenderPath),
			Slug = CalculateRenderSlug(resource, renderType, resourceRenderPath)
		};
		RequiresSave = true;
		NotifyResourceUpdated(resource);
		return resourceRenderPath;

	}

	public virtual void DeleteResourceRender(string resourceID, RenderType renderType) {
		CheckLoaded();
		var resource = this.GetResource(resourceID);
		if (!resource.Renders.TryGetValue(renderType, out var render))
			return;
		NotifyResourceUpdating(resourceID);
		var fileToDelete = Path.GetFullPath(render.LocalPath, Paths.GetRepositoryPath(FileSystemPathType.Absolute));
		if (File.Exists(fileToDelete))
			File.Delete(fileToDelete);
		resource.Renders.Remove(renderType);
		RequiresSave = true;
		NotifyResourceUpdated(resource);
	}

	public string CalculateRenderSlug(LocalNotionResource resource, RenderType render, string renderedFilename) {
			
		var slugBuilder = new List<string>();
		var resourceTypeFolder = Paths.GetResourceTypeFolderPath(resource.Type, FileSystemPathType.Relative);

		// Add resource type folder part (if has one)
		if (!string.IsNullOrWhiteSpace(resourceTypeFolder))
			slugBuilder.Add(LocalNotionHelper.SanitizeSlug(Path.GetDirectoryName(resourceTypeFolder)));
			
		// Add object if folder (if has one)
		if (Paths.UsesObjectIDSubFolders(resource.Type))
			slugBuilder.Add(LocalNotionHelper.SanitizeSlug(resource.ID));

		if (render == RenderType.File) {
			// use proper filename
			renderedFilename = Tools.FileSystem.GetCaseCorrectFilePath(renderedFilename);
			slugBuilder.Add(Path.GetFileName(renderedFilename));
		} else {
			// add HTML/PDF file without extension and lowercase
			slugBuilder.Add(LocalNotionHelper.SanitizeSlug(Path.GetFileNameWithoutExtension(renderedFilename)));
		}
			
		return "/" + slugBuilder.ToDelimittedString("/");
	}

	/// <summary>
	/// Calculates the parent resource of the given object. A "resource" is something that is rendered by Local Notion.
	/// </summary>
	protected string CalculateResourceParent(string objectID) {
		var visited = new HashSet<string>();
		while(!visited.Contains(objectID) && TryGetObject(objectID, out var obj)) {
			visited.Add(objectID);
			if (obj.Value.TryGetParent(out var parent)) {
				switch(parent.Type) {
					case ParentType.DatabaseId:
					case ParentType.PageId:
					case ParentType.Workspace:
						return parent.GetId();
					case ParentType.Unknown:
					case ParentType.BlockId:
					default:
						// object 
						objectID = parent.GetId();
						break;
				}
			}
		}
		return null;
	}

	protected virtual void OnLoading() {
	}

	protected virtual void OnLoaded() {
	}

	protected virtual void OnChanging() {
	}

	protected virtual void OnChanged() {
	}

	protected virtual void OnSaving() {
	}

	protected virtual void OnSaved() {
	}

	protected virtual void OnClearing() {
	}

	protected virtual void OnCleared() {
	}

	protected virtual void OnResourceAdding(string resourceID) {
	}
	
	protected virtual void OnResourceAdded(LocalNotionResource resource) {
	}

	protected virtual void OnResourceUpdating(string resourceID) {
	}
	
	protected virtual void OnResourceUpdated(LocalNotionResource resource) {
	}

	protected virtual void OnResourceRemoving(string resourceID) {
	}
	
	protected virtual void OnResourceRemoved(LocalNotionResource resource) {
	}

	protected void NotifyLoading() {
		if (SuppressNotifications)
			return;

		OnLoading();
		Loading?.Invoke(this);
	}

	protected void NotifyLoaded() {
		if (SuppressNotifications)
			return;

		OnLoaded();
		Loaded?.Invoke(this);
	}

	protected void NotifyChanging() {
		if (SuppressNotifications)
			return;

		OnChanging();
		Changing?.Invoke(this);
	}

	protected void NotifyChanged() {
		if (SuppressNotifications)
			return;

		OnChanged();
		Changed?.Invoke(this);
	}

	protected void NotifySaving() { 
		if (SuppressNotifications)
			return;

		OnSaving();
		Saving?.Invoke(this);
	}

	protected void NotifySaved() {
		if (SuppressNotifications)
			return;
		OnSaved();
		Saved?.Invoke(this);
	}

	protected void NotifyClearing() { 
		if (SuppressNotifications)
			return;
		
		NotifyChanging();
		OnClearing();
		Clearing?.Invoke(this);
	}

	protected void NotifyCleared() {
		if (SuppressNotifications)
			return;
		NotifyChanged();
		OnCleared();
		Cleared?.Invoke(this);
	}

	protected void NotifyResourceAdding(string resourceID) {
		if (SuppressNotifications)
			return;
		NotifyChanging();
		OnResourceAdding(resourceID);
		ResourceAdding?.Invoke(this, resourceID);
	}
	
	protected void NotifyResourceAdded(LocalNotionResource resource) {
		if (SuppressNotifications)
			return;
		NotifyChanged();
		OnResourceAdded(resource);
		ResourceAdded?.Invoke(this, resource);
	}

	protected void NotifyResourceUpdating(string resourceID) {
		if (SuppressNotifications)
			return;
		NotifyChanging();
		OnResourceUpdating(resourceID);
		ResourceUpdating?.Invoke(this, resourceID);
	}
	
	protected void NotifyResourceUpdated(LocalNotionResource resource) {
		if (SuppressNotifications)
			return;
		NotifyChanged();
		OnResourceUpdated(resource);
		ResourceUpdated?.Invoke(this, resource);
	}

	protected void NotifyResourceRemoving(string resourceID) {
		if (SuppressNotifications)
			return;
		NotifyChanging();
		OnResourceRemoving(resourceID);
		ResourceRemoving?.Invoke(this, resourceID);
	}
	
	protected virtual void NotifyResourceRemoved(LocalNotionResource resource) {
		if (SuppressNotifications)
			return;
		NotifyChanged();
		OnResourceRemoved(resource);
		ResourceRemoved?.Invoke(this, resource);
	}

	private void CheckNotLoaded() {
		if (!RequiresLoad)
			throw new InvalidOperationException($"{nameof(LocalNotionRepository)} was loaded");
	}

	private void CheckLoaded() {
		if (RequiresLoad)
			throw new InvalidOperationException($"{nameof(LocalNotionRepository)} was not loaded");
	}

}