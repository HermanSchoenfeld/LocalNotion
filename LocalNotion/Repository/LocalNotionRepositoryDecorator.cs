using Hydrogen;
using Notion.Client;

namespace LocalNotion;

public abstract class LocalNotionRepositoryDecorator : ILocalNotionRepository {

	public event EventHandlerEx<object> Loading { add => InternalRepository.Loading += value; remove => InternalRepository.Loading -= value; }
	public event EventHandlerEx<object> Loaded { add => InternalRepository.Loaded += value; remove => InternalRepository.Loaded -= value; }
	public event EventHandlerEx<object> Saving { add => InternalRepository.Saving += value; remove => InternalRepository.Saving -= value; }
	public event EventHandlerEx<object> Saved { add => InternalRepository.Saved += value; remove => InternalRepository.Saved -= value; }


	protected LocalNotionRepositoryDecorator(ILocalNotionRepository internalRepository) {
		InternalRepository = internalRepository;
	}

	protected ILocalNotionRepository InternalRepository { get; }

	public virtual int Version => InternalRepository.Version;
	public ILogger Logger => InternalRepository.Logger;

	public virtual string DefaultTemplate => InternalRepository.DefaultTemplate;

	public virtual LocalNotionMode Mode => InternalRepository.Mode;
	
	public virtual IReadOnlyDictionary<string, string> RootTemplates => InternalRepository.RootTemplates;

	public virtual string BaseUrl { 
		get => InternalRepository.BaseUrl;
		set => InternalRepository.BaseUrl = value;
	} 
	
	public virtual string ObjectsPath => InternalRepository.ObjectsPath;

	public virtual string TemplatesPath => InternalRepository.TemplatesPath;

	public virtual string FilesPath => InternalRepository.FilesPath;
		
	public virtual string PagesPath => InternalRepository.PagesPath;
	
	public virtual string DefaultNotionApiKey => InternalRepository.DefaultNotionApiKey;

	public virtual IEnumerable<string> Objects => InternalRepository.Objects;

	public virtual IEnumerable<LocalNotionResource> Resources => InternalRepository.Resources;

	public virtual bool RequiresLoad => InternalRepository.RequiresLoad;

	public virtual bool RequiresSave => InternalRepository.RequiresSave;

	public virtual Task Load()=> InternalRepository.Load();

	public virtual Task Save()=> InternalRepository.Save();

	public virtual IUrlResolver CreateUrlResolver()=> InternalRepository.CreateUrlResolver();

	public virtual bool TryGetObject(string objectId, out IFuture<IObject> @object) => InternalRepository.TryGetObject(objectId, out @object);

	public virtual void AddObject(IObject @object) => InternalRepository.AddObject(@object);

	public virtual void DeleteObject(string objectId) => InternalRepository.DeleteObject(objectId);
	
	public virtual bool TryGetResource(string resourceId, out LocalNotionResource resource) => InternalRepository.TryGetResource(resourceId, out resource);

	public virtual bool TryLookupResourceBySlug(string slug, out string resourceID) => InternalRepository.TryLookupResourceBySlug(slug, out resourceID);

	public virtual IEnumerable<LocalNotionResource> GetResourceAncestry(string resourceId) => InternalRepository.GetResourceAncestry(resourceId);

	public virtual void AddResource(LocalNotionResource resource) => InternalRepository.AddResource(resource);

	public virtual void DeleteResource(string resourceId) => InternalRepository.DeleteResource(resourceId);

	public virtual bool TryGetPage(string pageId, out LocalNotionPage page) => InternalRepository.TryGetPage(pageId, out page);

	public virtual void AddPage(LocalNotionPage page)=> InternalRepository.AddPage(page);

	public virtual void DeletePage(string pageId)=> InternalRepository.DeletePage(pageId);

	public virtual bool TryGetPageGraph(string pageId, out IFuture<NotionObjectGraph> page)=> InternalRepository.TryGetPageGraph(pageId, out page);

	public virtual void AddPageGraph(string pageId, NotionObjectGraph pageGraph) => InternalRepository.AddPageGraph(pageId, pageGraph);

	public virtual void DeletePageGraph(string pageId) => InternalRepository.DeletePageGraph(pageId);

	public virtual string ImportPageRender(string pageId, PageRenderType renderType, string renderedFile) => InternalRepository.ImportPageRender(pageId, renderType, renderedFile);

	public virtual void DeletePageRender(string pageId, PageRenderType renderType) => InternalRepository.DeletePageRender(pageId, renderType);

	public string CalculatePageRenderFilename(string pageID, PageRenderType renderType) => InternalRepository.CalculatePageRenderFilename(pageID, renderType);

	public string CalculatePageRenderPath(string pageID, PageRenderType renderType) => InternalRepository.CalculatePageRenderPath(pageID, renderType);

	public virtual bool TryGetFile(string fileId, out LocalNotionFile notionFile) => InternalRepository.TryGetFile(fileId, out notionFile);

	public virtual LocalNotionFile RegisterFile(string fileId, string filename) => InternalRepository.RegisterFile(fileId, filename);

	public virtual bool TryGetFileContents(string fileID, out string internalFile) => InternalRepository.TryGetFileContents(fileID, out internalFile);

	public virtual void ImportFileContents(string fileId, string localFilePath) => InternalRepository.ImportFileContents(fileId, localFilePath);

	public virtual void DeleteFile(string fileId) => InternalRepository.DeleteFile(fileId);

	public virtual IEnumerable<LocalNotionPage> GetNotionCMSPages(string root, params string[] categories) => InternalRepository.GetNotionCMSPages(root, categories);

	public virtual string[] GetRoots() => InternalRepository.GetRoots();

	public virtual string[] GetSubCategories(string root, params string[] categories) => InternalRepository.GetSubCategories(root, categories);

}