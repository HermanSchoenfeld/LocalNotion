using Hydrogen;
using Hydrogen.Data;
using Notion.Client;

namespace LocalNotion.Core;

// Make ThreadSafe
public class LocalNotionRepository : ILocalNotionRepository {
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
	
	protected LocalNotionRegistry Registry;
	private GuidStringFileStore _objectStore;
	private GuidStringFileStore _graphStore;
	
	private ICache<string, LocalNotionResource> _resourcesByNID;
	private ICache<string, CachedSlug> _renderBySlug;
	private ICache<string, LocalNotionEditableResource> _resourceByName;
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
		Registry = null;
		_objectStore = null;
		_graphStore = null;
		_resourcesByNID = null;
		_renderBySlug = null;
		_resourceByName = null;
		Paths = null;
	}

	public int Version {
		get {
			CheckLoaded();
			return Registry.Version;
		}
	}

	public ILogger Logger => _logger;

	public string[] DefaultThemes {
		get {
			CheckLoaded();
			return Registry.DefaultThemes;
		}
	}

	public IPathResolver Paths { get; private set; }

	public string DefaultNotionApiKey {
		get {
			CheckLoaded();
			return Registry.NotionApiKey;
		}
	}

	public string CMSDatabaseID {
		get {
			CheckLoaded();
			return Registry.CMSDatabase;
		}
	}

	public IEnumerable<string> Objects {
		get {
			CheckLoaded();
			return _objectStore.FileKeys;
		}
	}

	public IEnumerable<string> Graphs {
		get {
			CheckLoaded();
			return _graphStore.FileKeys;
		}
	}

	public IEnumerable<LocalNotionResource> Resources {
		get {
			CheckLoaded();
			return Registry.Resources;
		}
	}

	public IEnumerable<CMSItem> CMSItems {
		get {
			CheckLoaded();
			return Registry.CMSItemsBySlug.Values;
		}
	}

	public GitSettings GitSettings => Registry.GitSettings;

	public NGinxSettings NGinxSettings => Registry.NGinxSettings;

	public ApacheSettings ApacheSettings => Registry.ApacheSettings;

	public bool RequiresLoad { get; private set; }

	public bool RequiresSave { get; private set; }

	protected bool SuppressNotifications { get; set; }

	public static async Task<LocalNotionRepository> CreateNew(
		string repoPath,
		string notionApiKey = null,
		string cmsDatabaseID = null,
		string[] themes = null,
		LogLevel logLevel = LogLevel.Info,
		LocalNotionPathProfile pathProfile = null,
		GitSettings gitSettings = null,
		NGinxSettings nginxSettings = null,
		ApacheSettings apacheSettings = null,
		ILogger logger = null
	) {
		Guard.ArgumentNotNull(repoPath, nameof(repoPath));
		Guard.DirectoryExists(repoPath);
		Guard.Ensure(cmsDatabaseID is null || LocalNotionHelper.IsValidObjectID(cmsDatabaseID), "Invalid CMS Database ID");
		if (cmsDatabaseID != null)
			cmsDatabaseID = LocalNotionHelper.SanitizeObjectID(cmsDatabaseID);

		// Backup theme
		if (themes == null || themes.Length == 0)
			themes = new [] { Constants.DefaultTheme};

		// The registry file is computed from the profile
		pathProfile ??= LocalNotionPathProfile.Default;

		var registryFile = Path.GetFullPath(pathProfile.RegistryPathR, repoPath);
		Guard.FileNotExists(registryFile);

		// Remove dangling files
		await Remove(repoPath, logger);

		// create registry objects
		var registry = new LocalNotionRegistry {
			NotionApiKey = notionApiKey,
			CMSDatabase = cmsDatabaseID ,
			DefaultThemes = themes,
			Paths = pathProfile,
			LogLevel = logLevel,
			Resources = Array.Empty<LocalNotionResource>(),
			GitSettings = gitSettings,
			NGinxSettings = nginxSettings,
			ApacheSettings = apacheSettings,
		};
		
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

		var repo = cmsDatabaseID is not null ? new CMSLocalNotionRepository(registryFile, logger) : new LocalNotionRepository(registryFile, logger);
		await repo.LoadAsync();
		return repo;
	}

	public static async Task<bool> Remove(string repoPath, ILogger logger = null) {
		Guard.ArgumentNotNullOrEmpty(repoPath, nameof(repoPath));
		Guard.DirectoryExists(repoPath);
		logger ??= new NoOpLogger();
		var registryFilePath = Path.Join(repoPath, LocalNotionPathProfile.Default.RegistryPathR).ToUnixPath();
		var removedSomething = false;
		if (File.Exists(registryFilePath)) {
			await RemoveViaRegistry(registryFilePath, logger);
			removedSomething = true;
		}

		var registryParentFolder = Tools.FileSystem.GetParentDirectoryPath(registryFilePath);
		if (Directory.Exists(registryParentFolder) && registryParentFolder != repoPath) {
			await Tools.FileSystem.DeleteDirectoryAsync(registryParentFolder); // this can happen if registry file is deleted but this folder was left
			removedSomething = true;
		}
		return removedSomething;
	}

	public static async Task RemoveViaRegistry(string registryPath, ILogger logger = null) {
		Guard.ArgumentNotNull(registryPath, nameof(registryPath));
		Guard.FileExists(registryPath);
		logger ??= new NoOpLogger();
		var registry = Tools.Json.ReadFromFile<LocalNotionRegistry>(registryPath);
		var pathResolver = new PathResolver(Path.GetFullPath(registry.Paths.RepositoryPathR, Path.GetDirectoryName(registryPath)), registry.Paths);
		await RemoveInternal(pathResolver, logger);
		
		// Remove any dangling renders
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

	public static bool Exists(string repositoryFolder) {
		Guard.ArgumentNotNull(repositoryFolder, nameof(repositoryFolder));
		return Directory.Exists(repositoryFolder) &&  File.Exists(PathResolver.ResolveDefaultRegistryFilePath(repositoryFolder));
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
		
		var repo = LocalNotionRegistry.IsForCms(registryFile) ? new CMSLocalNotionRepository(registryFile, logger) : new LocalNotionRepository(registryFile, logger);
		await repo.LoadAsync();
		return repo;
	}

	public async Task LoadAsync() {
		CheckNotLoaded();
		Guard.FileExists(_registryPath);

		// First check for unfinished transaction
		await SaveInternal_CompleteUnfinishedPhase(_registryPath);

		// load the registry
		Registry = await Task.Run(() => Tools.Json.ReadFromFile<LocalNotionRegistry>(_registryPath));

		// create path resolver
		Paths = new PathResolver(Path.GetFullPath(Registry.Paths.RepositoryPathR, Path.GetDirectoryName(_registryPath)), Registry.Paths);

		// create the resource lookup table
		_resourcesByNID = new BulkFetchActionCache<string, LocalNotionResource>( () => Registry.Resources.ToDictionary(x => x.ID));

		// Create the slug lookup table
		_renderBySlug = new BulkFetchActionCache<string, CachedSlug>( 
			() => Registry
			      .Resources
			      .SelectMany(resource => resource.Renders.Select(render => (render.Value.Slug, resource, render)))
			      .Concat(
				      Registry
					      .Resources
					      .Where(r => r is LocalNotionPage { CMSProperties.CustomSlug: not null })
					      .Cast<LocalNotionPage>()
					      .Where(x => x.Renders.ContainsKey(RenderType.HTML))
					      .Select(x => (x.CMSProperties.CustomSlug, (LocalNotionResource)x, x.Renders.Single(z => z.Key == RenderType.HTML)))
			      )
			      .Distinct(x => x.Item1, StringComparer.InvariantCultureIgnoreCase) 
			      .ToDictionary(x => x.Item1, x => new CachedSlug(x.Item2.ID, x.Item3.Key, x.Item1), StringComparer.InvariantCultureIgnoreCase) // possible exception if duplicate slug found in repo
		);

		//// Create name lookup table
		_resourceByName = new BulkFetchActionCache<string, LocalNotionEditableResource>(
			() => Registry
			      .Resources
			      .Where(r => r is LocalNotionEditableResource)
			      .Cast<LocalNotionEditableResource>()
			      .ToDictionary(r => r.Name)
		);

		// Prepare repository logger
		_logger.Add(
			new ThreadIdLogger(new TimestampLogger(new RollingFileLogger(Path.Combine(Paths.GetInternalResourceFolderPath(InternalResourceType.Logs, FileSystemPathType.Absolute), Constants.DefaultLogFilename)))) {
				Options = Registry.LogLevel.ToLogOptions()
			}
		);
		SystemLog.RegisterLogger(_logger);

		// Create object store
		_objectStore = new GuidStringFileStore(Paths.GetInternalResourceFolderPath(InternalResourceType.Objects, FileSystemPathType.Absolute), LocalNotionHelper.ObjectGuidToId, LocalNotionHelper.ObjectIdToGuid, fileExtension: ".json" );
		
		// Create graph store
		_graphStore = new GuidStringFileStore(Paths.GetInternalResourceFolderPath(InternalResourceType.Graphs, FileSystemPathType.Absolute), LocalNotionHelper.ObjectGuidToId, LocalNotionHelper.ObjectIdToGuid, fileExtension: ".json" );

		// Create template manager (will extract missing templates on ctor)
		HtmlThemeManager.ExtractEmbeddedThemes(Paths.GetInternalResourceFolderPath(InternalResourceType.Themes, FileSystemPathType.Absolute), true, _logger);

		RequiresLoad = false;

		// Remove clean due to race-condition caused by Website loading repo during a download but not registry update
		// await CleanAsync();
	
	}

	public async Task SaveAsync() {
		CheckLoaded();
		if (!RequiresSave)
			return;
		// 3-phased save approach ensures registry never corrupted if process abandoned mid-way
		// note: LoadAsync will complete mid-run saves
		var registryFile = Paths.GetRegistryFilePath(FileSystemPathType.Absolute);
		Guard.FileExists(registryFile);
		await SaveInternal_PersistPhase(registryFile);
		await SaveInternal_OverwritePhase(registryFile);
		await SaveInternal_CleanPhase(registryFile);
	}

	public async Task ClearAsync() {
		NotifyClearing();
		SuppressNotifications = true;
		try {
			await Task.Run(() => _objectStore.Clear());
			await Task.Run(() => _graphStore.Clear());
			await Task.Run(() => Resources.Select(r => r.ID).ToArray().ForEach(x => RemoveResource(x, false)));
			if (RequiresSave)
				await SaveAsync();
		} finally {
			SuppressNotifications = false;
		}
		NotifyCleared();
	}

	public async Task CleanAsync() {
		// TODO: only cleans when ObjectID folders are used, need to 
		// - clean dangling renders

		// Fix any dangling/missing resources
		var resourceToRemove = new List<LocalNotionResource>();
		var filesToRemove = new List<string>();
		var foldersToRemove = new List<string>();

		// Scan through
		foreach (var resource in Registry.Resources) {
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
			if (!_resourcesByNID.ContainsCachedItem(resourceID))
				foldersToRemove.Add(resourceFolder);
		}

		foreach (var resource in resourceToRemove) {
			RequiresSave = true;
			Registry.Remove(resource);
		}

		foreach (var file in filesToRemove) {
			await Tools.FileSystem.DeleteFileAsync(file);
		}

		foreach (var folder in foldersToRemove) {
			await Tools.FileSystem.DeleteDirectoryAsync(folder);
		}

		// This can happen when resources are fixed
		if (RequiresSave) 
			await SaveAsync();
		FlushCaches();
	}

	#region Objects

	public bool ContainsObject(string objectID) {
		CheckLoaded();
		return _objectStore.ContainsFile(objectID);
	}

	public bool TryGetObject(string objectID, out IObject @object) {
		CheckLoaded();
		var path = _objectStore.GetFilePath(objectID);
		if (!File.Exists(path)) {
			@object = null;
			return false;
		}
		@object = Tools.Json.ReadFromFile<IObject>(path);
		return true;
	}

	public void AddObject(IObject @object) {
		CheckLoaded();
		_objectStore.RegisterFile(@object.Id);
		Tools.Json.WriteToFile(_objectStore.GetFilePath(@object.Id), @object, FileMode.Create);  // note: RegisterFile creates a blank file
	}

	public virtual void UpdateObject(IObject @object) {
		CheckLoaded();
		Guard.Ensure(ContainsObject(@object.Id), $"Object not found: {@object.Id}");
		var filePath = _objectStore.GetFilePath(@object.Id);
		Tools.Json.WriteToFile(filePath, @object, FileMode.Create);
	}

	public virtual void RemoveObject(string objectID) {
		CheckLoaded();
		_objectStore.Delete(objectID);
	}

	#endregion

	#region Resource Graphs

	public virtual bool ContainsResourceGraph(string resourceID) {
		CheckLoaded();
		return _graphStore.ContainsFile(resourceID);
	}

	public virtual bool TryGetResourceGraph(string resourceID, out NotionObjectGraph graph) {
		CheckLoaded();
		if (!_graphStore.ContainsFile(resourceID)) {
			graph = default;
			return false;
		}
		graph = Tools.Json.ReadFromFile<NotionObjectGraph>(_graphStore.GetFilePath(resourceID));
		return true;
	}

	public virtual void AddResourceGraph(NotionObjectGraph pageGraph) {
		CheckLoaded();
		Guard.ArgumentNotNull(pageGraph, nameof(pageGraph));
		_graphStore.RegisterFile(pageGraph.ObjectID);
		Tools.Json.WriteToFile(_graphStore.GetFilePath(pageGraph.ObjectID), pageGraph);
	}

	public virtual void UpdateResourceGraph(NotionObjectGraph graph) {
		CheckLoaded();
		Guard.Ensure(ContainsResourceGraph(graph.ObjectID), $"Graph not found: {graph.ObjectID}");
		var filePath = _graphStore.GetFilePath(graph.ObjectID);
		Tools.Json.WriteToFile(filePath, graph, FileMode.Create);
	}

	public virtual void RemoveResourceGraph(string resourceID) {
		CheckLoaded();
		_graphStore.Delete(resourceID);
	}

	#endregion

	#region Resources

	public virtual bool ContainsResource(string resourceID) => _resourcesByNID.ContainsCachedItem(resourceID);

	public bool ContainsResourceByName(string name) => _resourceByName.ContainsCachedItem(name);

	public virtual bool TryGetResource(string resourceID, out LocalNotionResource localNotionResource) {
		CheckLoaded();
		if (resourceID == null) {
			localNotionResource = null;
			return false;
		}

		if (!_resourcesByNID.ContainsCachedItem(resourceID)) {
			localNotionResource = null;
			return false;
		}
		localNotionResource = _resourcesByNID[resourceID];
		return true;
	}

	public bool TryGetResourceByName(string name, out LocalNotionEditableResource resource) {
		if (!_resourceByName.ContainsCachedItem(name)) {
			resource = null;
			return false;
		}
		resource = _resourceByName[name];
		return true;
	}

	public virtual void AddResource(LocalNotionResource resource) {
		CheckLoaded();
		Guard.ArgumentNotNull(resource, nameof(resource));
		Guard.Against(_resourcesByNID.ContainsCachedItem(resource.ID), $"Resource '{resource.ID}' already registered");

		if (resource is LocalNotionEditableResource lner) {
			Guard.Ensure(!string.IsNullOrWhiteSpace(lner.Name), $"Resource name was null or whitespace");
			Guard.Ensure(!_resourceByName.ContainsCachedItem(lner.Name), $"Resource with name '{lner.Name}' already exists.");
		}

		NotifyResourceAdding(resource.ID);

		var resourceFolder = Paths.GetResourceFolderPath(resource.Type, resource.ID, FileSystemPathType.Absolute);
		var useObjectIDFolder = resource.Type switch {
			LocalNotionResourceType.File => Registry.Paths.UseFileIDFolders,
			LocalNotionResourceType.Page => Registry.Paths.UsePageIDFolders,
			LocalNotionResourceType.Database => Registry.Paths.UseDatabaseIDFolders,
			LocalNotionResourceType.Workspace => Registry.Paths.UseWorkspaceIDFolders,
			LocalNotionResourceType.CMS => false,
			_ => throw new NotSupportedException(resource.Type.ToString())
		};

		if (useObjectIDFolder) {
			//Guard.Against(Directory.Exists(resourceFolder), $"Resource '{resource.ID}' path was already existing (dangling resource)");
			// 2012-02-27 No longer enforce dangling resources, since they aren't cleaned at load time
			if (Directory.Exists(resourceFolder)) 
				Tools.FileSystem.DeleteDirectory(resourceFolder);
			Directory.CreateDirectory(resourceFolder);
		}


		// Update slug cache with CMS properties (if applicable)
		if (resource is LocalNotionPage { CMSProperties.CustomSlug: not null } lnp && lnp.TryGetRender(RenderType.HTML, out var render)) {
			_renderBySlug[lnp.CMSProperties.CustomSlug] = new(resource.ID, RenderType.HTML, lnp.CMSProperties.CustomSlug);
		}

		Registry.Add(resource);
		FlushCaches();
		RequiresSave = true;
		NotifyResourceAdded(resource);
	}

	public virtual void UpdateResource(LocalNotionResource resource) {
		// NOTE: Dangerous implementation. This could result in dangling resources/renders. Only invoke this method
		// if changing fields
		Guard.ArgumentNotNull(resource, nameof(resource));
		Guard.Ensure(TryGetResource(resource.ID, out var resourceInstance),"Resource was not found");
		Guard.Ensure(resource.Type == resourceInstance.Type, "Cannot update resource type");

		if (resource is LocalNotionEditableResource lner) {
			Guard.Ensure(!string.IsNullOrWhiteSpace(lner.Name), $"Resource name was null or whitespace");

			// Update name
			var lnerInstance = (LocalNotionEditableResource)resourceInstance;
			if (lnerInstance.Name != lner.Name) {
				Guard.Ensure(!_resourceByName.ContainsCachedItem(lner.Name), $"Resource with name '{lner.Name}' already exists.");
				lnerInstance.Name = lner.Name;
			}

			// Other LNER props
			lnerInstance.CreatedOn = lner.CreatedOn;
			lnerInstance.LastEditedOn = lner.LastEditedOn;
		}

		// Std props
		resourceInstance.Title = resource.Title;
		resourceInstance.ID = resource.ID;
		resourceInstance.Renders = resource.Renders;

		if (resource is LocalNotionPage lnp) {
			var lnpInstance = (LocalNotionPage)resourceInstance;
			lnpInstance.Cover = lnp.Cover;
			lnpInstance.Title = lnp.Title;
			lnpInstance.CMSProperties = lnp.CMSProperties;
		}

		// Don't need to do anything
		FlushCaches();
		RequiresSave = true;
	}

	public virtual void RemoveResource(string resourceID, bool removeChildren) {
		CheckLoaded();
		Guard.ArgumentNotNullOrWhitespace(resourceID, nameof(resourceID));
		if (!_resourcesByNID.ContainsCachedItem(resourceID))
			throw new InvalidOperationException($"Resource {resourceID} not found");
		var resource = _resourcesByNID[resourceID];

		NotifyResourceRemoving(resourceID);
		
		// Remove resource renders
		foreach(var render in resource.Renders)
			RemoveResourceRenderInternal(resource, render.Key, render.Value);

		// Remove resource folder if any
		if (Paths.UsesObjectIDSubFolders(resource.Type)) {
			var resourceFolderPath = Paths.GetResourceFolderPath(resource.Type, resource.ID, FileSystemPathType.Absolute);
			if (Directory.Exists(resourceFolderPath))
				Tools.FileSystem.DeleteDirectory(resourceFolderPath);
		}

		// Remove graph
		if (_graphStore.ContainsFile(resourceID))
			_graphStore.Delete(resourceID);

		// Remove any attached files if applicable
		if (resource is LocalNotionPage page) {
			if (page.Thumbnail.Type == ThumbnailType.Image && TryFindRenderBySlug(page.Thumbnail.Data, out var attachedFileRender)) 
				// TODO: reference count decrease (when added)
				RemoveResource(attachedFileRender.ResourceID, removeChildren);
			
			if (!string.IsNullOrWhiteSpace(page.Cover) && TryFindRenderBySlug(page.Cover, out attachedFileRender))
				RemoveResource(attachedFileRender.ResourceID, removeChildren);
		}

		// Remove child resource if any
		if (removeChildren) {
			foreach(var child in GetChildResources(resourceID))
				RemoveResource(child.ID, removeChildren);
		}

		// Remove from caches
		Registry.Remove(resource);
		FlushCaches();
		RequiresSave = true;

		NotifyResourceRemoved(resource);
	}

	public virtual bool TryGetParentResource(string objectID, out LocalNotionResource parent) {
		parent = null;
		if (!TryGetObject(objectID, out var obj)) 
			return false; 

		var parentID = obj.GetParent()?.GetId();
		if (string.IsNullOrWhiteSpace(parentID)) 
			return false;

		return TryGetResource(parentID, out parent) || TryGetParentResource(parentID, out parent);

	}

	public virtual IEnumerable<LocalNotionResource> GetChildResources(string resourceID) 
		=> Resources.Where(x => x.ParentResourceID == resourceID).ToArray();  // ToArray ensures enumeration completes as 

	#endregion

	#region Renders

	public virtual bool ContainsResourceRender(string resourceID, RenderType renderType) {
		Guard.ArgumentNotNull(resourceID, nameof(resourceID));
		if (!TryGetResource(resourceID, out var resource))
			return false;

		if (!resource.Renders.TryGetValue(renderType, out var render))
			return false;

		var file = Path.GetFullPath(render.LocalPath, Paths.GetRepositoryPath(FileSystemPathType.Absolute));
		return File.Exists(file);
	}

	public virtual bool TryFindRenderBySlug(string slug, out CachedSlug result) {
		 if (!_renderBySlug.ContainsCachedItem(slug)) {
			result = null;
			return false;
		}
		result = _renderBySlug[slug];
		return true;
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
			// Remove existing render
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
		var renderEntry =  new RenderEntry {
			LocalPath = Path.GetRelativePath(Paths.GetRepositoryPath(FileSystemPathType.Absolute), resourceRenderPath),
			Slug = CalculateRenderSlug(resource, renderType, resourceRenderPath)
		};
		resource.Renders[renderType] =renderEntry;
		FlushCaches();
		RequiresSave = true;
		NotifyResourceUpdated(resource);
		return resourceRenderPath;

	}

	public virtual void RemoveResourceRender(string resourceID, RenderType renderType) {
		CheckLoaded();
		var resource = this.GetResource(resourceID);
		if (!resource.Renders.TryGetValue(renderType, out var render))
			return;
		NotifyResourceUpdating(resourceID);
		RemoveResourceRenderInternal(resource, renderType, render);
		NotifyResourceUpdated(resource);
	}

	public string CalculateRenderSlug(LocalNotionResource resource, RenderType render, string renderedFilename) {
		var slugBuilder = new List<string>();

		// Add resource type folder part (if has one)
		var resourceTypeFolder = Paths.GetResourceTypeFolderPath(resource.Type, FileSystemPathType.Relative);
		if (!string.IsNullOrWhiteSpace(resourceTypeFolder))
			slugBuilder.Add(resourceTypeFolder);
			
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
			
		return slugBuilder.ToDelimittedString("/");
	}

	#endregion

	protected async Task SaveInternal_PersistPhase(string registryFile) {
		var persistFile = registryFile + ".persist";
		await Task.Run(() => Tools.Json.WriteToFile(persistFile, Registry));
		var commitFile = registryFile + ".commit";
		Tools.FileSystem.RenameFile(persistFile, commitFile);
	}

	protected async Task SaveInternal_OverwritePhase(string registryFile) {
		var commitFile = registryFile + ".commit";
		Guard.FileExists(commitFile);
		await Tools.FileSystem.CopyFileAsync(commitFile, registryFile, true);
		RequiresSave = false;
	}

	protected async Task SaveInternal_CleanPhase(string registryFile) {
		var persistFile = registryFile + ".persist";
		var commitFile = registryFile + ".commit";
		if (File.Exists(persistFile))
			await Tools.FileSystem.DeleteFileAsync(persistFile);

		if (File.Exists(commitFile))
			await Tools.FileSystem.DeleteFileAsync(commitFile);
	}

	protected async Task SaveInternal_CompleteUnfinishedPhase(string registryFile) {
		var commitFile = registryFile + ".commit";
		if (File.Exists(commitFile)) {
			await SaveInternal_OverwritePhase(registryFile);
		}
		await SaveInternal_CleanPhase(registryFile);
	}

	protected static async Task RemoveInternal(IPathResolver pathResolver, ILogger logger = null) {
		logger ??= new NoOpLogger();

		var registryFile = pathResolver.GetRegistryFilePath(FileSystemPathType.Absolute);
		
		// Cleanup parent folder
		var registryParentFolder = Tools.FileSystem.GetParentDirectoryPath(registryFile);
		if (Directory.Exists(registryParentFolder) && !Tools.FileSystem.IsDirectoryEmpty(registryParentFolder)) {
			logger.Info($"Removing {registryParentFolder}");
			await Tools.FileSystem.DeleteDirectoryAsync(registryParentFolder);
		}
		
		// Remove resource paths if exists
		var repoPath = Tools.FileSystem.GetCaseCorrectDirectoryPath(pathResolver.GetRepositoryPath(FileSystemPathType.Absolute));
		foreach(var resourceType in Enum.GetValues<LocalNotionResourceType>()) {
			var resourceTypePath = pathResolver.GetResourceTypeFolderPath(resourceType, FileSystemPathType.Absolute);
			if (Directory.Exists(resourceTypePath) && Tools.FileSystem.GetCaseCorrectDirectoryPath(resourceTypePath) != repoPath ) {
				logger.Info($"Removing {resourceTypePath}");
				await Tools.FileSystem.DeleteDirectoryAsync(resourceTypePath);
			}
		}

		// Remove internal resource paths if exists
		foreach(var internalResourceType in Enum.GetValues<InternalResourceType>()) {
			var resourceTypePath = pathResolver.GetInternalResourceFolderPath(internalResourceType, FileSystemPathType.Absolute);
			if (Directory.Exists(resourceTypePath) && !Tools.FileSystem.IsDirectoryEmpty(resourceTypePath)) {
				logger.Info($"Removing {resourceTypePath}");
				await Tools.FileSystem.DeleteDirectoryAsync(resourceTypePath);
			}
		}
	}

	protected void RemoveResourceRenderInternal(LocalNotionResource resource, RenderType renderType, RenderEntry render) {
		var fileToDelete = Path.GetFullPath(render.LocalPath, Paths.GetRepositoryPath(FileSystemPathType.Absolute));
		if (File.Exists(fileToDelete))
			File.Delete(fileToDelete);
		resource.Renders.Remove(renderType);
		FlushCaches();
		RequiresSave = true;
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
		Logger.Info($"Added resource '{resource.Title}' ({resource.ID})");
	}

	protected virtual void OnResourceUpdating(string resourceID) {
	}
	
	protected virtual void OnResourceUpdated(LocalNotionResource resource) {
		Logger.Info($"Updated resource '{resource.Title}' ({resource.ID})");
	}

	protected virtual void OnResourceRemoving(string resourceID) {
	}
	
	protected virtual void OnResourceRemoved(LocalNotionResource resource) {
		Logger.Info($"Removed resource '{resource.Title}' ({resource.ID})");
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

	protected void FlushCaches() {
		_resourcesByNID.Purge();
		_renderBySlug.Purge();
		_resourceByName.Purge();
	}

	protected void CheckNotLoaded() {
		if (!RequiresLoad)
			throw new InvalidOperationException($"{nameof(LocalNotionRepository)} was loaded");
	}

	protected void CheckLoaded() {
		if (RequiresLoad)
			throw new InvalidOperationException($"{nameof(LocalNotionRepository)} was not loaded");
	}

}
