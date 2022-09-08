using Hydrogen;
using Notion.Client;

namespace LocalNotion.Core;

public abstract class LocalNotionRepositoryDecorator : ILocalNotionRepository {

	public event EventHandlerEx<object> Loading { add => InternalRepository.Loading += value; remove => InternalRepository.Loading -= value; }
	public event EventHandlerEx<object> Loaded { add => InternalRepository.Loaded += value; remove => InternalRepository.Loaded -= value; }
	public event EventHandlerEx<object> Changing { add => InternalRepository.Changing += value; remove => InternalRepository.Changing -= value; }
	public event EventHandlerEx<object> Changed { add => InternalRepository.Changed += value; remove => InternalRepository.Changed -= value; }
	public event EventHandlerEx<object> Saving { add => InternalRepository.Saving += value; remove => InternalRepository.Saving -= value; }
	public event EventHandlerEx<object> Saved { add => InternalRepository.Saved += value; remove => InternalRepository.Saved -= value; }
	public event EventHandlerEx<object> Clearing { add => InternalRepository.Clearing += value; remove => InternalRepository.Clearing -= value; }
	public event EventHandlerEx<object> Cleared { add => InternalRepository.Cleared += value; remove => InternalRepository.Cleared -= value; }
	public event EventHandlerEx<object, string> ResourceAdding { add => InternalRepository.ResourceAdding += value; remove => InternalRepository.ResourceAdding -= value; }
	public event EventHandlerEx<object, LocalNotionResource> ResourceAdded { add => InternalRepository.ResourceAdded += value; remove => InternalRepository.ResourceAdded -= value; }
	public event EventHandlerEx<object, string> ResourceUpdating { add => InternalRepository.ResourceUpdating += value; remove => InternalRepository.ResourceUpdating -= value; }
	public event EventHandlerEx<object, LocalNotionResource> ResourceUpdated { add => InternalRepository.ResourceUpdated += value; remove => InternalRepository.ResourceUpdated -= value; }
	public event EventHandlerEx<object, string> ResourceRemoving { add => InternalRepository.ResourceRemoving += value; remove => InternalRepository.ResourceRemoving -= value; }
	public event EventHandlerEx<object, LocalNotionResource> ResourceRemoved { add => InternalRepository.ResourceRemoved += value;	remove => InternalRepository.ResourceRemoved -= value; }

	protected LocalNotionRepositoryDecorator(ILocalNotionRepository internalRepository) {
		InternalRepository = internalRepository;
	}

	protected ILocalNotionRepository InternalRepository { get; }

	public virtual int Version => InternalRepository.Version;
	
	public virtual ILogger Logger => InternalRepository.Logger;

	public virtual string DefaultTemplate => InternalRepository.DefaultTemplate;
	
	public virtual IReadOnlyDictionary<string, string> ThemeMaps => InternalRepository.ThemeMaps;
	
	public virtual IPathResolver Paths => InternalRepository.Paths;

	public virtual string DefaultNotionApiKey => InternalRepository.DefaultNotionApiKey;

	public virtual IEnumerable<string> Objects => InternalRepository.Objects;
	
	public virtual IEnumerable<string> Graphs => InternalRepository.Graphs;

	public virtual IEnumerable<LocalNotionResource> Resources => InternalRepository.Resources;

	public virtual bool RequiresLoad => InternalRepository.RequiresLoad;

	public virtual bool RequiresSave => InternalRepository.RequiresSave;

	public virtual Task Load() => InternalRepository.Load();

	public virtual Task Save() => InternalRepository.Save();

	public virtual Task Clear() => InternalRepository.Clear();

	public virtual Task Clean() => InternalRepository.Clean();

	public virtual bool TryGetObject(string objectId, out IFuture<IObject> @object) => InternalRepository.TryGetObject(objectId, out @object);

	public virtual void AddObject(IObject @object) => InternalRepository.AddObject(@object);

	public virtual void DeleteObject(string objectId) => InternalRepository.DeleteObject(objectId);

	public virtual bool ContainsResource(string resourceID) => InternalRepository.ContainsResource(resourceID);

	public virtual bool TryGetResource(string resourceId, out LocalNotionResource resource) => InternalRepository.TryGetResource(resourceId, out resource);

	public virtual bool ContainsResourceRender(string resourceID, RenderType renderType) => InternalRepository.ContainsResourceRender(resourceID, renderType);

	public virtual void AddResource(LocalNotionResource resource) => InternalRepository.AddResource(resource);

	public virtual void DeleteResource(string resourceID) => InternalRepository.DeleteResource(resourceID);

	public virtual bool TryGetResourceGraph(string resourceID, out IFuture<NotionObjectGraph> page)=> InternalRepository.TryGetResourceGraph(resourceID, out page);

	public virtual void AddResourceGraph(string resourceID, NotionObjectGraph pageGraph) => InternalRepository.AddResourceGraph(resourceID, pageGraph);

	public virtual void DeleteResourceGraph(string resourceID) => InternalRepository.DeleteResourceGraph(resourceID);

	public virtual string ImportResourceRender(string resourceID, RenderType renderType, string renderedFile) => InternalRepository.ImportResourceRender(resourceID, renderType, renderedFile);

	public virtual void DeleteResourceRender(string resourceID, RenderType renderType) => InternalRepository.DeleteResourceRender(resourceID, renderType);

	public virtual string CalculateRenderSlug(LocalNotionResource resource, RenderType render, string renderedFilename) => InternalRepository.CalculateRenderSlug(resource, render, renderedFilename);


}