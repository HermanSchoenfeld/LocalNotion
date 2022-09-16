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
/// A readonly <see cref="ILocalNotionRepository"/> implementation that monitors a Local Notion repository via <see cref="FileSystemWatcher"/> and reloads itself when it changes. This is suitable for scenarios where the application
/// relies on a repository as a read-only data-store but doesn't want to open/close the repo every time it needs a resource (since this is expensive). Instead it loads it once, and on next resource request, it will reload itself if changed.
public class MonitoredRepo : ILocalNotionRepository {
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

	public MonitoredRepo(string repoFile) {
		Guard.ArgumentNotNullOrEmpty(repoFile, nameof(repoFile));
		Guard.FileExists(repoFile);
		InternalRepository = Tools.Values.Future.Reloadable(() =>  (ILocalNotionRepository) LocalNotionRepository.Open(repoFile).ResultSafe());
		var fileMonitor = new FileSystemWatcher(repoFile);
		fileMonitor.Changed += (object sender, FileSystemEventArgs args) => {
			if (args.ChangeType.HasFlag(WatcherChangeTypes.Changed)) {
				InternalRepository.Invalidate();
				Changed?.Invoke(this);
			}
		};
	}

	private Reloadable<ILocalNotionRepository> InternalRepository { get; }
	
	public virtual int Version => InternalRepository.Value.Version;
	
	public virtual ILogger Logger => InternalRepository.Value.Logger;

	public virtual string DefaultTemplate => InternalRepository.Value.DefaultTemplate;


	public virtual IReadOnlyDictionary<string, string> ThemeMaps => InternalRepository.Value.ThemeMaps;
	
	public virtual IPathResolver Paths => InternalRepository.Value.Paths;

	public virtual string DefaultNotionApiKey => InternalRepository.Value.DefaultNotionApiKey;

	public virtual IEnumerable<string> Objects => InternalRepository.Value.Objects;
	
	public virtual IEnumerable<string> Graphs => InternalRepository.Value.Graphs;

	public virtual IEnumerable<LocalNotionResource> Resources => InternalRepository.Value.Resources;

	public virtual bool RequiresLoad => InternalRepository.Value.RequiresLoad;

	public virtual bool RequiresSave => InternalRepository.Value.RequiresSave;

	public virtual Task Load() => InternalRepository.Value.Load();

	public virtual Task Save() => throw new NotSupportedException();

	public virtual Task Clear() => throw new NotSupportedException();

	public virtual Task Clean() => throw new NotSupportedException();

	public bool ContainsObject(string objectID) => InternalRepository.Value.ContainsObject(objectID);

	public virtual bool TryGetObject(string objectID, out IObject @object) => InternalRepository.Value.TryGetObject(objectID, out @object);

	public virtual void AddObject(IObject @object) => throw new NotSupportedException();

	public virtual void RemoveObject(string objectID) => throw new NotSupportedException();

	public virtual bool ContainsResource(string resourceID) => InternalRepository.Value.ContainsResource(resourceID);

	public virtual bool TryGetResource(string resourceID, out LocalNotionResource resource) => InternalRepository.Value.TryGetResource(resourceID, out resource);

	public bool ContainsResourceRender(string resourceID, RenderType renderType) => InternalRepository.Value.ContainsResourceRender(resourceID, renderType);

	public bool TryFindRenderBySlug(string slug, out string resourceID, out RenderType renderType) 
		=> InternalRepository.Value.TryFindRenderBySlug(slug, out resourceID, out renderType);

	public virtual void AddResource(LocalNotionResource resource) => InternalRepository.Value.AddResource(resource);

	public virtual void RemoveResource(string resourceID) => throw new NotSupportedException();

	public virtual bool ContainsResourceGraph(string objectID) => InternalRepository.Value.ContainsResourceGraph(objectID);

	public virtual bool TryGetResourceGraph(string resourceID, out NotionObjectGraph page) => InternalRepository.Value.TryGetResourceGraph(resourceID, out page);

	public virtual void AddResourceGraph(string resourceID, NotionObjectGraph pageGraph) => InternalRepository.Value.AddResourceGraph(resourceID, pageGraph);

	public virtual void RemoveResourceGraph(string resourceID) => throw new NotSupportedException();

	public virtual string ImportResourceRender(string resourceID, RenderType renderType, string renderedFile) => throw new NotSupportedException();

	public virtual void RemoveResourceRender(string resourceID, RenderType renderType) => throw new NotSupportedException();

	public virtual string CalculateRenderSlug(LocalNotionResource resource, RenderType render, string renderedFilename) => InternalRepository.Value.CalculateRenderSlug(resource, render, renderedFilename);

}

