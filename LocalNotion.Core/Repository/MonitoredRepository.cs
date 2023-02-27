using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Hydrogen;
using LocalNotion.Core;
using Notion.Client;

namespace LocalNotion.Repository;

/// <summary>
/// A readonly <see cref="ILocalNotionRepository"/> implementation that monitors an external Local Notion repository via <see cref="FileSystemWatcher"/>. When
/// a change is detected on the registry file, this proxy repository reloads itself. A <see cref="MonitoredRepository"/> is useful for scenarios where the
/// application relies on a repository but does not update it. Thus it only wants read-only access to it. A ASP.NET Core web-application using Local Notion as
/// a View store is a perfect example.
/// </summary>
public class MonitoredRepository : DisposableResource, ILocalNotionRepository, IDisposable {
	public event EventHandlerEx<object> Loading { add => throw new NotSupportedException(); remove => throw new NotSupportedException(); }
	public event EventHandlerEx<object> Loaded { add => throw new NotSupportedException(); remove => throw new NotSupportedException(); }
	public event EventHandlerEx<object>? Changing { add => throw new NotSupportedException(); remove => throw new NotSupportedException(); }
	public event EventHandlerEx<object>? Changed; // the only event implemented
	public event EventHandlerEx<object> Saving { add => throw new NotSupportedException(); remove => throw new NotSupportedException(); }
	public event EventHandlerEx<object> Saved { add => throw new NotSupportedException(); remove => throw new NotSupportedException(); }
	public event EventHandlerEx<object> Clearing { add => throw new NotSupportedException(); remove => throw new NotSupportedException(); }
	public event EventHandlerEx<object> Cleared { add => throw new NotSupportedException(); remove => throw new NotSupportedException(); }
	public event EventHandlerEx<object, string> ResourceAdding { add => throw new NotSupportedException(); remove => throw new NotSupportedException(); }
	public event EventHandlerEx<object, LocalNotionResource> ResourceAdded { add => throw new NotSupportedException(); remove => throw new NotSupportedException(); }
	public event EventHandlerEx<object, string>? ResourceUpdating { add => throw new NotSupportedException(); remove => throw new NotSupportedException(); }
	public event EventHandlerEx<object, LocalNotionResource>? ResourceUpdated { add => throw new NotSupportedException(); remove => throw new NotSupportedException(); }
	public event EventHandlerEx<object, string>? ResourceRemoving { add => throw new NotSupportedException(); remove => throw new NotSupportedException(); }
	public event EventHandlerEx<object, LocalNotionResource>? ResourceRemoved { add => throw new NotSupportedException(); remove => throw new NotSupportedException(); }

	public MonitoredRepository(string repositoryFolder) {
		Guard.ArgumentNotNullOrEmpty(repositoryFolder, nameof(repositoryFolder));
		Guard.DirectoryExists(repositoryFolder);
		var registryFile = PathResolver.ResolveDefaultRegistryFilePath(repositoryFolder);
		Guard.FileExists(registryFile);
		InternalRepository = Tools.Values.Future.Reloadable(() =>  (ILocalNotionRepository) LocalNotionRepository.OpenRegistry(registryFile).ResultSafe());
		var fileMonitor = Tools.FileSystem.MonitorFile(
			registryFile, 
			(WatcherChangeTypes changeType, string file) => {
				if (changeType.HasFlag(WatcherChangeTypes.Changed)) {
					InternalRepository.Invalidate();
					Changed?.Invoke(this);
				}
			}
		);

		Disposables.Add(fileMonitor);
	}

	private Reloadable<ILocalNotionRepository> InternalRepository { get; }
	
	public virtual int Version => InternalRepository.Value.Version;
	
	public virtual ILogger Logger => InternalRepository.Value.Logger;

	public virtual string[] DefaultThemes => InternalRepository.Value.DefaultThemes;

	public virtual IPathResolver Paths => InternalRepository.Value.Paths;

	public virtual string DefaultNotionApiKey => InternalRepository.Value.DefaultNotionApiKey;

	public virtual IEnumerable<string> Objects => InternalRepository.Value.Objects;
	
	public virtual IEnumerable<string> Graphs => InternalRepository.Value.Graphs;

	public virtual IEnumerable<LocalNotionResource> Resources => InternalRepository.Value.Resources;

	public virtual bool RequiresLoad => InternalRepository.Value.RequiresLoad;

	public virtual bool RequiresSave => InternalRepository.Value.RequiresSave;
	
	public virtual Task LoadAsync() => InternalRepository.Value.LoadAsync();

	public virtual Task SaveAsync() => throw new NotSupportedException();

	public virtual Task ClearAsync() => throw new NotSupportedException();

	public virtual Task CleanAsync() => throw new NotSupportedException();

	public virtual bool ContainsObject(string objectID) => InternalRepository.Value.ContainsObject(objectID);

	public virtual bool TryGetObject(string objectID, out IObject @object) => InternalRepository.Value.TryGetObject(objectID, out @object);

	public virtual void AddObject(IObject @object) => throw new NotSupportedException();

	public virtual void UpdateObject(IObject @object) => throw new NotSupportedException();

	public virtual void RemoveObject(string objectID) => throw new NotSupportedException();

	public virtual bool ContainsResource(string resourceID) => InternalRepository.Value.ContainsResource(resourceID);

	public virtual bool ContainsResourceByName(string name) => InternalRepository.Value.ContainsResourceByName(name);

	public virtual bool TryGetResource(string resourceID, out LocalNotionResource resource) => InternalRepository.Value.TryGetResource(resourceID, out resource);

	public virtual bool TryGetResourceByName(string name, out LocalNotionEditableResource resource) => InternalRepository.Value.TryGetResourceByName(name, out resource);

	public virtual IEnumerable<LocalNotionResource> GetChildObjects(string resourceID) => InternalRepository.Value.GetChildObjects(resourceID);

	public virtual bool ContainsResourceRender(string resourceID, RenderType renderType) => InternalRepository.Value.ContainsResourceRender(resourceID, renderType);

	public virtual bool TryFindRenderBySlug(string slug, out CachedSlug result) => InternalRepository.Value.TryFindRenderBySlug(slug, out result);

	public virtual void AddResource(LocalNotionResource resource) => InternalRepository.Value.AddResource(resource);

	public virtual void UpdateResource(LocalNotionResource resource) => InternalRepository.Value.UpdateResource(resource);

	public virtual void RemoveResource(string resourceID, bool removeChildren) => throw new NotSupportedException();

	public virtual bool ContainsResourceGraph(string objectID) => InternalRepository.Value.ContainsResourceGraph(objectID);

	public virtual void UpdateResourceGraph(NotionObjectGraph graph) => throw new NotSupportedException();

	public virtual bool TryGetResourceGraph(string resourceID, out NotionObjectGraph page) => InternalRepository.Value.TryGetResourceGraph(resourceID, out page);

	public virtual void AddResourceGraph(NotionObjectGraph pageGraph) => InternalRepository.Value.AddResourceGraph(pageGraph);

	public virtual void RemoveResourceGraph(string resourceID) => throw new NotSupportedException();

	public virtual string ImportResourceRender(string resourceID, RenderType renderType, string renderedFile) => throw new NotSupportedException();

	public virtual void RemoveResourceRender(string resourceID, RenderType renderType) => throw new NotSupportedException();

	public virtual string CalculateRenderSlug(LocalNotionResource resource, RenderType render, string renderedFilename) => InternalRepository.Value.CalculateRenderSlug(resource, render, renderedFilename);

}

