using Hydrogen;
using Notion.Client;

namespace LocalNotion.Core;

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

	public override int Version {
		get {
			using (EnterReadScope())
				return InternalRepository.Version;
		}
	}
	
	public override ILogger Logger {
		get {
			using (EnterReadScope())
				return InternalRepository.Logger;
		}
	}

	public override string DefaultTemplate {
		get {
			using (EnterReadScope())
				return InternalRepository.DefaultTemplate;
		}
	}

	public override IReadOnlyDictionary<string, string> ThemeMaps {
		get {
			using (EnterReadScope())
				return InternalRepository.ThemeMaps;
		}
	}
	
	public override IPathResolver Paths {
		get {
			using (EnterReadScope())
				return InternalRepository.Paths;
		}
	}

	public override string DefaultNotionApiKey {
		get {
			using (EnterReadScope())
				return InternalRepository.DefaultNotionApiKey;
		}
	}

	public override IEnumerable<string> Objects {
		get {
			using (EnterReadScope())
				return InternalRepository.Objects;
		}
	}
	
	public override IEnumerable<string> Graphs {
		get {
			using (EnterReadScope())
				return InternalRepository.Graphs;
		}
	}

	public override IEnumerable<LocalNotionResource> Resources {
		get {
			using (EnterReadScope())
				return base.Resources;
		}
	}

	public override bool RequiresLoad { 
		get {
			using (EnterReadScope())
				return InternalRepository.RequiresLoad;
		}
	}

	public override bool RequiresSave { 
		get {
			using (EnterReadScope())
				return InternalRepository.RequiresSave;
		}
	}

	public override async Task Load() {
		using (EnterWriteScope())
			await InternalRepository.Load();
	}

	public override async Task Save() {
		using (EnterWriteScope())
			await InternalRepository.Save();
	}

	public override async Task Clear() {
		using (EnterWriteScope())
			await InternalRepository.Clear();
	}

	public override async Task Clean() {
		using (EnterWriteScope())
			await InternalRepository.Clean();
	}

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

	public override bool ContainsResource(string resourceID) {
		using (EnterReadScope())
			return base.ContainsResource(resourceID);
	}

	public override bool TryGetResource(string resourceID, out LocalNotionResource localNotionResource) {
		using (EnterReadScope()) 
			return base.TryGetResource(resourceID, out localNotionResource);
	}

	public override bool ContainsResourceRender(string resourceID, RenderType renderType) {
		using (EnterReadScope())
			return base.ContainsResourceRender(resourceID, renderType);
	}

	public override void AddResource(LocalNotionResource resource) {
		using (EnterWriteScope())
			base.AddResource(resource);
	}

	public override void DeleteResource(string resourceID) {
		using (EnterWriteScope())
			base.DeleteResource(resourceID);
	}

	public override bool TryGetResourceGraph(string resourceID, out IFuture<NotionObjectGraph> page) {
		using (EnterReadScope()) 
			return TryGetResourceGraph(resourceID, out page);
	}

	public override void AddResourceGraph(string resourceID, NotionObjectGraph pageGraph) {
		using (EnterWriteScope()) 
			base.AddResourceGraph(resourceID, pageGraph);
	}

	public override void DeleteResourceGraph(string resourceID) {
		using (EnterWriteScope()) 
			base.DeleteResourceGraph(resourceID);
	}

	public override string ImportResourceRender(string resourceID, RenderType renderType, string renderedFile) {
		using (EnterWriteScope())
			return base.ImportResourceRender(resourceID, renderType, renderedFile);
	}

	public override void DeleteResourceRender(string resourceID, RenderType renderType) {
		using (EnterWriteScope()) 
			base.DeleteResourceRender(resourceID, renderType);
	}

	public override string CalculateRenderSlug(LocalNotionResource resource, RenderType render, string renderedFilename) {
		using (EnterReadScope()) 
			return base.CalculateRenderSlug(resource, render, renderedFilename);
	}

	public Scope EnterReadScope() => _syncObj.EnterReadScope();

	public Scope EnterWriteScope() => _syncObj.EnterWriteScope();

}