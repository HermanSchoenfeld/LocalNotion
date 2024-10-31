#pragma warning disable CS8618

using System.Text;
using System.Xml.Schema;
using Hydrogen;
using Hydrogen.Application;
using LocalNotion.Core.DataObjects;
using Notion.Client;

namespace LocalNotion.Core;

public class NotionSyncOrchestrator {

	public NotionSyncOrchestrator(INotionClient notionClient, ILocalNotionRepository repository) {
		Guard.ArgumentNotNull(notionClient, nameof(notionClient));
		Guard.ArgumentNotNull(repository, nameof(repository));
		NotionClient = notionClient;
		Repository = repository;
		Logger = Repository.Logger ?? new NoOpLogger();
	}

	protected ILogger Logger { get; }

	protected INotionClient NotionClient { get; }

	protected ILocalNotionRepository Repository { get; }

	/// <summary>
	/// Downloads and renders pages from a CMSDatabase Database.
	/// </summary>
	/// <param name="databaseID">Notion ID of database to download</param>
	/// <param name="knownNotionLastEditTime">The last edited time of database as currently on notion. Only specify IF KNOWN and leave null otherwise. This can help speed-up sync in many items scenarios.</param>
	/// <param name="options">The options for downloading database.</param>
	/// <param name="cancellationToken">Task cancellation token</param>
	/// <returns>All updated resources. Will be empty if nothing was synced.</returns>
	public async Task<LocalNotionResource[]> DownloadDatabaseAsync(string databaseID, DateTime? knownNotionLastEditTime = null, DownloadOptions options = null, CancellationToken cancellationToken = default) {
		Guard.ArgumentNotNull(databaseID, nameof(databaseID));
		options ??= DownloadOptions.Default;
		databaseID = LocalNotionHelper.SanitizeObjectID(databaseID);
		var downloadedResources = new List<LocalNotionResource>();
		var processedPages = new List<Page>();
		await using (Repository.EnterUpdateScope()) {

			// Track the database objects
			Database notionDatabase = null;
			IList<Page> notionChildPages = null;

			// Nested method used to fetch notion page (if required, the logic herein
			// tries to avoid calling this)
			async Task FetchNotionDatabase() {
				Logger.Info($"Fetching database: '{databaseID}'");
				notionDatabase = await FetchDatabaseHeader(databaseID, cancellationToken);
				knownNotionLastEditTime = notionDatabase.LastEditedTime;
			}

			// Fetch page, we need to know last edit tie on notion
			if (knownNotionLastEditTime == null)
				await FetchNotionDatabase();

			var containsDatabase = Repository.TryGetDatabase(databaseID, out var localDatabase);
			var preDownloadLocalRows = Repository.GetChildResources(databaseID).ToArray();
			var databaseDifferent = containsDatabase && localDatabase.LastEditedOn != knownNotionLastEditTime;
			var wasPrematurelySynced = containsDatabase && !databaseDifferent && knownNotionLastEditTime.HasValue && Math.Abs((localDatabase.LastSyncedOn - localDatabase.LastEditedOn).TotalSeconds) <= Constants.PrematureSyncThreshholdSec;
			var shouldDownload = !containsDatabase || databaseDifferent || options.ForceRefresh || wasPrematurelySynced;
			var lastKnownParent = localDatabase?.ParentResourceID;

			// Fetch child page headers (will be needed)
			Logger.Info($"Fetching database rows: '{databaseID}'");
			notionChildPages = await FetchDatabasePagesAsync(databaseID, null, cancellationToken).ToListAsync(cancellationToken);

			if (shouldDownload) {

				// fetch database if not already
				await FetchNotionDatabase();

				// Remove local database (if applicable)				
				if (containsDatabase)
					Repository.RemoveResource(databaseID, false);  // dangling child resources removed when fetching rows below

				// Hydrate the fetched database from notion
				localDatabase = LocalNotionHelper.ParseDatabase(notionDatabase);

				// Ensure page name is unique (resolve conflicts)
				localDatabase.Name = CalculateUniqueResourceName(localDatabase.Name);

				// Fetch page graph from notion
				Logger.Info($"Fetching database graph for '{notionDatabase.GetTitle()}' ({notionDatabase.Id})");
				
				// Determine the parent page
				localDatabase.ParentResourceID = CalculateResourceParent(localDatabase.ID); // note: it's parent, if it has one, will be in the Repository at this point

				// Save notion objects
				if (Repository.ContainsObject(notionDatabase.Id))
					Repository.RemoveObject(notionDatabase.Id);
				Repository.SaveObject(notionDatabase);

				// Save the page graph (json file describing the object graph)
				var databaseGraph = new NotionObjectGraph {
					ObjectID = databaseID,
					Children = notionChildPages.Select(x => new NotionObjectGraph { ObjectID = x.Id } ).Reverse().ToArray()
				};

				// Determine feature image
				localDatabase.FeatureImageID = LocalNotionHelper.CalculateFeatureImageID(localDatabase, notionDatabase, databaseGraph);

				Repository.SavePageGraph(databaseGraph);

				// Save local notion page resource 
				Repository.AddResource(localDatabase);

				// Track this resource for rendering below
				downloadedResources.Add(localDatabase);

				// In order to support cyclic references between parent -> child pages in the case where
				// no object-id folders are used, we generate blank stub at download time. This is because trying to predict
				// the render as ILinkGenerator's do is unreliable due to potential for conflicting filenames.
				// Search for 646870E8-FEDC-45F0-9CF5-B8945C4A2F9E in source code for code support relating to this
				// issue.
				if (!Repository.Paths.UsesObjectIDSubFolders(LocalNotionResourceType.Database))
					Repository.ImportBlankResourceRender(notionDatabase.Id, options.RenderType);

				var linkGenerator = LinkGeneratorFactory.Create(Repository);
				// Download cover
				if (localDatabase.Cover != null && ShouldDownloadFile(localDatabase.Cover)) {
					// Cover is a Notion file
					var file = await DownloadFileAsync(localDatabase.Cover, notionDatabase.Id, options.ForceRefresh, cancellationToken);
					if (file != null) {
						localDatabase.Cover = linkGenerator.Generate(localDatabase, file.ID, RenderType.File, out _);
						// update notion object with url (this is a component object and saved with page)
						notionDatabase.Cover.SetUrl(LocalNotionRenderLink.GenerateUrl(file.ID, RenderType.File));

						// track new file
						downloadedResources.Add(file);
					}
				}

				// Download thumbnail
				if (localDatabase.Thumbnail.Type == ThumbnailType.Image && ShouldDownloadFile(localDatabase.Thumbnail.Data)) {
					// Thumbnail is a Notion file
					var file = await DownloadFileAsync(localDatabase.Thumbnail.Data, notionDatabase.Id, options.ForceRefresh, cancellationToken);
					if (file != null) {
						localDatabase.Thumbnail.Data = linkGenerator.Generate(localDatabase, file.ID, RenderType.File, out _);
						// update notion object with url (this is a component object and saved with page)
						((FileObject)notionDatabase.Icon).SetUrl(LocalNotionRenderLink.GenerateUrl(file.ID, RenderType.File)); // update the locally stored NotionObject with local url

						// track new file
						downloadedResources.Add(file);
					}
				}

			}

			// Fetch updated database pages
			var postDownloadRows = new List<Page>();
			// Download
			var pageSequence = 0;
			 foreach (var page in notionChildPages) {
				cancellationToken.ThrowIfCancellationRequested();
				try {
					postDownloadRows.Add(page);
					var downloads = await DownloadPageAsync(page.Id, page.LastEditedTime, DownloadOptions.WithoutRender(options), cancellationToken: cancellationToken); // rendering deferred to below
					downloadedResources.AddRange(downloads);
					processedPages.Add(page);
					pageSequence++;
				} catch (ProductLicenseLimitException) {
					Logger.Error("You have reached the limit of pages available under your license. Please purchase a license to pull more pages/databases from Notion.");
					break;
				} catch (TaskCanceledException) {
					throw;
				} catch (Exception error) {
					Logger.Error($"Failed to process page '{page.Id}'.");
					Logger.Exception(error);
					if (!options.FaultTolerant)
						throw;
				}
			}

			// Locally delete rows that were removed in Notion
			foreach (var oldResource in preDownloadLocalRows.Select(x => x.ID).Except(postDownloadRows.Select(x => x.Id))) {
				Repository.RemoveResource(oldResource, true);
			}

			// Render
			if (options.Render) {
				var renderer = new RenderingManager(Repository, Logger);
				var renderableResources = LocalNotionHelper.FilterRenderableResources(downloadedResources).ToArray();
				foreach (var resource in renderableResources) {
					cancellationToken.ThrowIfCancellationRequested();
					try {
						renderer.RenderLocalResource(resource.ID, options.RenderType, options.RenderMode);
					} catch (TaskCanceledException) {
						throw;
					} catch (Exception error) {
						Logger.Error($"Failed to render resource '{resource.Title}' ({resource.ID})");
						Logger.Exception(error);
						if (!options.FaultTolerant)
							throw;
					}
				}

				// Render CMS items
				var renderedResources = renderableResources.Select(x => x.ID).ToHashSet();
				foreach (var cmsItem in Repository.CMSItems) {
					if (cmsItem.ReferencesAnyResources(renderedResources)) {
						try {
							renderer.RenderCMSItem(cmsItem);
						} catch (TaskCanceledException) {
							throw;
						} catch (Exception error) {
							Logger.Error($"Failed to render CMS item '{cmsItem.Slug}'");
							Logger.Exception(error);
							if (!options.FaultTolerant)
								throw;
						}

					}
				}
	
			}
			return downloadedResources.ToArray();
		}
	}

	public async Task<LocalNotionResource[]> DownloadPageAsync(string pageID, DateTime? knownNotionLastEditTime = null, DownloadOptions options = null, CancellationToken cancellationToken = default) {
		Guard.ArgumentNotNull(pageID, nameof(pageID));
		options ??= DownloadOptions.Default;
		var downloadedResources = new List<LocalNotionResource>();

		// ensure correct
		pageID = LocalNotionHelper.SanitizeObjectID(pageID);

		await using (Repository.EnterUpdateScope()) {

			// Track the page objects. 
			Page notionPage = null;
			LocalNotionPage localPage = null;
			IDictionary<string, IObject> pageObjects;
			NotionObjectGraph pageGraph = default;
			List<(string, DateTime?)> childPages = default;
			List<(string, DateTime?)> childDatabases = default;

			// Nested method used to fetch notion page (if required, the logic herein
			// tries to avoid calling this)
			async Task FetchNotionPage() {
				Logger.Info($"Fetching page '{pageID}'");
				notionPage = await NotionClient.Pages.RetrieveAsync(pageID, cancellationToken);
				knownNotionLastEditTime = notionPage.LastEditedTime;
			}

			#region Determine if Page should be fetched

			// Fetch page, we need to know last edit tie on notion
			if (knownNotionLastEditTime == null)
				await FetchNotionPage();

			var containsPage = Repository.TryGetPage(pageID, out localPage);
			var oldChildResources = Repository.GetChildResources(pageID).ToArray();
			var pageDifferent = containsPage && localPage.LastEditedOn != knownNotionLastEditTime;
			var wasPrematurelySynced = containsPage && !pageDifferent && knownNotionLastEditTime.HasValue && Math.Abs((localPage.LastSyncedOn - localPage.LastEditedOn).TotalSeconds) <= Constants.PrematureSyncThreshholdSec;
			var shouldDownload = !containsPage || pageDifferent || options.ForceRefresh || wasPrematurelySynced;
			var lastKnownParent = localPage?.ParentResourceID;

			#endregion

			// Hydrate into a LocalNotion page
			if (shouldDownload) { 

				#region Download page from Notion

				// Fetch page if not already
				if (notionPage == null)
					await FetchNotionPage();

				// Remove local page (if applicable)				
				if (containsPage)
					Repository.RemoveResource(pageID, false);  // dangling child resources removed below

				// Hydrate the fetched page from notion
				localPage = LocalNotionHelper.ParsePage(notionPage);

				// Ensure page name is unique (resolve conflicts)
				localPage.Name = CalculateUniqueResourceName(localPage.Name);

				// Fetch page graph from notion
				Logger.Info($"Fetching page graph for '{notionPage.GetTitle()}' ({notionPage.Id})");
				pageObjects = new Dictionary<string, IObject>();
				pageObjects.Add(notionPage.Id, notionPage);    // optimization: pre-add Page root object to avoid expensive fetch
				pageGraph = await NotionClient.Blocks.GetObjectGraphAsync(notionPage.Id, pageObjects, cancellationToken);

				// Determine the parent page
				localPage.ParentResourceID = CalculateResourceParent(localPage.ID, pageObjects);

				// Determine feature image
				localPage.FeatureImageID = LocalNotionHelper.CalculateFeatureImageID(localPage, notionPage, pageObjects, pageGraph);

				// Parse keywords using a text renderer
				var textRenderer = new TextRenderer(Logger);
				var text = textRenderer.Render(localPage, pageGraph, pageObjects, Repository.Paths.GetResourceFolderPath(LocalNotionResourceType.Page, localPage.ID, FileSystemPathType.Absolute));
				localPage.Keywords = RakeAlgorithm.Run([text], minCharLength: 2).Select(x => x.Key).Take(10).ToArray();
				
				// Determine child resources
				childPages =
					pageObjects
					.Values
					.Where(x => x is ChildPageBlock)
					.Cast<ChildPageBlock>()
					.Select(x => (x.Id, new DateTime?(x.LastEditedTime)))
					.ToList();

				childDatabases =
					pageObjects
						.Values
						.Where(x => x is ChildDatabaseBlock)
						.Cast<ChildDatabaseBlock>()
						.Select(x => (x.Id, new DateTime?(x.LastEditedTime)))
						.ToList();

				// Hydrate a Notion CMS summaries (and future plugins)
				if (Repository.CMSDatabaseID is not null) {
					if (CMSHelper.IsCMSPage(notionPage)) {
						// Page is a CMSDatabase page
						var htmlThemeManager = new HtmlThemeManager(Repository.Paths, Logger);
						localPage.CMSProperties = CMSHelper.ParseCMSProperties(localPage.Name, notionPage);
					} else if (localPage.ParentResourceID != null && Repository.TryGetPage(localPage.ParentResourceID, out var parentPage) && parentPage.CMSProperties != null) {
						// Page has a CMSDatabase page ancestor, so propagate CMS properties down
						localPage.CMSProperties = CMSHelper.ParseCMSPropertiesAsChildPage(localPage.Name, notionPage, parentPage);
					}
				}

				// If no summary was provided, generate one
				if (localPage.CMSProperties is { Summary: null })
					localPage.CMSProperties.Summary = LocalNotionHelper.ExtractDefaultPageSummary(pageGraph, pageObjects);

				#endregion

				#region Download attached files

				var linkGenerator = LinkGeneratorFactory.Create(Repository);
				// Download cover
				if (localPage.Cover != null && ShouldDownloadFile(localPage.Cover)) {
					// Cover is a Notion file
					var file = await DownloadFileAsync(localPage.Cover, notionPage.Id, options.ForceRefresh, cancellationToken);
					if (file != null) {
						localPage.Cover = linkGenerator.Generate(localPage, file.ID, RenderType.File, out _);
						// update notion object with url (this is a component object and saved with page)
						notionPage.Cover.SetUrl(LocalNotionRenderLink.GenerateUrl(file.ID, RenderType.File));

						// track new file
						downloadedResources.Add(file);
					}
				}

				// Download thumbnail
				if (localPage.Thumbnail.Type == ThumbnailType.Image && ShouldDownloadFile(localPage.Thumbnail.Data)) {
					// Thumbnail is a Notion file
					var file = await DownloadFileAsync(localPage.Thumbnail.Data, notionPage.Id, options.ForceRefresh, cancellationToken);
					if (file != null) {
						localPage.Thumbnail.Data = linkGenerator.Generate(localPage, file.ID, RenderType.File, out _);
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
						.Where(x => x is FileObject || x is FileObjectWithName)
						.Select(x => new WrappedNotionFile(x))
						.Union(
							notionPage
								.Properties
								.Values
								.Where(y => y is FilesPropertyValue)
								.Cast<FilesPropertyValue>()
								.SelectMany(y => y.Files)
								.Select(y => new WrappedNotionFile(y))
						).ToArray();
				foreach (var file in uploadedFiles) {
					var url = file.GetUrl();
					if (ShouldDownloadFile(url)) {
						var downloadedFile = await DownloadFileAsync(url, notionPage.Id, options.ForceRefresh, cancellationToken);
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
				// the render as ILinkGenerator's do is unreliable due to potential for conflicting filenames.
				// Search for 646870E8-FEDC-45F0-9CF5-B8945C4A2F9E in source code for code support relating to this
				// issue.
				if (!Repository.Paths.UsesObjectIDSubFolders(LocalNotionResourceType.Page))
					Repository.ImportBlankResourceRender(notionPage.Id, options.RenderType);

				#endregion

			} else {
				#region CASE: Page already downloaded and is unchanged

				// Case: the page is unchanged but it was moved within the table, thus sequence is different
				// TODO: uncomment this when Notion return table records in correct order
				//if (localPage.Sequence != sequence && sequence != null) {
				//	localPage.Sequence = sequence; 
				//	Repository.UpdateResource(localPage);
				//}

				// Fetch local versions of page objcets and graph
				// since we still need to downlaod  child pages (and if
				// any are changed, this page needs re-rendering)
				Guard.Ensure(localPage != null);
				Logger.Info($"No changes detected for `{localPage.Title}`");
				pageGraph = Repository.GetEditableResourceGraph(pageID);
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

				childDatabases =
					pageObjects
						.Values
						.Where(x => x is ChildDatabaseBlock)
						.Cast<ChildDatabaseBlock>()
						.Select(x => (x.Id, null as DateTime?))
						.Union(
							pageObjects
								.Values
								.Where(x => x is Database)
								.Cast<Database>()
								.Select(x => (x.Id, null as DateTime?))
						)
						.ToList();

				// Render the unchanged page if no render exists
				if (!localPage.TryGetRender(options.RenderType, out _))
					downloadedResources.Add(localPage);

				#endregion
			}

			#region Download any child pages

			foreach (var childPage in childPages) {
				// We call download to sub-pages but with rendering off, only top-level will ever render itself and all children
				var subPageDownloads = await DownloadPageAsync(childPage.Item1, childPage.Item2, DownloadOptions.WithoutRender(options), cancellationToken);
				downloadedResources.AddRange(subPageDownloads);
			}

			// If page was not changed but a child page was changed, then we add it to renderable
			// set because links and other things may be changed
			if (!downloadedResources.Contains(localPage) &&
				downloadedResources.Any(x => x is LocalNotionPage && childPages.Any(y => x.ID == y.Item1)))
				downloadedResources.Add(localPage);

			#endregion

			#region Download any child databases

			foreach (var childDatabase in childDatabases) {
				// We download child database but with rendering off, only top-level will ever render itself and all children
				var subPageDownloads = await DownloadDatabaseAsync(childDatabase.Item1, childDatabase.Item2, DownloadOptions.WithoutRender(options), cancellationToken);
				downloadedResources.AddRange(subPageDownloads);
			}

			// If page was not changed but a child page was changed, then we add it to renderable
			// set because links and other things may be changed
			if (!downloadedResources.Contains(localPage) &&
			    downloadedResources.Any(x => x is LocalNotionPage && childPages.Any(y => x.ID == y.Item1)))
				downloadedResources.Add(localPage);

			#endregion

			#region Remove old child resources

			foreach (var childResource in oldChildResources.Except(Repository.GetChildResources(pageID), EqualityComparerBuilder.For<LocalNotionResource>().By(r => r.ID))) {
				Logger.Info($"Removing '{childResource.Title}' ({childResource.ID})");
				Repository.RemoveResource(childResource.ID, true);
			}

			#endregion

			#region Render page and child-pages

			if (options.Render) {
				var renderer = new RenderingManager(Repository, Logger);
				var renderableResources = LocalNotionHelper.FilterRenderableResources(downloadedResources).ToArray();

				// render pages
				foreach (var renderableResource in renderableResources) {
					cancellationToken.ThrowIfCancellationRequested();
					try {
						renderer.RenderLocalResource(renderableResource.ID, options.RenderType, options.RenderMode);
					} catch (ProductLicenseLimitException) {
						throw;
					} catch (TaskCanceledException) {
						throw;
					} catch (Exception error) {
						Logger.Error($"Failed to render page '{renderableResource.Title}' ({renderableResource.ID}).");
						Logger.Exception(error);
						if (!options.FaultTolerant)
							throw;
					}
				}

				// Render CMS items
				var renderedResources = renderableResources.Select(x => x.ID).ToHashSet();
				foreach (var cmsItem in Repository.CMSItems) {
					if (cmsItem.ReferencesAnyResources(renderedResources)) {
						try {
							renderer.RenderCMSItem(cmsItem);
						} catch (TaskCanceledException) {
							throw;
						} catch (Exception error) {
							Logger.Error($"Failed to render CMS item '{cmsItem.Slug}'");
							Logger.Exception(error);
							if (!options.FaultTolerant)
								throw;
						}

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
					filename = Tools.Web.Downloader.ParseFilenameFromUrl(url);
					if (!Tools.FileSystem.IsWellFormedFileName(filename))
						filename = "LN_" + Guid.Parse(resourceID).ToStrictAlphaString();
				} else throw new InvalidOperationException($"Url is not a recognized notion file url and downloading external content is not enabled. Url: '{url}'");
			}

			if (Repository.TryGetFile(resourceID, out var file)) {
				if (!force && Repository.ContainsResourceRender(resourceID, RenderType.File)) {
					Logger.Info($"Skipped downloading already existing file '{resourceID}/{filename}'");
					return file;
				}
				Repository.RemoveResource(resourceID, true);
			}

			Logger.Info($"Downloading: {filename} (resource: {resourceID})");
			var tmpFile = Tools.FileSystem.GetTempFileName();
			try {
				var mimeType = await Tools.Web.Downloader.DownloadFileAsync(url, tmpFile, verifySSLCert: false, cancellationToken);
				file = LocalNotionFile.Parse(resourceID, filename, parentResourceID, mimeType);
				file.ParentResourceID = CalculateResourceParent(file.ID);
				Repository.AddResource(file);
				Repository.ImportResourceRender(resourceID, RenderType.File, tmpFile);
			} catch (TaskCanceledException) {
				Logger.Info($"Downloading Cancelled: {filename} (resource: {resourceID})");
				throw;
			} catch (ProductLicenseLimitException) {
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

	public async Task<Database> FetchDatabaseHeader(string databaseID, CancellationToken cancellationToken = default) {
		return await NotionClient.Databases.RetrieveAsync(databaseID, cancellationToken);
	}

	public IAsyncEnumerable<Page> FetchDatabasePagesAsync(string databaseID, DateTime? updatedOnOrAfter = null, CancellationToken cancellationToken = default) {
		Logger.Info($"Fetching updated pages for database '{databaseID}'{(updatedOnOrAfter.HasValue ? $" (updated on or after {updatedOnOrAfter:yyyy-MM-dd HH:mm:ss.fff}" : string.Empty)}");
		return NotionClient.Databases.EnumerateAsync(
			databaseID,
			new DatabasesQueryParameters { Filter = updatedOnOrAfter.HasValue ? new TimestampLastEditedTimeFilter(onOrAfter: updatedOnOrAfter) : null, },
			cancellationToken
		);
	}

	private bool ShouldDownloadFile(string url)
		// We only download user files uploaded to notion (or everything is force download is on)
		=> (Repository.Paths.ForceDownloadExternalContent && !Tools.Url.IsVideoSharingUrl(url)) ||
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

	/// <summary>
	/// Calculates the parent resource of the given object. A "resource" is something that is rendered by Local Notion.
	/// </summary>
	protected string CalculateResourceParent(string objectID, IDictionary<string, IObject> nonPersistedObjects = null) {
		nonPersistedObjects ??= new Dictionary<string, IObject>();
		var visited = new HashSet<string>();
		while (!visited.Contains(objectID) && (nonPersistedObjects.TryGetValue(objectID, out var obj) || Repository.TryGetObject(objectID, out obj))) {
			visited.Add(objectID);
			if (obj.TryGetParent(out var parent)) {
				switch (parent.Type) {
					case ParentType.DatabaseId:
					case ParentType.PageId:
					case ParentType.Workspace:
						return parent.GetId();
					case ParentType.Unknown:
					case ParentType.BlockId:
					default:
						// Parent is a block, so search for that block's parent until we find DB, WS or Page
						objectID = parent.GetId();
						break;
				}
			}
		}
		return null;
	}

	protected string CalculateUniqueResourceName(string resourceName) {
		var nameCandidate = resourceName;
		var attempt = 2;
		while (Repository.ContainsResourceByName(nameCandidate))
			nameCandidate = $"{resourceName}-{attempt++}";
		return nameCandidate;
	}

}