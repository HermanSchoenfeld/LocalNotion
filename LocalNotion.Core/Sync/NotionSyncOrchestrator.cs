#pragma warning disable CS8618

using System.Net;
using System.Runtime.CompilerServices;
using System.Security.AccessControl;
using Hydrogen;
using Notion.Client;

namespace LocalNotion.Core;

// Add Scoping to ResourceRepository

public class NotionSyncOrchestrator {

	public NotionSyncOrchestrator(INotionClient notionClient, ILocalNotionRepository repository) {
		Guard.ArgumentNotNull(notionClient, nameof(notionClient));
		Guard.ArgumentNotNull(repository, nameof(repository));
		NotionClient = notionClient;
		Repository = repository;
		Logger = Repository.Logger ?? new NoOpLogger(); ;
	}

	protected ILogger Logger { get; }

	protected INotionClient NotionClient { get; }

	protected  ILocalNotionRepository Repository { get; }

	public async Task<LocalNotionResourceType?> QualifyObject(string objectId) {
		LocalNotionResourceType? objType = null;
		if (LocalNotionHelper.TryCovertObjectIdToGuid(objectId, out _)) {
			try {
				var block = await NotionClient.Blocks.RetrieveAsync(objectId);
				switch (block.Type) {
					case BlockType.ChildDatabase:
						objType = LocalNotionResourceType.Database;
						break;
					case BlockType.ChildPage:
						objType = LocalNotionResourceType.Page;
						break; ;
				}
			} catch {
			}
		}
		return objType;
	}

	/// <summary>
	/// Downloads and renders pages from a NotionCMS Database.
	/// </summary>
	/// <param name="cmsDatabaseID">Notion ID of the CMS Database</param>
	/// <param name="sourceFilter">Filter for Source property on CMS Database</param>
	/// <param name="rootFilter">Filter for Root property on CMS Database</param>
	/// <param name="updatedOnFilter">Filter out pages updated before date</param>
	/// <param name="faultTolerant">Continues processing other items when failing on any individual item</param>
	/// <returns></returns>
	public async Task<Page[]> DownloadDatabasePages(string cmsDatabaseID, string updatedOnColumnName = "Edited On", DateTime? updatedOnFilter = null, bool render = true, RenderType renderType = RenderType.HTML, RenderMode renderMode = RenderMode.ReadOnly, bool faultTolerant = true, bool forceRefresh = false) {
		try {
			// Fetch updated notion pages and transform into articles
			var cmsPages = await QueryDatabasePages(cmsDatabaseID, updatedOnColumnName, updatedOnFilter);

			// Download
			var downloadedResources = new List<LocalNotionResource>();
			foreach (var page in cmsPages) {
				try {
					var downloads = await DownloadPage(page.Id, render: false, forceRefresh: forceRefresh); // rendering deferred to below
					downloadedResources.AddRange(downloads);
				} catch (Exception error) {
					Logger.Error($"Failed to process page '{page.Id}'.");
					Logger.Exception(error);
					if (!faultTolerant)
						throw;
				}
			}

			// Render
			if (render) {
				var renderer = new ResourceRenderer(Repository, Logger);
				foreach (var page in downloadedResources.Where(x => x is LocalNotionPage).Cast<LocalNotionPage>()) {
					try {
						renderer.RenderLocalResource(page.ID, renderType, renderMode);
					} catch (Exception error) {
						Logger.Error($"Failed to render page  '{page.Title}' ({page.ID}).");
						Logger.Exception(error);
						if (!faultTolerant)
							throw;
					}
				}
			}

			return cmsPages;
		} finally {
			if (Repository.RequiresSave)
				await Repository.Save().IgnoringExceptions();
		}
	}

	public async Task<LocalNotionResource[]> DownloadPage(string pageId, bool render = true, RenderType renderType = RenderType.HTML, RenderMode renderMode = RenderMode.ReadOnly, bool faultTolerant = true, bool forceRefresh = false) {
		Guard.ArgumentNotNullOrWhitespace(pageId, nameof(pageId));
		var downloadedResources = new List<LocalNotionResource>();

		// Fetch page from Notion
		Logger.Info($"Fetching page '{pageId}'");
		var notionPage = await NotionClient.Pages.RetrieveAsync(pageId);

		if (!forceRefresh && Repository.TryGetPage(pageId, out var localPage) && localPage.LastEditedTime >= notionPage.LastEditedTime) {
			Logger.Info($"No changes detected");
			return Array.Empty<LocalNotionResource>();
		}

		// Parse into a LocalNotion page
		var localNotionPage = LocalNotionHelper.ParsePage(notionPage);

		// Parse NotionCMS properties (if applicable)
		if (NotionCMSHelper.IsCMSPage(notionPage))
			localNotionPage.CMSProperties = NotionCMSHelper.ParseCMSProperties(notionPage);

		// Fetch page object graph
		Logger.Info($"Fetching page graph for '{notionPage.GetTitle()}' ({pageId})");
		var objects = new Dictionary<string, IObject>();
		objects.Add(notionPage.Id, notionPage); // add root page object (will not fetch it and re-use it)
		var objectGraph = await NotionClient.Blocks.GetObjectGraph(pageId, objects);

		// Extract a default summary
		if (localNotionPage.CMSProperties is not null) 
			localNotionPage.CMSProperties.Summary = LocalNotionHelper.ExtractDefaultPageSummary(objectGraph, objects);

		// Save notion objects (note: this saves Page object)
		foreach (var obj in objects.Values) {
			if (Repository.ContainsObject(obj.Id)) 
				Repository.DeleteObject(obj.Id);
			Repository.AddObject(obj);
		}

		// Save local notion page resource 
		if (Repository.ContainsResource(pageId))
			Repository.DeleteResource(pageId);

		Repository.AddResource(localNotionPage);
		downloadedResources.Add(localNotionPage);

		// Save the page graph (json file describing the object graph)
		if (Repository.ContainsPageGraph(pageId))
			Repository.DeleteResourceGraph(pageId);
		Repository.AddResourceGraph(pageId, objectGraph);

		var linkResolver = UrlGeneratorFactory.Create(Repository);

		// Download cover/thumbnails
		if (localNotionPage.Cover != null && LocalNotionHelper.TryParseNotionFileUrl(localNotionPage.Cover, out var coverResourceID, out _)) {
			// Cover is a Notion file
			var file = await DownloadFile(localNotionPage.Cover, notionPage.Id, forceRefresh);
			localNotionPage.Cover = linkResolver.Resolve(localNotionPage, coverResourceID, RenderType.File, out _);
			((UploadedFile)notionPage.Cover).File.Url = localNotionPage.Cover;
			downloadedResources.Add(file);
		}

		if (localNotionPage.Thumbnail.Type == ThumbnailType.Image && LocalNotionHelper.TryParseNotionFileUrl(localNotionPage.Thumbnail.Data, out var thumbnailResourceID, out _)) {
			// Thumbnail is a Notion file
			var file = await DownloadFile(localNotionPage.Thumbnail.Data, notionPage.Id, forceRefresh);
			localNotionPage.Thumbnail.Data = linkResolver.Resolve(localNotionPage, thumbnailResourceID, RenderType.File, out _);
			((UploadedFile)notionPage.Icon).File.Url = localNotionPage.Thumbnail.Data; // update the locally stored NotionObject with local url
			downloadedResources.Add(file);
		}

		// Download any files attached to page
		var uploadedFiles =
			objectGraph
				.VisitAll()
				.Where(x => objects[x.ObjectID].HasFileAttachment())
				.Select(x => objects[x.ObjectID].GetFileAttachment())
				.Where(x => x is UploadedFile)
				.Cast<UploadedFile>()
				.ToArray();

		foreach (var uploadedFile in uploadedFiles) {
			var downloadedFile = await DownloadFile(uploadedFile.File.Url, notionPage.Id, forceRefresh);
			downloadedResources.Add(downloadedFile);
		}

		// In order to support cyclic references between parent -> child pages in the case where
		// no object-id folders are used, we generate blank stub at download time. This is because trying to predict
		// the render as IUrlResolver's do is unreliable due to potential for conflicting filenames.
		// Search for 646870E8-FEDC-45F0-9CF5-B8945C4A2F9E in source code for code support relating to this
		// issue.
		if (!Repository.Paths.UsesObjectIDSubFolders(LocalNotionResourceType.Page))
			Repository.ImportBlankResourceRender(pageId, renderType); 

		// Download any child pages
		var childPages =
			objectGraph
				.VisitAll()
				.Select(x => objects[x.ObjectID])
				.Where(x => x is ChildPageBlock)
				.Cast<ChildPageBlock>()
				.ToArray();

		foreach (var childPage in childPages) {
			var subPageDownloads = await DownloadPage(childPage.Id, render, renderType, renderMode, faultTolerant, forceRefresh);
			downloadedResources.AddRange(subPageDownloads);
		}

		// Render page
		if (render) {
			downloadedResources.Reverse(); // render child first
			var renderer = new ResourceRenderer(Repository, Logger);
			foreach (var page in downloadedResources.Where(x => x is LocalNotionPage).Cast<LocalNotionPage>()) {
				try {
					renderer.RenderLocalResource(page.ID, renderType, renderMode);
				} catch (Exception error) {
					Logger.Error($"Failed to process page  '{page.Title}' ({page.ID}). {error.ToDisplayString()}");
					Logger.Info(error.ToDiagnosticString());
					if (!faultTolerant)
						throw;
				}
			}
		}
		
		if (Repository.RequiresSave)
			await Repository.Save();

		return downloadedResources.ToArray();
	}

	public async Task<LocalNotionFile> DownloadFile(string notionFileUrl, string parentID, bool force = false) {
		Guard.ArgumentNotNull(notionFileUrl, nameof(notionFileUrl));
		if (!LocalNotionHelper.TryParseNotionFileUrl(notionFileUrl, out var resourceID, out var filename))
			throw new InvalidOperationException($"Url is not a recognized notion file url '{notionFileUrl}' ({resourceID})");

		if (Repository.TryGetFile(resourceID, out var file)) {
			if (!force && Repository.ContainsResourceRender(resourceID, RenderType.File)) {
				Logger.Info($"Skipped downloading already existing file '{resourceID}/{filename}'");
				return file;
			}
			Repository.DeleteResource(resourceID);
		}

		Logger.Info($"Downloading file '{resourceID}/{filename}'");
		var webClient = new WebClient();
		var tmpFile = Tools.FileSystem.GetTempFileName();
		try {
			await webClient.DownloadFileTaskAsync(notionFileUrl, tmpFile);
			file = LocalNotionFile.Parse(resourceID,filename);
			Repository.AddResource(file);
			Repository.ImportResourceRender(resourceID, RenderType.File, tmpFile);
			//file = Repository.AddFile(resourceID, filename, tmpFile, parentID);
		} catch (Exception error) {
			Logger.Exception(error);
		} finally {
			File.Delete(tmpFile);
		}

		if (Repository.RequiresSave)
			await Repository.Save();

		return file;
	}

	/// <summary>
	/// Delete articles and folders from file system that no longer link to a page in Notion
	/// </summary>
	/// <returns></returns>
	public async Task PruneDeletedDatabasePages() {
		// TODO: implement this
		throw new NotImplementedException();
	}

	public async Task<Page[]> QueryDatabasePages(string databaseID, string lastUpdateOnColumnName,  DateTime? updatedOnOrAfter = null) {
		Logger.Info($"Fetching updated pages for database '{databaseID}'{(updatedOnOrAfter.HasValue ? $" (updated on or after {updatedOnOrAfter:yyyy-MM-dd HH:mm:ss.fff}" : string.Empty)}");
		var notionUpdateOnFilter = 
			lastUpdateOnColumnName != null && updatedOnOrAfter != null ?
			new DateFilter(lastUpdateOnColumnName, onOrAfter: updatedOnOrAfter) :
			null;
		var results = await NotionClient.Databases.GetAllDatabaseRows(
			databaseID,
			new DatabasesQueryParameters {
				Filter = notionUpdateOnFilter
			}
		);
		Logger.Info($"Found {results.Length} updated articles");
		return results;
	}

	//public async Task<Page[]> QueryUpdatedNotionPages(string databaseID, string[] sourceFilters = null, string[] rootFilters = null, DateTime? updatedOnOrAfter = null) {
	//	Logger.Info($"Fetching updated articles{(updatedOnOrAfter.HasValue ? $" (updated on or after {updatedOnOrAfter:yyyy-MM-dd HH:mm:ss.fff}" : string.Empty)}");
	//	var filters = new List<Filter>();
	//	var notionSourceFilter = 
	//		new CompoundFilter( 
	//			or: (sourceFilters ?? Array.Empty<string>())
	//			    .Where(x => !string.IsNullOrWhiteSpace(x))
	//			    .Select(x => new RichTextFilter(Constants.LocationPropertyName, x))
	//				.Cast<Filter>()
	//				.ToList()
	//		);

	//	var notionRootFilter = 
	//		new CompoundFilter( 
	//			or: (rootFilters ?? Array.Empty<string>())
	//			    .Where(x => !string.IsNullOrWhiteSpace(x))
	//			    .Select(x => new RichTextFilter(Constants.RootCategoryPropertyName, x))
	//			    .Cast<Filter>()
	//			    .ToList()
	//		);
	//	var notionUpdateOnFilter = updatedOnOrAfter != null ? new DateFilter("Edited On", onOrAfter: updatedOnOrAfter) : null;
	//	var topLevelAndFilters = new Filter[] { notionSourceFilter, notionRootFilter, notionUpdateOnFilter }.Where(x => x != null).ToList();
	//	var results = await NotionClient.Databases.GetAllDatabaseRows(
	//		databaseID,
	//		new DatabasesQueryParameters {
	//			Filter = filters.Count > 0 ? new CompoundFilter(and: topLevelAndFilters) : null
	//		}
	//	);
	//	Logger.Info($"Found {results.Length} updated articles");
	//	return results;
	//}

}
