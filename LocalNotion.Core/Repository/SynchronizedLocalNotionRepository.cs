using Hydrogen;
using Hydrogen.Data;
using Notion.Client;

namespace LocalNotion;

public class SynchronizedLocalNotionRepository : LocalNotionRepositoryDecorator, ISynchronizedObject {
	private readonly SynchronizedObject _syncObj;

	public SynchronizedLocalNotionRepository(ILocalNotionRepository internalRepository) 
		: base(internalRepository) {
		_syncObj = new SynchronizedObject();
	}

	public ISynchronizedObject<Scope, Scope> ParentSyncObject { 
		get => _syncObj.ParentSyncObject; 
		set => _syncObj.ParentSyncObject = value; 
	}

	public ReaderWriterLockSlim ThreadLock => _syncObj.ThreadLock;

	
	public override IEnumerable<string> Objects {
		get {
			using (EnterReadScope())
				return base.Objects;
		}
	}

	public override IEnumerable<LocalNotionResource> Resources {
		get {
			using (EnterReadScope())
				return base.Resources;
		}
	}

	public override string BaseUrl {
		get {
			using (EnterReadScope())
				return base.BaseUrl;
		}
		set {
			using (EnterWriteScope()) 
				base.BaseUrl = value;
		}
	}

	public override string ObjectsPath {
		get {
			using (EnterReadScope())
				return base.ObjectsPath;
		}
	}

	public override string TemplatesPath {
		get {
			using (EnterReadScope())
				return base.TemplatesPath;
		}
	}

	public override string FilesPath {
		get {
			using (EnterReadScope())
				return base.FilesPath;
		}
	}

	public override string PagesPath {
		get {
			using (EnterReadScope())
				return base.PagesPath;
		}
	}

	public Scope EnterReadScope() => _syncObj.EnterReadScope();

	public Scope EnterWriteScope() => _syncObj.EnterWriteScope();

	public override bool TryGetObject(string objectId, out IFuture<IObject> @object) {
		using (EnterReadScope()) 
			return base.TryGetObject(objectId, out @object);
	}

	public override void AddObject(IObject @object) {
		using (EnterWriteScope()) 
			base.AddObject(@object);
	}

	public override void DeleteObject(string objectId) {
		using (EnterWriteScope()) 
			base.DeleteObject(objectId);
	}

	public override bool TryGetResource(string resourceID, out LocalNotionResource localNotionResource) {
		using (EnterReadScope()) 
			return base.TryGetResource(resourceID, out localNotionResource);
	}

	public override bool TryLookupResourceBySlug(string slug, out string resourceID) {
		using (EnterReadScope()) 
			return base.TryLookupResourceBySlug(slug, out resourceID);
	}

	public override IEnumerable<LocalNotionResource> GetResourceAncestry(string resourceId) {
		using (EnterReadScope()) 
			return base.GetResourceAncestry(resourceId).ToArray(); // note: inefficient
	}

	public override void AddResource(LocalNotionResource resource) {
		using (EnterWriteScope())
			base.AddResource(resource);
	}

	public override void DeleteResource(string resourceID) {
		using (EnterWriteScope())
			base.DeleteResource(resourceID);
	}

	public override bool TryGetPage(string pageId, out LocalNotionPage page) {
		using (EnterReadScope()) 
			return base.TryGetPage(pageId, out page);
	}

	public override void AddPage(LocalNotionPage page) {
		using (EnterWriteScope())
			base.AddPage(page);
	}

	public override void DeletePage(string pageId) {
		using (EnterWriteScope()) 
			base.DeletePage(pageId);
	}

	public override bool TryGetPageGraph(string pageId, out IFuture<NotionObjectGraph> page) {
		using (EnterReadScope()) 
			return TryGetPageGraph(pageId, out page);
	}

	public override void AddPageGraph(string pageId, NotionObjectGraph pageGraph) {
		using (EnterWriteScope()) 
			base.AddPageGraph(pageId, pageGraph);
	}

	public override void DeletePageGraph(string pageId) {
		using (EnterWriteScope()) 
			base.DeletePageGraph(pageId);
	}

	public override string ImportPageRender(string pageId, RenderOutput renderOutput, string renderedFile) {
		using (EnterWriteScope())
			return base.ImportPageRender(pageId, renderOutput, renderedFile);
	}

	public override void DeletePageRender(string pageId, RenderOutput renderOutput) {
		using (EnterWriteScope()) 
			base.DeletePageRender(pageId, renderOutput);
	}
	
	public override bool TryGetFile(string fileId, out LocalNotionFile localFile) {
		using (EnterReadScope()) 
			return base.TryGetFile(fileId, out localFile);
	}

	public override LocalNotionFile RegisterFile(string fileId, string filename) {
		using (EnterWriteScope())
			return base.RegisterFile(fileId, filename);
	}

	public override bool TryGetFileContents(string fileID, out string internalFile) {
		using (EnterReadScope()) 
			return base.TryGetFileContents(fileID, out internalFile);
	}

	public override void ImportFileContents(string fileId, string localFilePath) {
		using (EnterWriteScope()) 
			base.ImportFileContents(fileId, localFilePath);
	}

	public override void DeleteFile(string fileId) {
		using (EnterWriteScope()) 
			base.DeleteFile(fileId);
	}

	public override IEnumerable<LocalNotionPage> GetNotionCMSPages(string root, params string[] categories) {
		using (EnterReadScope()) 
			return base.GetNotionCMSPages(root, categories).ToArray(); // inefficient
	}

	public override string[] GetRoots() {
		using (EnterReadScope()) 
			return base.GetRoots();
	}

	public override string[] GetSubCategories(string root, params string[] categories) {
		using (EnterReadScope()) 
			return base.GetSubCategories(root, categories);
	}
	
}