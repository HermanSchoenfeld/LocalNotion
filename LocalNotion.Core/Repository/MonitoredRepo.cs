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
/// A readonly <see cref="ILocalNotionRepository"/> implementation that monitors a Local Notion repo file and refreshes itself when the file is updated.  This is suitable for scenarios when the consumer needs
/// to read a repository that is being actively updated by another process. Since opening/closing a repository is expensive, using this class is better since it only re-opens it whenever a change is detected.
/// </summary>
public class MonitoredRepo : ILocalNotionRepository {
	public event EventHandlerEx<object> Loading { add => throw new NotSupportedException(); remove => throw new NotSupportedException(); }
	public event EventHandlerEx<object> Loaded { add => throw new NotSupportedException(); remove => throw new NotSupportedException(); }
	public event EventHandlerEx<object> Saving { add => throw new NotSupportedException(); remove => throw new NotSupportedException(); }
	public event EventHandlerEx<object> Saved { add => throw new NotSupportedException(); remove => throw new NotSupportedException(); }

	public MonitoredRepo(string repoFile) {
		Guard.ArgumentNotNullOrEmpty(repoFile, nameof(repoFile));
		Guard.FileExists(repoFile);
		var fileMonitor = new FileSystemWatcher(repoFile);
		fileMonitor.Changed += (object sender, FileSystemEventArgs args) => {
			if (args.ChangeType.HasFlag(WatcherChangeTypes.Changed)) {
			
			}

		};
	}

	protected IFuture<ILocalNotionRepository> InternalRepository { get; private set; }
	
	public virtual int Version => InternalRepository.Value.Version;
	public ILogger Logger => InternalRepository.Value.Logger;

	public virtual string DefaultTemplate => InternalRepository.Value.DefaultTemplate;

	public virtual LocalNotionMode Mode => InternalRepository.Value.Mode;

	public virtual IReadOnlyDictionary<string, string> RootTemplates => InternalRepository.Value.RootTemplates;

	public virtual string BaseUrl {
		get => InternalRepository.Value.BaseUrl;
		set => throw new NotSupportedException();
	}

	public virtual string ObjectsPath => InternalRepository.Value.ObjectsPath;

	public virtual string TemplatesPath => InternalRepository.Value.TemplatesPath;

	public virtual string FilesPath => InternalRepository.Value.FilesPath;

	public virtual string PagesPath => InternalRepository.Value.PagesPath;

	public virtual string DefaultNotionApiKey => InternalRepository.Value.DefaultNotionApiKey;

	public virtual IEnumerable<string> Objects => InternalRepository.Value.Objects;

	public virtual IEnumerable<LocalNotionResource> Resources => InternalRepository.Value.Resources;

	public virtual bool RequiresLoad => InternalRepository.Value.RequiresLoad;

	public virtual bool RequiresSave => InternalRepository.Value.RequiresSave;

	public virtual Task Load() => InternalRepository.Value.Load();

	public virtual Task Save() => throw new NotSupportedException();

	public virtual IUrlResolver CreateUrlResolver() => InternalRepository.Value.CreateUrlResolver();

	public virtual bool TryGetObject(string objectId, out IFuture<IObject> @object) => InternalRepository.Value.TryGetObject(objectId, out @object);

	public virtual void AddObject(IObject @object) => throw new NotSupportedException();

	public virtual void DeleteObject(string objectId) => throw new NotSupportedException();

	public virtual bool TryGetResource(string resourceId, out LocalNotionResource resource) => InternalRepository.Value.TryGetResource(resourceId, out resource);

	public virtual bool TryLookupResourceBySlug(string slug, out string resourceID) => InternalRepository.Value.TryLookupResourceBySlug(slug, out resourceID);

	public virtual IEnumerable<LocalNotionResource> GetResourceAncestry(string resourceId) => InternalRepository.Value.GetResourceAncestry(resourceId);

	public virtual void AddResource(LocalNotionResource resource) => InternalRepository.Value.AddResource(resource);

	public virtual void DeleteResource(string resourceId) => throw new NotSupportedException();

	public virtual bool TryGetPage(string pageId, out LocalNotionPage page) => InternalRepository.Value.TryGetPage(pageId, out page);

	public virtual void AddPage(LocalNotionPage page) => throw new NotSupportedException();

	public virtual void DeletePage(string pageId) => throw new NotSupportedException();

	public virtual bool TryGetPageGraph(string pageId, out IFuture<NotionObjectGraph> page) => InternalRepository.Value.TryGetPageGraph(pageId, out page);

	public virtual void AddPageGraph(string pageId, NotionObjectGraph pageGraph) => InternalRepository.Value.AddPageGraph(pageId, pageGraph);

	public virtual void DeletePageGraph(string pageId) => throw new NotSupportedException();

	public virtual string ImportPageRender(string pageId, RenderOutput renderOutput, string renderedFile) => throw new NotSupportedException();

	public virtual void DeletePageRender(string pageId, RenderOutput renderOutput) => throw new NotSupportedException();

	public string CalculatePageRenderFilename(string pageID, RenderOutput renderOutput) => InternalRepository.Value.CalculatePageRenderFilename(pageID, renderOutput);

	public string CalculatePageRenderPath(string pageID, RenderOutput renderOutput) => InternalRepository.Value.CalculatePageRenderPath(pageID, renderOutput);

	public virtual bool TryGetFile(string fileId, out LocalNotionFile notionFile) => InternalRepository.Value.TryGetFile(fileId, out notionFile);

	public virtual LocalNotionFile RegisterFile(string fileId, string filename) => throw new NotSupportedException();

	public virtual bool TryGetFileContents(string fileID, out string internalFile) => InternalRepository.Value.TryGetFileContents(fileID, out internalFile);

	public virtual void ImportFileContents(string fileId, string localFilePath) => InternalRepository.Value.ImportFileContents(fileId, localFilePath);

	public virtual void DeleteFile(string fileId) => throw new NotSupportedException();

	public virtual IEnumerable<LocalNotionPage> GetNotionCMSPages(string root, params string[] categories) => InternalRepository.Value.GetNotionCMSPages(root, categories);

	public virtual string[] GetRoots() => InternalRepository.Value.GetRoots();

	public virtual string[] GetSubCategories(string root, params string[] categories) => InternalRepository.Value.GetSubCategories(root, categories);
}

