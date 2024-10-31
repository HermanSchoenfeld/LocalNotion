using System.Runtime.CompilerServices;
using Hydrogen;
using Notion.Client;
using Tools;

namespace LocalNotion.Core;

public interface ILocalNotionRepository : IDisposable, IAsyncDisposable {

	event EventHandlerEx<object> Changing;
	event EventHandlerEx<object> Changed;
	event EventHandlerEx<object> Loading;
	event EventHandlerEx<object> Loaded;
	event EventHandlerEx<object> Saving;
	event EventHandlerEx<object> Saved;
	event EventHandlerEx<object> Clearing;
	event EventHandlerEx<object> Cleared;
	event EventHandlerEx<object, string> ResourceAdding;
	event EventHandlerEx<object, LocalNotionResource> ResourceAdded;
	event EventHandlerEx<object, string> ResourceUpdating;
	event EventHandlerEx<object, LocalNotionResource> ResourceUpdated;
	event EventHandlerEx<object, string> ResourceRemoving;
	event EventHandlerEx<object, LocalNotionResource> ResourceRemoved;

	int Version { get; }

	public ILogger Logger { get; }

	string[] DefaultThemes { get; }

	public IPathResolver Paths { get; }

	string DefaultNotionApiKey { get; }

	string CMSDatabaseID { get; }

	IEnumerable<string> Objects { get; }

	IEnumerable<string> Graphs { get; }

	IEnumerable<LocalNotionResource> Resources { get; }

	IEnumerable<CMSItem> CMSItems { get; }

	GitSettings GitSettings { get; }

	NGinxSettings NGinxSettings { get; }

	ApacheSettings ApacheSettings { get; }

	bool RequiresLoad { get; }

	bool RequiresSave { get; }

	Task LoadAsync();

	Task SaveAsync();
	
	Task ClearAsync();

	Task CleanAsync();

	bool ContainsObject(string objectID);

	bool TryGetObject(string objectID, out IObject @object);

	void AddObject(IObject @object);

	void UpdateObject(IObject @object);

	void RemoveObject(string objectID);

	bool ContainsResourceGraph(string objectID);

	bool TryGetResourceGraph(string resourceID, out NotionObjectGraph graph);

	void UpdateResourceGraph(NotionObjectGraph @object);

	void AddResourceGraph(NotionObjectGraph pageGraph);

	void RemoveResourceGraph(string resourceID);

	bool ContainsResource(string resourceID);

	bool ContainsResourceByName(string name);

	bool TryGetResource(string resourceID, out LocalNotionResource resource);

	bool TryGetResourceByName(string name, out LocalNotionEditableResource resource);

	void AddResource(LocalNotionResource resource);

	void UpdateResource(LocalNotionResource resource);

	void RemoveResource(string resourceID, bool removeChildren);

	bool TryGetParentResource(string objectID, out LocalNotionResource parent);

	IEnumerable<LocalNotionResource> GetChildResources(string resourceID);

	bool ContainsResourceRender(string resourceID, RenderType renderType);

	bool TryFindRenderBySlug(string slug, out CachedSlug result);
	
	string ImportResourceRender(string resourceID, RenderType renderType, string renderedFile);

	void RemoveResourceRender(string resourceID, RenderType renderType);

	string CalculateRenderSlug(LocalNotionResource resource, RenderType render, string renderedFilename);

	public static bool IsValidObjectID(string objectID) => LocalNotionHelper.TryCovertObjectIdToGuid(objectID, out _);
}

public static class ILocalNotionRepositoryExtensions {

	public static IScope EnterUpdateScope(this ILocalNotionRepository repository) 
		=> new TaskContextScope(repository.SaveAsync, ContextScopePolicy.None, "[LocalNotion Scope]:" + repository.Paths.GetRegistryFilePath(FileSystemPathType.Absolute));

	public static LocalNotionResource GetResource(this ILocalNotionRepository repository, string objectID)
		=> repository.TryGetResource(objectID, out var resource) ? resource : throw new InvalidOperationException($"Resource '{objectID}' not found");

	public static IDictionary<string, IObject> LoadObjects(this ILocalNotionRepository repository, NotionObjectGraph graph)
		=> graph
			.VisitAll()
			.Select(x => x.ObjectID)
			.Select(repository.GetObject)
			.ToDictionary(x => x.Id, x => x);

	public static IObject GetObject(this ILocalNotionRepository repository, string objectID)
		=> repository.TryGetObject(objectID, out var @object) ? @object : throw new InvalidOperationException($"Object '{objectID}' not found");

	public static void SaveObject(this ILocalNotionRepository repository, IObject @object) {
		if (repository.ContainsObject(@object.Id)) {
			repository.UpdateObject(@object);
		}  else
			repository.AddObject(@object);
	}

	public static NotionObjectGraph GetEditableResourceGraph(this ILocalNotionRepository repository, string pageID)
		=> repository.TryGetResourceGraph(pageID, out var pageGraph) ? pageGraph : throw new InvalidOperationException($"Page '{pageID}' not found");

	public static void SavePageGraph(this ILocalNotionRepository repository, NotionObjectGraph graph) {
		if (repository.ContainsResourceGraph(graph.ObjectID))
			repository.UpdateResourceGraph(graph);
		else
			repository.AddResourceGraph(graph);
	}

	public static LocalNotionEditableResource GetResourceByName(this ILocalNotionRepository repository, string name)
		=> repository.TryGetResourceByName(name, out var resource) ? resource : throw new InvalidOperationException($"Resource '{name}' not found");

	public static bool TryGetPage(this ILocalNotionRepository repository, string pageID, out LocalNotionPage page) {
		page = null;
		if (!repository.TryGetResource(pageID, out var resource))
			return false;

		if (resource is not LocalNotionPage lnp)
			return false;

		page = lnp;
		return true;
	}

	public static LocalNotionPage GetPage(this ILocalNotionRepository repository, string pageID)
		=> repository.TryGetPage(pageID, out var resource) ? resource : throw new InvalidOperationException($"Page '{pageID}' not found");

	public static bool TryGetDatabase(this ILocalNotionRepository repository, string databaseID, out LocalNotionDatabase database) {
		database = null;
		if (!repository.TryGetResource(databaseID, out var resource))
			return false;

		if (resource is not LocalNotionDatabase lndb)
			return false;

		database = lndb;
		return true;
	}

	public static LocalNotionDatabase GetDatabase(this ILocalNotionRepository repository, string databaseID)
		=> repository.TryGetDatabase(databaseID, out var resource) ? resource : throw new InvalidOperationException($"Database '{databaseID}' not found");

	public static bool TryGetFile(this ILocalNotionRepository repository, string fileID, out LocalNotionFile file) {
		file = null;
		if (!repository.TryGetResource(fileID, out var resource))
			return false;

		if (resource is not LocalNotionFile lnf)
			return false;

		file = lnf;
		return true;
	}
	
	public static void SaveResource(this ILocalNotionRepository repository, LocalNotionResource resource) {
		if (repository.ContainsResource(resource.ID))
			repository.UpdateResource(resource);
		else
			repository.AddResource(resource);
	}

	public static LocalNotionFile GetFile(this ILocalNotionRepository repository, string fileID)
		=> repository.TryGetFile(fileID, out var resource) ? resource : throw new InvalidOperationException($"File '{fileID}' not found");

	public static void ImportBlankResourceRender(this ILocalNotionRepository repository, string resourceID, RenderType renderType) {
		var tmpFile = Path.GetTempFileName();
		using var disposables = new Disposables();
		disposables.Add( () => File.Delete(tmpFile));
		repository.ImportResourceRender(resourceID, renderType, tmpFile);
	}

	public static IEnumerable<LocalNotionResource> GetResourceAncestry(this ILocalNotionRepository repository, LocalNotionResource resource) {
		do {
			yield return resource;
		} while (repository.TryGetResource(resource.ParentResourceID, out resource));
	}

	public static RenderEntry FindRenderBySlug(this ILocalNotionRepository repository, string slug, out LocalNotionResource resource) {
		if (!repository.TryFindRenderBySlug(slug, out var render))
			throw new InvalidOperationException($"No render with slug '{slug}' was found");
		resource = repository.GetResource(slug);
		return resource.Renders[render.RenderType];
	}

}
