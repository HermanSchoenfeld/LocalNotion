using Hydrogen;
using Notion.Client;

namespace LocalNotion.Core;

public interface ILocalNotionRepository : IAsyncLoadable, IAsyncSaveable {

	event EventHandlerEx<object> Changing;
	event EventHandlerEx<object> Changed;
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

	string DefaultTemplate { get; }

	IReadOnlyDictionary<string, string> ThemeMaps { get; }

	public IPathResolver Paths { get; }

	string DefaultNotionApiKey { get; }

	IEnumerable<string> Objects { get; }

	IEnumerable<string> Graphs { get; }

	IEnumerable<LocalNotionResource> Resources { get; }

	Task Load();

	Task Save();

	Task Clear();

	Task Clean();

	bool TryGetObject(string objectId, out IFuture<IObject> @object);

	void AddObject(IObject @object);

	void DeleteObject(string objectId);

	bool TryGetResourceGraph(string resourceID, out IFuture<NotionObjectGraph> page);

	void AddResourceGraph(string resourceID, NotionObjectGraph pageGraph);

	void DeleteResourceGraph(string resourceID);

	bool ContainsResource(string resourceID);

	bool TryGetResource(string resourceId, out LocalNotionResource resource);

	void AddResource(LocalNotionResource resource);

	void DeleteResource(string resourceID);

	IEnumerable<LocalNotionResource> GetResourceAncestry(string resourceId);

	bool ContainsResourceRender(string resourceID, RenderType renderType);

	string ImportResourceRender(string resourceID, RenderType renderType, string renderedFile);

	void DeleteResourceRender(string resourceID, RenderType renderType);

}

public static class ILocalNotionRepositoryExtensions {

	public static bool ContainsResource(this ILocalNotionRepository repository, string objectId)
		=> repository.TryGetResource(objectId, out _); // WARN: potentially expensive

	public static LocalNotionResource GetResource(this ILocalNotionRepository repository, string objectId)
		=> repository.TryGetResource(objectId, out var resource) ? resource : throw new InvalidOperationException($"Resource '{objectId}' not found");

	public static IDictionary<string, IObject> LoadObjects(this ILocalNotionRepository repository, NotionObjectGraph graph)
		=> graph
			.VisitAll()
			.Select(x => x.ObjectID)
			.Select(repository.GetObject)
			.ToDictionary(x => x.Id, x => x);

	public static bool ContainsObject(this ILocalNotionRepository repository, string objectId)
		=> repository.TryGetObject(objectId, out _);

	public static IObject GetObject(this ILocalNotionRepository repository, string objectId)
		=> repository.TryGetObject(objectId, out var @object) ? @object.Value : throw new InvalidOperationException($"Object '{objectId}' not found");

	public static bool ContainsPageGraph(this ILocalNotionRepository repository, string pageId)
		=> repository.TryGetResourceGraph(pageId, out _);

	public static NotionObjectGraph GetPageGraph(this ILocalNotionRepository repository, string pageId)
		=> repository.TryGetResourceGraph(pageId, out var pageGraph) ? pageGraph.Value : throw new InvalidOperationException($"Page '{pageId}' not found");

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

	public static bool TryGetFile(this ILocalNotionRepository repository, string fileID, out LocalNotionFile file) {
		file = null;
		if (!repository.TryGetResource(fileID, out var resource))
			return false;

		if (resource is not LocalNotionFile lnf)
			return false;

		file = lnf;
		return true;
	}

	public static LocalNotionFile GetFile(this ILocalNotionRepository repository, string fileID)
		=> repository.TryGetFile(fileID, out var resource) ? resource : throw new InvalidOperationException($"File '{fileID}' not found");

	public static void ImportBlankResourceRender(this ILocalNotionRepository repository, string resourceID, RenderType renderType) {
		var tmpFile = Path.GetTempFileName();
		using var disposables = new Disposables();
		disposables.Add( () => File.Delete(tmpFile));
		repository.ImportResourceRender(resourceID, renderType, tmpFile);
	}

}