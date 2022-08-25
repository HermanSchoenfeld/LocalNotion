using Hydrogen;
using Notion.Client;

namespace LocalNotion.Core;

public interface ILocalNotionRepository: IAsyncLoadable, IAsyncSaveable {

	int Version { get; }

	public ILogger Logger { get; }

	string DefaultTemplate { get; }

	public LocalNotionMode Mode { get; }

	IReadOnlyDictionary<string, string> RootTemplates { get; }

	public string BaseUrl { get; set; }
	
	string ObjectsPath { get; }

	string TemplatesPath { get; }

	string FilesPath { get; }
		
	string PagesPath { get; }

	string DefaultNotionApiKey { get; }

	IEnumerable<string> Objects { get; }
	
	IEnumerable<LocalNotionResource> Resources { get; }
	
	Task Load();

	Task Save();

	IUrlResolver CreateUrlResolver();

	bool TryGetObject(string objectId, out IFuture<IObject> @object);

	void AddObject(IObject @object);

	void DeleteObject(string objectId);
	
	bool TryGetResource(string resourceId, out LocalNotionResource resource);

	bool TryLookupResourceBySlug(string slug, out string resourceID);

	IEnumerable<LocalNotionResource> GetResourceAncestry(string resourceId);

	void AddResource(LocalNotionResource resource);

	void DeleteResource(string resourceId);

	bool TryGetPage(string pageId, out LocalNotionPage page);

	void AddPage(LocalNotionPage page);

	void DeletePage(string pageId);

	bool TryGetPageGraph(string pageId, out IFuture<NotionObjectGraph> page);

	void AddPageGraph(string pageId, NotionObjectGraph pageGraph);

	void DeletePageGraph(string pageId);

	string ImportPageRender(string pageId, RenderOutput renderOutput, string renderedFile);

	void DeletePageRender(string pageId, RenderOutput renderOutput);

	string CalculatePageRenderFilename(string pageID, RenderOutput renderOutput);

	string CalculatePageRenderPath(string pageID, RenderOutput renderOutput);

	bool TryGetFile(string fileId, out LocalNotionFile notionFile);

	LocalNotionFile RegisterFile(string fileId, string filename);

	bool TryGetFileContents(string fileID, out string internalFile);

	void ImportFileContents(string fileId, string localFilePath);

	void DeleteFile(string fileId);

	IEnumerable<LocalNotionPage> GetNotionCMSPages(string root, params string[] categories);

	string[] GetRoots();

	string[] GetSubCategories(string root, params string[] categories);

}

public static class ILocalNotionRepositoryExtensions {

	public static bool ContainsResource(this ILocalNotionRepository notionResourceRepository, string objectId) 
		=> notionResourceRepository.TryGetResource(objectId, out _);

	public static LocalNotionResource GetResource(this ILocalNotionRepository notionResourceRepository, string objectId)
		=> notionResourceRepository.TryGetResource(objectId, out var resource) ? resource : throw new InvalidOperationException($"Resource '{objectId}' not found");

	public static string LookupResourceBySlug(this ILocalNotionRepository notionResourceRepository, string slug)
		=> notionResourceRepository.TryLookupResourceBySlug(slug, out var resourceID) ? resourceID : throw new InvalidOperationException($"No resource addressable by the slug '{slug}' was found");
	
	public static IDictionary<string, IObject> FetchObjects(this ILocalNotionRepository notionResourceRepository, NotionObjectGraph graph) 
		=> graph
			.VisitAll()
			.Select(x => x.ObjectID)
			.Select(notionResourceRepository.GetObject)
			.ToDictionary(x => x.Id, x => x);

	public static bool ContainsObject(this ILocalNotionRepository notionResourceRepository, string objectId) 
		=> notionResourceRepository.TryGetObject(objectId, out _);

	public static IObject GetObject(this ILocalNotionRepository notionResourceRepository, string objectId)
		=> notionResourceRepository.TryGetObject(objectId, out var @object) ? @object.Value : throw new InvalidOperationException($"Object '{objectId}' not found");

	public static bool ContainsPage(this ILocalNotionRepository notionResourceRepository, string pageId) 
		=> notionResourceRepository.TryGetPage(pageId, out _);

	public static LocalNotionPage GetPage(this ILocalNotionRepository notionResourceRepository, string pageId) 
		=> notionResourceRepository.TryGetPage(pageId, out var page) ? page : throw new InvalidOperationException($"Page '{pageId}' not found");

	public static bool ContainsPageGraph(this ILocalNotionRepository notionResourceRepository, string pageId) 
		=> notionResourceRepository.TryGetPageGraph(pageId, out _);

	public static NotionObjectGraph GetPageGraph(this ILocalNotionRepository notionResourceRepository, string pageId) 
		=> notionResourceRepository.TryGetPageGraph(pageId, out var pageGraph) ? pageGraph.Value : throw new InvalidOperationException($"Page '{pageId}' not found");

	public static bool ContainsFile(this ILocalNotionRepository notionResourceRepository, string fileId) 
		=> notionResourceRepository.TryGetFile(fileId, out _);

	public static bool ContainsFileContent(this ILocalNotionRepository notionResourceRepository, string fileId) 
		=> notionResourceRepository.TryGetFileContents(fileId, out _);

	public static LocalNotionFile GetFile(this ILocalNotionRepository notionResourceRepository, string fileId) 
		=> notionResourceRepository.TryGetFile(fileId, out var file) ? file : throw new InvalidOperationException($"File '{fileId}' not found");
	
	public static LocalNotionFile AddFile(this ILocalNotionRepository notionResourceRepository, string fileId, string filename, string importLocalFilePath, string parentID) {
		var file = notionResourceRepository.RegisterFile(fileId, filename);
		notionResourceRepository.ImportFileContents(fileId, importLocalFilePath);
		return file;
	}
}