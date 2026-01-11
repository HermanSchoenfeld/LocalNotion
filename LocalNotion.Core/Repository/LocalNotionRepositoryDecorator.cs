using Sphere10.Framework;
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

	public virtual string[] DefaultThemes => InternalRepository.DefaultThemes;
	
	public virtual IPathResolver Paths => InternalRepository.Paths;

	public virtual string DefaultNotionApiKey => InternalRepository.DefaultNotionApiKey;

	public string CMSDatabaseID => InternalRepository.CMSDatabaseID;

	//public string CMSDataSourceID => InternalRepository.CMSDataSourceID;

	public virtual IEnumerable<string> Objects => InternalRepository.Objects;
	
	public virtual IEnumerable<string> Graphs => InternalRepository.Graphs;

	public virtual IEnumerable<LocalNotionResource> Resources => InternalRepository.Resources;

	public virtual IEnumerable<CMSItem> CMSItems => InternalRepository.CMSItems;
	public virtual GitSettings GitSettings => InternalRepository.GitSettings;

	public virtual NGinxSettings NGinxSettings => InternalRepository.NGinxSettings;

	public virtual ApacheSettings ApacheSettings => InternalRepository.ApacheSettings;

	public virtual bool RequiresLoad => InternalRepository.RequiresLoad;

	public virtual bool RequiresSave => InternalRepository.RequiresSave;

	//public virtual void IdentifyPrimaryDataSourceID(string dataSourceID)  =>	InternalRepository.IdentifyPrimaryDataSourceID(dataSourceID);

	public virtual Task LoadAsync() => InternalRepository.LoadAsync();

	public virtual Task SaveAsync() => InternalRepository.SaveAsync();

	public virtual Task ClearAsync() => InternalRepository.ClearAsync();

	public virtual Task CleanAsync() => InternalRepository.CleanAsync();

	public virtual bool ContainsObject(string objectID) => InternalRepository.ContainsObject(objectID);

	public virtual bool TryGetObject(string objectID, out IObject @object) => InternalRepository.TryGetObject(objectID, out @object);

	public virtual void AddObject(IObject @object) => InternalRepository.AddObject(@object);

	public virtual void UpdateObject(IObject @object) => InternalRepository.UpdateObject(@object);

	public virtual void RemoveObject(string objectID) => InternalRepository.RemoveObject(objectID);

	public virtual bool ContainsResource(string resourceID) => InternalRepository.ContainsResource(resourceID);

	public virtual bool ContainsResourceByName(string name) => InternalRepository.ContainsResourceByName(name);

	public virtual bool TryFindRenderBySlug(string slug, out CachedSlug result) => InternalRepository.TryFindRenderBySlug(slug, out result);

	public virtual bool TryGetResource(string resourceID, out LocalNotionResource resource) => InternalRepository.TryGetResource(resourceID, out resource);

	public virtual bool TryGetResourceByName(string name, out LocalNotionEditableResource resource) => InternalRepository.TryGetResourceByName(name, out resource);

	public virtual bool TryGetParentResource(string objectID, out LocalNotionResource parent) => InternalRepository.TryGetParentResource(objectID, out parent);

	public virtual IEnumerable<LocalNotionResource> GetChildResources(string resourceID) => InternalRepository.GetChildResources(resourceID);

	public virtual bool ContainsResourceRender(string resourceID, RenderType renderType) => InternalRepository.ContainsResourceRender(resourceID, renderType);

	public virtual void AddResource(LocalNotionResource resource) => InternalRepository.AddResource(resource);

	public virtual void UpdateResource(LocalNotionResource resource) => InternalRepository.UpdateResource(resource);

	public virtual void RemoveResource(string resourceID, bool removeChildren) => InternalRepository.RemoveResource(resourceID, removeChildren);

	public virtual bool ContainsResourceGraph(string objectID) => InternalRepository.ContainsResourceGraph(objectID);

	public virtual void UpdateResourceGraph(NotionObjectGraph graph) => InternalRepository.UpdateResourceGraph(graph);

	public virtual bool TryGetResourceGraph(string resourceID, out NotionObjectGraph graph)=> InternalRepository.TryGetResourceGraph(resourceID, out graph);

	public virtual void AddResourceGraph(NotionObjectGraph pageGraph) => InternalRepository.AddResourceGraph(pageGraph);

	public virtual void RemoveResourceGraph(string resourceID) => InternalRepository.RemoveResourceGraph(resourceID);

	public virtual string ImportResourceRender(string resourceID, RenderType renderType, string renderedFile) => InternalRepository.ImportResourceRender(resourceID, renderType, renderedFile);

	public virtual void RemoveResourceRender(string resourceID, RenderType renderType) => InternalRepository.RemoveResourceRender(resourceID, renderType);

	public virtual string CalculateRenderSlug(LocalNotionResource resource, RenderType render, string renderedFilename) => InternalRepository.CalculateRenderSlug(resource, render, renderedFilename);

	public virtual void Dispose() => InternalRepository.Dispose();

	public virtual ValueTask DisposeAsync() => InternalRepository.DisposeAsync();

}