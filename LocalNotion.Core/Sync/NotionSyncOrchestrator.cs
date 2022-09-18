#pragma warning disable CS8618

using System.Collections.Generic;
using System.Net;
using System.Runtime.CompilerServices;
using System.Security.AccessControl;
using System.Text;
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

	protected ILocalNotionRepository Repository { get; }

	/// <summary>
	/// Downloads and renders pages from a NotionCMS Database.
	/// </summary>
	/// <param name="databaseID">Notion ID of the CMS Database</param>
	/// <param name="sourceFilter">Filter for Source property on CMS Database</param>
	/// <param name="rootFilter">Filter for Root property on CMS Database</param>
	/// <param name="updatedOnFilter">Filter out pages updated before date</param>
	/// <param name="faultTolerant">Continues processing other items when failing on any individual item</param>
	/// <param name="forceRefresh"></param>
	/// <returns></returns>
	public async Task<Page[]> DownloadDatabasePagesAsync(string databaseID, bool render = true, RenderType renderType = RenderType.HTML, RenderMode renderMode = RenderMode.ReadOnly, bool faultTolerant = true, bool forceRefresh = false, CancellationToken cancellationToken = default) {
		var processedPages = new List<Page>();
		await using (Repository.EnterUpdateScope()) {
			// Fetch updated notion pages and transform into articles
			var databasePages = QueryDatabasePagesAsync(databaseID, null, cancellationToken);
			var databasePageIDs = new List<string>();
			// Download
			var downloadedResources = new List<LocalNotionResource>();
			await foreach (var page in databasePages.WithCancellation(cancellationToken)) {
				cancellationToken.ThrowIfCancellationRequested();
				try {
					databasePageIDs.Add(page.Id);
					var downloads = await DownloadPageAsync(page.Id, page.LastEditedTime, render: false, forceRefresh: forceRefresh, cancellationToken: cancellationToken); // rendering deferred to below
					downloadedResources.AddRange(downloads);
					processedPages.Add(page);
				} catch (TaskCanceledException) {
					throw;
				} catch (Exception error) {
					Logger.Error($"Failed to process page '{page.Id}'.");
					Logger.Exception(error);
					if (!faultTolerant)
						throw;
				}
			}

			// Remove deleted resources
			var localDBItems = Repository.Resources.Where(x => x.ParentResourceID == databaseID).Select(x => x.ID).ToArray();
			foreach(var oldItem in localDBItems.Except(databasePageIDs)) {
				Logger.Info($"Removing `{Repository.GetResource(oldItem).Title}` ({oldItem})");
				Repository.RemoveResource(oldItem);
			}

			// Render
			if (render) {
				var renderer = new ResourceRenderer(Repository, Logger);
				foreach (var page in downloadedResources.Where(x => x is LocalNotionPage).Cast<LocalNotionPage>()) {
					cancellationToken.ThrowIfCancellationRequested();
					try {
						renderer.RenderLocalResource(page.ID, renderType, renderMode);
					} catch (TaskCanceledException) {
						throw;
					} catch (Exception error) {
						Logger.Error($"Failed to render page  '{page.Title}' ({page.ID}).");
						Logger.Exception(error);
						if (!faultTolerant)
							throw;
					}
				}
			}

			return processedPages.ToArray();
		}
	}

	public async Task<LocalNotionResource[]> DownloadPageAsync(string pageID, DateTime? knownNotionLastEditTime = null, bool render = true, RenderType renderType = RenderType.HTML, RenderMode renderMode = RenderMode.ReadOnly, bool faultTolerant = true, bool forceRefresh = false, CancellationToken cancellationToken = default) {
		Guard.ArgumentNotNull(pageID, nameof(pageID));
		var downloadedResources = new List<LocalNotionResource>();

		await using (Repository.EnterUpdateScope()) {

			// Track the page objects. 
			Page notionPage = null;
			LocalNotionPage localPage = null;
			IDictionary<string, IObject> pageObjects = default;
			NotionObjectGraph pageGraph = default;
			List<(string, DateTime?)> childPages = default;

			// Nested method used to fetch notion page (if required, the logic herein
			// tries to avoid calling this)
			async Task FetchNotionPage() {
				Logger.Info($"Fetching page '{pageID}'");
				notionPage = await NotionClient.Pages.RetrieveAsync(pageID).WithCancellationToken(cancellationToken);
				knownNotionLastEditTime = notionPage.LastEditedTime;
			}

			#region Determine if Page should be fetched

			// Fetch page, we need to know last edit tie on notion
			if (knownNotionLastEditTime == null)
				await FetchNotionPage();

			var containsPage = Repository.TryGetPage(pageID, out localPage);
			var pageDifferent= containsPage && localPage.LastEditedTime != knownNotionLastEditTime;
			var shouldDownload = !containsPage || pageDifferent || forceRefresh;

			#endregion

			// Hydrate into a LocalNotion page
			if (shouldDownload) {
				
				#region Download page from Notion

				// Fetch page if not already
				if (notionPage == null)
					await FetchNotionPage();

				// Remove local page (if applicable)				
				if (containsPage)
					Repository.RemoveResource(pageID);

				// Hydrate the fetched page from notion
				localPage = LocalNotionHelper.ParsePage(notionPage);

				// Fetch page graph from notion
				Logger.Info($"Fetching page graph for '{notionPage.GetTitle()}' ({notionPage.Id})");
				pageObjects = new Dictionary<string, IObject>();
				pageObjects.Add(notionPage.Id, notionPage);    // optimization: pre-add Page root object to avoid expensive fetch
				pageGraph = await NotionClient.Blocks.GetObjectGraphAsync(notionPage.Id, pageObjects, cancellationToken);

				// Determine child pages
				childPages =
					pageObjects
					.Values
					.Where(x => x is ChildPageBlock)
					.Cast<ChildPageBlock>()
					.Select(x => (x.Id, new DateTime?(x.LastEditedTime)))
					.ToList();

				// Hydrate a Notion CMS summaries (and future plugins)
				if (NotionCMSHelper.IsCMSPage(notionPage))
					localPage.CMSProperties = NotionCMSHelper.ParseCMSProperties(notionPage);

				if (localPage.CMSProperties is not null)
					localPage.CMSProperties.Summary = LocalNotionHelper.ExtractDefaultPageSummary(pageGraph, pageObjects);

				#endregion

				#region Download attached files

				var linkResolver = UrlGeneratorFactory.Create(Repository);
				// Download cover
				if (localPage.Cover != null && ShouldDownloadFile(localPage.Cover)) {
					// Cover is a Notion file
					var file = await DownloadFileAsync(localPage.Cover, notionPage.Id, forceRefresh, cancellationToken);
					if (file != null) {
						localPage.Cover = linkResolver.Resolve(localPage, file.ID, RenderType.File, out _);
						// update notion object with url (this is a component object and saved with page)
						notionPage.Cover.SetUrl($"resource://{file.ID}");

						// track new file
						downloadedResources.Add(file);
					}
				}

				// Download thumbnail
				if (localPage.Thumbnail.Type == ThumbnailType.Image && ShouldDownloadFile(localPage.Thumbnail.Data)) {
					// Thumbnail is a Notion file
					var file = await DownloadFileAsync(localPage.Thumbnail.Data, notionPage.Id, forceRefresh, cancellationToken);
					if (file != null) {
						localPage.Thumbnail.Data = linkResolver.Resolve(localPage, file.ID, RenderType.File, out _);
						// update notion object with url (this is a component object and saved with page)
						((FileObject)notionPage.Icon).SetUrl(LocalNotionRenderLink.GenerateUrl(file.ID, RenderType.File)); // update the locally stored NotionObject with local url

						// track new file
						downloadedResources.Add(file);
					}
				}

				// Download uploaded files (and external files if applicable)
				var uploadedFiles =
					pageGraph
						.VisitAll()
						.Where(x => pageObjects[x.ObjectID].HasFileAttachment())
						.Select(x => pageObjects[x.ObjectID].GetFileAttachment())
						.Where(x => x is FileObject)
						.ToArray();

				foreach (var file in uploadedFiles) {
					var url = file.GetUrl();
					if (ShouldDownloadFile(url)) {
						var downloadedFile = await DownloadFileAsync(url, notionPage.Id, forceRefresh, cancellationToken);
						if (downloadedFile != null) {
							file.SetUrl(LocalNotionRenderLink.GenerateUrl(downloadedFile.ID, RenderType.File));
							downloadedResources.Add(downloadedFile);
						}
					}
				}

				#endregion

				#region Save Local Notion objects and resources

				// Save notion objects (note: this saves Page object)
				foreach (var obj in pageObjects.Values) {
					// since ChildPageBlock and Page share same ID, we avoid overwriting child page
					if (obj is ChildPageBlock && Repository.ContainsObject(obj.Id))
						continue;
					Repository.SaveObject(obj);
				}

				// Save the page graph (json file describing the object graph)
				Repository.SavePageGraph(pageGraph);

				// Save local notion page resource 
				Repository.AddResource(localPage);
				downloadedResources.Add(localPage);

				// In order to support cyclic references between parent -> child pages in the case where
				// no object-id folders are used, we generate blank stub at download time. This is because trying to predict
				// the render as IUrlResolver's do is unreliable due to potential for conflicting filenames.
				// Search for 646870E8-FEDC-45F0-9CF5-B8945C4A2F9E in source code for code support relating to this
				// issue.
				if (!Repository.Paths.UsesObjectIDSubFolders(LocalNotionResourceType.Page))
					Repository.ImportBlankResourceRender(notionPage.Id, renderType);

				#endregion

			} else {
				#region CASE: Page already downloaded and is unchanged

				// Fetch local versions of page objcets and graph
				// since we still need to downlaod  child pages (and if
				// any are changed, this page needs re-rendering)
				Guard.Ensure(localPage != null);
				Logger.Info($"No changes detected for `{localPage.Title}`");
				pageGraph = Repository.GetPageGraph(pageID);
				pageObjects = Repository.LoadObjects(pageGraph);
				
				// Note: in this case LocalNotion doesn't store ChildPage objects, it stores them as Page 
				// objects. Also, we need to ignore the current page to avoid infinite recursive loops.
				
				childPages =
					pageObjects
					.Values
					.Where(x => x is ChildPageBlock childPage && childPage.Id != pageID)
					.Cast<ChildPageBlock>()
					.Select(x => (x.Id, null as DateTime?))
					.Union( 
						pageObjects
							.Values
							.Where(x => x is Page page && page.Id != pageID)
							.Cast<Page>()
							.Select(x => (x.Id, null as DateTime?))
					)
					.ToList();

				// Render the unchanged page if no render exists
				if (!localPage.TryGetRender(renderType, out _))
					downloadedResources.Add(localPage);

				#endregion
			}

			#region Download any child pages

			foreach (var childPage in childPages) {
				// We call download to sub-pages but with rendering off, only top-level will ever render itself and all children
				var subPageDownloads = await DownloadPageAsync(childPage.Item1, childPage.Item2, false, renderType, renderMode, faultTolerant, forceRefresh, cancellationToken);
				downloadedResources.AddRange(subPageDownloads);
			}

			// If page was not changed but a child page was changed, then we add it to renderable
			// set because links and other things may be changed
			if (!downloadedResources.Contains(localPage) &&
				downloadedResources.Any(x => x is LocalNotionPage && childPages.Any(y => x.ID == y.Item1)))
				downloadedResources.Add(localPage);

			#endregion
			
			#region Render page and child-pages

			if (render) {
				var renderer = new ResourceRenderer(Repository, Logger);
				foreach (var page in downloadedResources.Where(x => x is LocalNotionPage).Distinct().Cast<LocalNotionPage>()) {
					cancellationToken.ThrowIfCancellationRequested();
					try {
						renderer.RenderLocalResource(page.ID, renderType, renderMode);
					} catch (TaskCanceledException) {
						throw;
					} catch (Exception error) {
						Logger.Error($"Failed to render page '{page.Title}' ({page.ID}).");
						Logger.Exception(error);
						if (!faultTolerant)
							throw;
					}
				}
			}

			#endregion

		}
		return downloadedResources.ToArray();
	}

	public async Task<LocalNotionFile> DownloadFileAsync(string url, string parentResourceID, bool force = false, CancellationToken cancellationToken = default) {
		Guard.ArgumentNotNull(url, nameof(url));
		await using (Repository.EnterUpdateScope()) {
			if (!LocalNotionHelper.TryParseNotionFileUrl(url, out var resourceID, out var filename)) {
				if (Repository.Paths.ForceDownloadExternalContent) {
					resourceID = CalculateExternalResourceFileID(url);
					filename = Tools.Web.ParseFilenameFromUrl(url);
					if (!Tools.FileSystem.IsWellFormedFileName(filename))
						filename = "LN_" + Guid.Parse(resourceID).ToStrictAlphaString();
				} else throw new InvalidOperationException($"Url is not a recognized notion file url and downloading external content is not enabled. Url: '{url}'");
			}

			if (Repository.TryGetFile(resourceID, out var file)) {
				if (!force && Repository.ContainsResourceRender(resourceID, RenderType.File)) {
					Logger.Info($"Skipped downloading already existing file '{resourceID}/{filename}'");
					return file;
				}
				Repository.RemoveResource(resourceID);
			}

			Logger.Info($"Downloading: {filename} (resource: {resourceID})");
			var tmpFile = Tools.FileSystem.GetTempFileName();
			try {
				var mimeType = await Tools.Web.DownloadFileAsync(url, tmpFile, verifySSLCert: false, cancellationToken);
				file = LocalNotionFile.Parse(resourceID, filename, parentResourceID, mimeType);
				Repository.AddResource(file);
				Repository.ImportResourceRender(resourceID, RenderType.File, tmpFile);
			} catch (TaskCanceledException) {
				throw;
			} catch (Exception error) {
				Logger.Exception(error);
			} finally {
				File.Delete(tmpFile);
			}

			return file;
		}
	}

	/// <summary>
	/// Remove articles and folders from file system that no longer link to a page in Notion
	/// </summary>
	/// <returns></returns>
	public async Task PruneDeletedDatabasePagesAsync(CancellationToken cancellationToken = default) {
		// TODO: implement this
		throw new NotImplementedException();
	}

	public IAsyncEnumerable<Page> QueryDatabasePagesAsync(string databaseID, DateTime? updatedOnOrAfter = null, CancellationToken cancellationToken = default) {
		Logger.Info($"Fetching updated pages for database '{databaseID}'{(updatedOnOrAfter.HasValue ? $" (updated on or after {updatedOnOrAfter:yyyy-MM-dd HH:mm:ss.fff}" : string.Empty)}");
		return NotionClient.Databases.EnumerateAsync(
			databaseID, 
			new DatabasesQueryParameters { Filter = updatedOnOrAfter.HasValue ? new TimestampLastEditedTimeFilter(onOrAfter: updatedOnOrAfter) : null, },
			cancellationToken
		);
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

	private bool ShouldDownloadFile(string url)
		// We only download user files uploaded to notion (or everything is force download is on)
		=> (Repository.Paths.ForceDownloadExternalContent && !Tools.Url.IsYouTubeUrl(url)) ||
		   LocalNotionHelper.TryParseNotionFileUrl(url, out _, out _);

	/// <summary>
	/// This calculates a notion-like FileID for a file which is not part of notion but downloaded to Local Notion.
	/// </summary>
	/// <remarks>The algorithm used is: Guid.Parse( Blake2b_128( ToGuidBytes(ToAsciiBytes(url) ) ) ) </remarks>
	/// <param name="url">The URL of the page</param>
	/// <returns>A globally unique ID for the property.</returns>
	private string CalculateExternalResourceFileID(string url) {
		Guard.ArgumentNotNull(url, nameof(url));
		if (LocalNotionHelper.TryParseNotionFileUrl(url, out var resourceID, out _))
			return resourceID;
		var urlBytes = Encoding.ASCII.GetBytes(url);
		var result = LocalNotionHelper.ObjectGuidToId(new Guid(Hashers.Hash(CHF.Blake2b_128, urlBytes)));
		return result;
	}
}
