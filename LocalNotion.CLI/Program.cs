using Hydrogen;
using Hydrogen.Application;
using Notion.Client;
using CommandLine;
using System.Runtime.Serialization;
using LocalNotion.Core;

namespace LocalNotion.CLI;

public static partial class Program {

	private static CancellationTokenSource CancelProgram { get; } = new CancellationTokenSource();

	private static string GetDefaultRepoFolder() 
		=> System.IO.Path.Combine(Environment.CurrentDirectory);

	private static string ToFullPath(string userEnteredPath) {
		if (Path.IsPathFullyQualified(userEnteredPath))
			return userEnteredPath;
		return Path.GetFullPath(userEnteredPath, Environment.CurrentDirectory);
	}

	private static string GetInputPathRelativeToRepo(string repoPath, string userEnteredPath) {
		if (Path.IsPathFullyQualified(userEnteredPath))
			return Path.GetRelativePath(repoPath, userEnteredPath);;
		return Path.GetRelativePath(repoPath, Path.GetFullPath(userEnteredPath, Environment.CurrentDirectory));
	}

	private static async Task<ILocalNotionRepository> OpenRepo(string userPath, ILogger logger) {
		userPath = ToFullPath(userPath);
		var repo = 
			Directory.Exists(userPath) ? 
			await LocalNotionRepository.Open(userPath, logger) : 
			await LocalNotionRepository.OpenRegistry(userPath, logger);
		return repo;
	}

	public enum LocalNotionProfileDescriptor {
		[EnumMember(Value = "backup")]
		Backup,

		[EnumMember(Value = "offline")]
		Offline,

		[EnumMember(Value = "publishing")]
		Publishing,

		[EnumMember(Value = "webhosting")]
		WebHosting,

	}

	public abstract class CommandArgumentsBase {
		
		[Option("cancel-trigger", Hidden = true)]
		public string CancelTriggerPath { get; set; } = null;

	}
	
	[Verb("status", HelpText = "Provides status of the Local Notion repository")]
	public class StatusRepositoryCommandArguments : CommandArgumentsBase {

		[Option('p', "path", HelpText = "Path to Local Notion repository")]
		public string Path { get; set; } = GetDefaultRepoFolder();

		
		[Option('v', "verbose", HelpText = $"Display debug information in console output")]
		public bool Verbose { get; set; } = false;
	}
	
	[Verb("init", HelpText = "Creates a Local Notion repository")]
	public class InitRepositoryCommandArguments : CommandArgumentsBase {

		[Option('p', "path", HelpText = "Path to Local Notion repository")]
		public string Path { get; set; } = GetDefaultRepoFolder();

		[Option('k', "key", HelpText = "Notion API key to use when contacting notion (do not pass in low security environment)")]
		public string APIKey { get; set; } = null;

		[Option('l', "log-level", Default = LogLevel.Info, HelpText = $"Logging level in log files (Options: debug, info, warning, error)")]
		public LogLevel LogLevel { get; set; }

		[Option('x', "profile", Default = LocalNotionProfileDescriptor.Backup, HelpText = $"Determines how to organizes files and generate links in your repository (options: backup, offline, publishing, website)")]
		public LocalNotionProfileDescriptor Profile { get; set; }

		[Option('t', "theme", Default = "default", HelpText = $"Local Notion theme used for rendering")]
		public string Theme { get; set; }
		
		[Option('v', "verbose", HelpText = $"Display debug information in console output")]
		public bool Verbose { get; set; } = false;

		[Option("override-objects-path", HelpText = $"Override path where notion objects are stored")]
		public string ObjectsPathOverride { get; set; } = null;

		[Option("override-pages-path", HelpText = $"Override path where rendered pages are stored")]
		public string PagesPathOverride { get; set; } = null;

		[Option("override-db-path", HelpText = $"Override path where rendered databases are stored")]
		public string DatabasePathOverride { get; set; } = null;

		[Option("override-workspace-path", HelpText = $"Override path where rendered workspace pages are stored")]
		public string WorkspacePathOverride { get; set; } = null;

		[Option("override-files-path", HelpText = $"Override path where files are stored")]
		public string FilesPathOverride { get; set; } = null;

		[Option("override-themes-path", HelpText = $"Override path where local notion themes are stored")]
		public string ThemesPathOverride { get; set; } = null;

		[Option("override-logs-path", HelpText = $"Override path that stores the log files")]
		public string LogsPathOverride { get; set; } = null;

		[Option("override-mode", HelpText = "Override the link generation mode (\"offline\" generates links to local files whereas \"online\" links to remote web server")]
		public LocalNotionMode? ModeOverride { get; set; } = null;

		[Option("override-base-url", HelpText = $"Override base URL for generated content links")]
		public string BaseUrlOverride { get; set; } = null;
	
	}
	
	[Verb("remove", HelpText = "Remove resources from a Local Notion repository")]
	public class RemoveRepositoryCommandArguments : CommandArgumentsBase {

		[Option('r', "path", HelpText = "Path to Local Notion repository (default is current working dir)")]
		public string Path { get; set; } = GetDefaultRepoFolder();

		[Option("all", HelpText = "Removes entire repository")]
		public bool All { get; set; }

		[Option('o', "objects", HelpText = "List of Notion objects to remove (i.e. pages, databases, workspaces)")]
		public IEnumerable<string> Objects { get; set; } = null;

		[Option('v', "verbose", HelpText = $"Display debug information in console output")]
		public bool Verbose { get; set; } = false;

	}
	
	[Verb("list", HelpText = "Lists objects from Notion which can be pulled into Local Notion")]
	public class ListContentsCommandArguments : CommandArgumentsBase {
		
		[Option('o', "objects", HelpText = "Filter by these objects (default lists workspace)")]
		public IEnumerable<string> Objects { get; set; } = null;

		[Option('a', "all", HelpText = "Include child items")]
		public bool All { get; set; } = false;

		[Option('f', "filter", HelpText = "Filter by object title")]
		public string Filter { get; set; } = null;
		
		[Option('k', "key", HelpText = "Notion API key to (overrides key specified in repository)")]
		public string APIKey { get; set; } = null;

		[Option('p', "path", HelpText = "Path to Local Notion repository (default is current working dir)")]
		public string Path { get; set; } = GetDefaultRepoFolder();
		
		[Option('v', "verbose", HelpText = $"Display debug information in console output")]
		public bool Verbose { get; set; } = false;
	}

	[Verb("pull", HelpText = "Pulls Notion objects into a Local Notion repository")]
	public class PullRepositoryCommandArguments : CommandArgumentsBase {

		[Option('o', "objects", Group = "target", HelpText = "List of Notion objects to pull (i.e. pages, databases)")]
		public IEnumerable<string> Objects { get; set; } = null;
		
		[Option('k', "key", HelpText = "Notion API key to use (overrides repository key if any)")]
		public string APIKey { get; set; } = null;

		[Option('p', "path", HelpText = "Path to Local Notion repository (default is current working dir)")]
		public string Path { get; set; } = GetDefaultRepoFolder();

		//[Option("filter-source", HelpText = "Only sync CMS pages whose 'Source' property matches at least one from this list.")]
		//public IEnumerable<string> FilterSources { get; set; } = Array.Empty<string>();

		//[Option("filter-root", HelpText = "Only sync CMS pages whose 'Root' property matches at least one from this list.")]
		//public IEnumerable<string> FilterRoots { get; set; } = Array.Empty<string>();

		[Option("render", Default = (bool)true, HelpText = "Renders objects after pull")]
		public bool Render { get; set; }

		[Option("render-type", Default = RenderType.HTML, HelpText = "Type of rendering to use (HTML, PDF)")]
		public RenderType RenderOutput { get; set; }

		[Option("render-mode", Default = RenderMode.ReadOnly, HelpText = "Rendering mode for objects (ReadOnly, Editable)")]
		public RenderMode RenderMode { get; set; }

		[Option("fault-tolerant", Default = (bool)true, HelpText = "Continues processing on failures")]
		public bool FaultTolerant { get; set; }

		[Option("force", HelpText = "Forces downloading of objects even if unchanged")]
		public bool Force { get; set; } = false;

		[Option('v', "verbose", HelpText = $"Display debug information in console output")]
		public bool Verbose { get; set; } = false;

	}

	[Verb("sync", HelpText = "Synchronizes a Local Notion repository with Notion (until process manually terminated)")]
	public class SyncRepositoryCommandArguments : PullRepositoryCommandArguments {

		[Option('f', "poll-frequency", Default = 30, HelpText = "How often to poll Notion for changes")]
		public int PollFrequency { get; set; }
	}
			
	[Verb("render", HelpText = "Renders a Local Notion object (using local state only)")]
	public class RenderCommandArguments : CommandArgumentsBase {

		[Option('r', "path", HelpText = "Path to Local Notion repository (default is current working dir)")]
		public string Path { get; set; } = GetDefaultRepoFolder();

		[Option('o', "objects", Required = false, HelpText = "List of object ID's to render (i.e. page(s), database(s), workspace)")]
		public IEnumerable<string> Objects { get; set; } = null;

		[Option('a', "all", HelpText = "Renders all objects in repository")]
		public bool RenderAll { get; set; }

		[Option("render-type", Default = RenderType.HTML, HelpText = "Type of rendering to use (HTML, PDF)")]
		public RenderType RenderOutput { get; set; }

		[Option("render-mode", Default = RenderMode.ReadOnly, HelpText = "Rendering mode for objects (ReadOnly, Editable)")]
		public RenderMode RenderMode { get; set; }

		[Option("fault-tolerant", Default = (bool)true, HelpText = "Continues processing on failures")]
		public bool FaultTolerant { get; set; }

		[Option('v', "verbose", HelpText = $"Display debug information in console output")]
		public bool Verbose { get; set; } = false;

	}

	[Verb("prune", HelpText = "Removes objects from a Local Notion that no longer exist in Notion")]
	public class PruneCommandArguments : CommandArgumentsBase {

		[Option('r', "path", HelpText = "Path to Local Notion repository (default current working dir)")]
		public string Path { get; set; } = GetDefaultRepoFolder();

		[Option('o', "objects", HelpText = "List of object ID's to keep (i.e. page(s), database(s), workspace)")]
		public IEnumerable<string> Objects { get; set; } = null;

		[Option('v', "verbose", HelpText = $"Display debug information in console output")]
		public bool Verbose { get; set; } = false;

	}

	[Verb("license", HelpText = "Manages Local Notion license")]
	public class LicenseCommandArguments : CommandArgumentsBase {

		[Option('a', "activate", HelpText = "Local Notion key.")]
		public string ProductKey { get; set; } = string.Empty;

		[Option('v', "verify", Hidden= true)]
		public bool Verify { get; set; } = false;

		[Option('v', "verbose", HelpText = $"Display debug information in console output")]
		public bool Verbose { get; set; } = false;

	}

	[Verb("service", HelpText = "Runs as service")]
	public class ServiceCommandArguments {

		[Option('o', "objects", Required = true, Group = "target", HelpText = "List of Notion objects to pull (i.e. pages, databases)")]
		public IEnumerable<string> Objects { get; set; } = null;

		[Option('p', "path", Required = true, HelpText = "Path to Local Notion repository (or registry)")]
		public string Path { get; set; }

		[Option('f', "poll-frequency", Default = 30, HelpText = "How often to poll Notion for changes")]
		public int PollFrequency { get; set; } 

	}


	public static async Task<int> ExecuteStatusCommandAsync(StatusRepositoryCommandArguments arguments, CancellationToken cancellationToken) {
		var consoleLogger = new ConsoleLogger { Options =  arguments.Verbose ? LogOptions.VerboseProfile : LogOptions.UserDisplayProfile };

		if (!Directory.Exists(arguments.Path)) {
			consoleLogger.Error($"Repository not found: {arguments.Path}");
			return Constants.ERRORCODE_REPO_NOT_FOUND;
		}

		var repo = await LocalNotionRepository.Open(arguments.Path, consoleLogger);
		System.Console.WriteLine(
$@"Local Notion Status:
	Total Resources: {repo.Resources.Count()}
	Total Objects: {repo.Objects.Count()}
	Total Graphs: {repo.Graphs.Count()}");

		Console.WriteLine();
		Console.WriteLine($"\tLocal Notion Resource Paths (relative):");
		foreach(var resourceType in Enum.GetValues<LocalNotionResourceType>()) 
			Console.WriteLine($"\t\t{resourceType}: {repo.Paths.GetResourceTypeFolderPath(resourceType, FileSystemPathType.Relative)}");
		Console.WriteLine();
		Console.WriteLine($"\tInternal Resource Paths (relative):");
		Console.WriteLine($"\t\tRegistry: {repo.Paths.GetRegistryFilePath(FileSystemPathType.Relative)}");
		foreach(var internalResourceType in Enum.GetValues<InternalResourceType>()) 
			Console.WriteLine($"\t\t{internalResourceType}: {repo.Paths.GetInternalResourceFolderPath(internalResourceType, FileSystemPathType.Relative)}");
		
		return Constants.ERRORCODE_OK;
	}

	public static async Task<int> ExecuteInitCommandAsync(InitRepositoryCommandArguments arguments, CancellationToken cancellationToken) {
		var consoleLogger = new ConsoleLogger { Options =  arguments.Verbose ? LogOptions.VerboseProfile : LogOptions.UserDisplayProfile };

		arguments.Path = ToFullPath(arguments.Path);
		if (!Directory.Exists(arguments.Path)) 
			throw new DirectoryNotFoundException(arguments.Path);

		var pathProfile = arguments.Profile switch {
			LocalNotionProfileDescriptor.Backup => LocalNotionPathProfile.Backup,
			LocalNotionProfileDescriptor.Offline => LocalNotionPathProfile.Offline,
			LocalNotionProfileDescriptor.Publishing => LocalNotionPathProfile.Publishing,
			LocalNotionProfileDescriptor.WebHosting => LocalNotionPathProfile.WebHosting,
			_ => throw new NotSupportedException(arguments.Profile.ToString())
		};

		if (!string.IsNullOrWhiteSpace(arguments.BaseUrlOverride))
			pathProfile.BaseUrl = arguments.BaseUrlOverride;;
		if (!string.IsNullOrWhiteSpace(arguments.DatabasePathOverride))
			pathProfile.DatabasesPathR = GetInputPathRelativeToRepo(arguments.Path, arguments.DatabasePathOverride);
		if (!string.IsNullOrWhiteSpace(arguments.FilesPathOverride))
			pathProfile.FilesPathR = GetInputPathRelativeToRepo(arguments.Path, arguments.FilesPathOverride);
		if (!string.IsNullOrWhiteSpace(arguments.LogsPathOverride))
			pathProfile.LogsPathR = GetInputPathRelativeToRepo(arguments.Path, arguments.LogsPathOverride);
		if (!string.IsNullOrWhiteSpace(arguments.ObjectsPathOverride))
			pathProfile.ObjectsPathR = GetInputPathRelativeToRepo(arguments.Path, arguments.ObjectsPathOverride);
		if (!string.IsNullOrWhiteSpace(arguments.PagesPathOverride))
			pathProfile.PagesPathR = GetInputPathRelativeToRepo(arguments.Path, arguments.PagesPathOverride);
		if (!string.IsNullOrWhiteSpace(arguments.ThemesPathOverride))
			pathProfile.ThemesPathR = GetInputPathRelativeToRepo(arguments.Path, arguments.ThemesPathOverride);
		if (!string.IsNullOrWhiteSpace(arguments.WorkspacePathOverride))
			pathProfile.WorkspacePathR = GetInputPathRelativeToRepo(arguments.Path, arguments.WorkspacePathOverride);

		await LocalNotionRepository.CreateNew(
			arguments.Path,
			arguments.APIKey,
			arguments.Theme,
			arguments.LogLevel,
			pathProfile,
			logger: consoleLogger
		);
		consoleLogger.Info("Location Notion repository has been created");
		return Constants.ERRORCODE_OK;
	}

	public static async Task<int> ExecuteRemoveCommandAsync(RemoveRepositoryCommandArguments arguments, CancellationToken cancellationToken) {
		var consoleLogger = new ConsoleLogger { Options =  arguments.Verbose ? LogOptions.VerboseProfile : LogOptions.UserDisplayProfile };
		if (!arguments.All) {
			foreach (var objectID in arguments.Objects) {
				var repo = await LocalNotionRepository.Open(arguments.Path, consoleLogger);
				if (!ILocalNotionRepository.IsValidObjectID(objectID)) {
					consoleLogger.Warning($"ObjectID '{objectID}' was malformed");
					continue;
				}
				if (repo.ContainsResource(objectID)) {
					repo.RemoveResource(objectID, true);
					consoleLogger.Info($"Removed resource: {objectID}");
				}

				if (repo.ContainsObject(objectID)) {
					repo.RemoveObject(objectID);
					consoleLogger.Info($"Removed object: {objectID}");
				}
			}
		} else {
			if (await LocalNotionRepository.Remove(arguments.Path, consoleLogger))
				consoleLogger.Info("Local Notion repository has been removed");
			else
				consoleLogger.Warning("No Local Notion repository was found");
		}
		
		return Constants.ERRORCODE_OK;
	}

	public static async Task<int> ExecuteListCommand(ListContentsCommandArguments arguments, CancellationToken cancellationToken) {
		var consoleLogger = new ConsoleLogger { Options = arguments.Verbose ? LogOptions.VerboseProfile : LogOptions.UserDisplayProfile };
		
		string apiKey = default;

		if (!string.IsNullOrWhiteSpace(arguments.APIKey)) {
			apiKey = arguments.APIKey;
		} else {
			if (LocalNotionRepository.Exists(arguments.Path)) {
				var repo = await LocalNotionRepository.Open(arguments.Path, consoleLogger);
				apiKey = repo.DefaultNotionApiKey;
			}
		}
		
		if (string.IsNullOrWhiteSpace(apiKey)) {
			consoleLogger.Info("No API key was specified in argument or registered in repository");
			return Constants.ERRORCODE_COMMANDLINE_ERROR;
		}

		var client = NotionClientFactory.Create(new ClientOptions { AuthToken = apiKey });

		if (!arguments.Objects.Any()) {
			// List workspace level
			Console.WriteLine($"Listing workspace {$"filtering by '{arguments.Filter}'".AsAmendmentIf(!string.IsNullOrWhiteSpace(arguments.Filter))}{"(use --all switch to include child objects)".AsAmendmentIf(!arguments.All)}");
			var searchParameters = new SearchParameters { Query = arguments.Filter };
			var results = client.Search.EnumerateAsync(searchParameters, cancellationToken);
			if (!arguments.All) 
				results = results.WhereAwait(async x => x is Database or Page && x.GetParent() is WorkspaceParent);
			
			await foreach(var obj in results.WithCancellation(cancellationToken))
				PrintObject(obj);
				
		} else {
			// Lists database contents
			foreach(var @obj in arguments.Objects) {
				switch(await client.QualifyObjectAsync(@obj, cancellationToken))  {
					case (LocalNotionResourceType.Database, _): 
						PrintObject(await client.Databases.RetrieveAsync(@obj));
						if (arguments.All) {
							var searchParameters = new DatabasesQueryParameters();
							var results = client.Databases.EnumerateAsync(@obj, searchParameters, cancellationToken);
							await foreach(var dbPage in results.WithCancellation(cancellationToken))
								PrintObject(dbPage);
						}

						break;
					case (LocalNotionResourceType.Page, _): 
						PrintObject(await client.Pages.RetrieveAsync(@obj));
							break;
					default:
						Console.WriteLine($"Unrecognized object: {@obj}");
						break;

				};
			}
		}

		void PrintObject(IObject obj) {
			Console.WriteLine($"{obj.Id}   {ToAcronym(obj.Object).PadRight(2)}   {obj.GetLastEditedDate():yyyy-MM-dd HH:mm}   {obj.GetTitle()}");
		}

		string ToAcronym(ObjectType objectType) 
			=> objectType switch {
				ObjectType.Page => "P",
				ObjectType.Database => "DB",
				ObjectType.Block => "B",
				ObjectType.User => "U",
				ObjectType.Comment => "C",
				_ => throw new ArgumentOutOfRangeException(nameof(objectType), objectType, null)
			};

		return Constants.ERRORCODE_OK;
	}

	public static async Task<int> ExecutePullCommandAsync(PullRepositoryCommandArguments arguments, CancellationToken cancellationToken) {
		var consoleLogger = new ConsoleLogger { Options =  arguments.Verbose ? LogOptions.VerboseProfile : LogOptions.UserDisplayProfile };
		var repo = await LocalNotionRepository.Open(arguments.Path, consoleLogger);

		await using (repo.EnterUpdateScope()) {

			var apiKey = arguments.APIKey ?? repo.DefaultNotionApiKey;
			if (string.IsNullOrWhiteSpace(apiKey)) {
				consoleLogger.Info("No API key was specified in argument or registered in repository");
				return -1;
			}
			var client = NotionClientFactory.Create(new ClientOptions { AuthToken = apiKey });

			var syncOrchestrator = new NotionSyncOrchestrator(client, repo);

			foreach (var @obj in arguments.Objects) {
				var objType = await client.QualifyObjectAsync(@obj, cancellationToken);
				switch (objType) {
					case (null, _):
						consoleLogger.Info($"Unrecognized object: {@obj}");
						break;
					case (LocalNotionResourceType.Database, var lastEditedTime):

						await syncOrchestrator.DownloadDatabasePagesAsync(
							@obj,
							//arguments.FilterLastUpdatedOn,
							arguments.Render,
							arguments.RenderOutput,
							arguments.RenderMode,
							arguments.FaultTolerant,
							arguments.Force,
							cancellationToken
						);
						break;
					case (LocalNotionResourceType.Page, var lastEditTimeNotion):
						await syncOrchestrator.DownloadPageAsync(@obj, lastEditTimeNotion, arguments.Render, arguments.RenderOutput, arguments.RenderMode, null, arguments.FaultTolerant, arguments.Force, cancellationToken);
						break;
					default:
						consoleLogger.Info($"Synchronizing objects of type {objType} is not supported yet");
						break; ;
				}
			}
		}
		return 0;
	}

	public static async Task<int> ExecuteSyncCommandAsync(SyncRepositoryCommandArguments arguments, CancellationToken cancellationToken) {
		// NOTE: FilterLastUpdateOn not requied since SyncOrchestrator intelligently determines 
		// what to fetch

		var consoleLogger = new ConsoleLogger { Options =  arguments.Verbose ? LogOptions.VerboseProfile : LogOptions.UserDisplayProfile };
	
		Console.WriteLine($"Synchronizing every {arguments.PollFrequency} seconds (send Break or CTRL-C to stop)");
		while(true) {
			Console.WriteLine($"Synchronizing Updates: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
			//arguments.FilterLastUpdatedOn = DateTime.Now;
			var result = await ExecutePullCommandAsync(arguments, cancellationToken);
			if (result != Constants.ERRORCODE_OK && !arguments.FaultTolerant) 
				return result;
			await Task.Delay(TimeSpan.FromSeconds(arguments.PollFrequency), cancellationToken);
			//arguments.FilterLastUpdatedOn = DateTime.UtcNow;
		}
		return Constants.ERRORCODE_OK;
	}

	public static async Task<int> ExecuteRenderCommandAsync(RenderCommandArguments arguments, CancellationToken cancellationToken) {
		var consoleLogger = new ConsoleLogger { Options =  arguments.Verbose ? LogOptions.VerboseProfile : LogOptions.UserDisplayProfile };
		var repo = await LocalNotionRepository.Open(arguments.Path, consoleLogger);
		var renderer = new ResourceRenderer(repo, repo.Logger);
		var toRender = arguments.RenderAll ? repo.Resources.Where(x => x is LocalNotionPage).Select(x => x.ID) : arguments.Objects;
		if (!toRender.Any()) {
			consoleLogger.Warning("Nothing to render");
			return Constants.ERRORCODE_OK;
		}

		foreach (var resource in toRender) {
			try {
				cancellationToken.ThrowIfCancellationRequested();
				renderer.RenderLocalResource(resource, arguments.RenderOutput, arguments.RenderMode);
			} catch (Exception error) {
				consoleLogger.Exception(error);
				if (!arguments.FaultTolerant)
					throw;
			}
		}
		return Constants.ERRORCODE_OK;
	}

	public static async Task<int> ExecutePruneCommandAsync(PruneCommandArguments arguments, CancellationToken cancellationToken) {
		var consoleLogger = new ConsoleLogger { Options =  arguments.Verbose ? LogOptions.VerboseProfile : LogOptions.UserDisplayProfile };
		consoleLogger.Warning("Local Notion pruning is not currently implemented");
		return Constants.ERRORCODE_NOT_IMPLEMENTED;
	}

	public static async Task<int> ExecuteLicenseCommandAsync(LicenseCommandArguments arguments, CancellationToken cancellationToken) {
		SystemLog.Warning("Local Notion DRM is not currently implemented");
		return Constants.ERRORCODE_NOT_IMPLEMENTED;
	}

	public static async Task<int> ProcessCommandLineErrorsAsync(IEnumerable<Error> errors) {
		await Task.Delay(200); // give time for output to flush to parent process
		if (errors.Count() == 1 && errors.Single() is VersionRequestedError)
			return Constants.ERRORCODE_OK;

		return Constants.ERRORCODE_COMMANDLINE_ERROR;
	}

	public static async Task<int> ExecuteCommandAsync<T>(T args, Func<T, CancellationToken, Task<int>> command) where T : CommandArgumentsBase {
		try {
			Guard.ArgumentNotNull(args, nameof(args));
			using var disposables = new Disposables();
			if (args.CancelTriggerPath != null && File.Exists(args.CancelTriggerPath)) {
				var monitor = Tools.FileSystem.MonitorFile(args.CancelTriggerPath, (changeType, path) => { 
					if (changeType == WatcherChangeTypes.Deleted)
						CancelProgram.Cancel();
				});
				disposables.Add(monitor);
			}

			return await command(args, CancelProgram.Token);
		} catch (TaskCanceledException tce) {
			Console.WriteLine("Cancelled successfully");
			return Constants.ERRORCODE_CANCELLED;
		} catch (Exception error) {
			SystemLog.Exception(error);
			Console.WriteLine($"ERROR: {error.ToDisplayString()}");
			return Constants.ERRORCODE_FAIL;
		}
	}


	/// <summary>
	/// The main entry point for the application.
	/// </summary>
	[STAThread]
	public static async Task<int> Main(string[] args) {
#if DEBUG
		string[] InitCmd = new[] { "init", "-k", "YOUR_NOTION_API_KEY_HERE" };
		string[] InitPublishingCmd = new[] { "init", "-k", "YOUR_NOTION_API_KEY_HERE", "-x", "publishing" };
		string[] InitWebhostingCmd = new[] { "init", "-k", "YOUR_NOTION_API_KEY_HERE", "-x", "webhosting" };
		string[] InitWebhostingEmbeddedCmd = new[] { "init", "-k", "YOUR_NOTION_API_KEY_HERE", "-x", "webhosting", "-t", "embedded" };
		string[] SyncCmd = new[] { "sync", "-o", "68e1d4d0-a9a0-43cf-a0dd-6a7ef877d5ec" };
		string[] PullCmd = new[] { "pull", "-o", "68e1d4d0-a9a0-43cf-a0dd-6a7ef877d5ec" };
		string[] PullForceCmd = new[] { "pull", "-o", "68e1d4d0-a9a0-43cf-a0dd-6a7ef877d5ec", "--force" };
		string[] PullBug1Cmd = new[] { "pull", "-o", "b31d9c97-524e-4646-8160-e6ef7f2a1ac1" };
		string[] PullBug2Cmd = new[] { "pull", "-o", "bffe3340-e269-4f2a-9587-e793b70f5c3d", "--force" };
		string[] PullBug3Cmd = new[] { "pull", "-o", "68944996-582b-453f-994f-d5562f4a6730", "--force" };
		string[] PullBug4Cmd = new[] { "pull", "-o", "a2a2a4f0-d13e-4cb0-8f13-dc33402651f5", "--force" };
		string[] PullBug5Cmd = new[] { "pull", "-o", "20e3c6f6-c91a-4d68-932e-00a463eb1654", "--force" };
		string[] PullSP10Cmd = new[] { "pull", "-o", "784082f3-5b8e-402a-b40e-149108da72f3" };
		string[] PullPage = new[] { "pull", "-o", "bffe3340-e269-4f2a-9587-e793b70f5c3d" };
		string[] PullPageForce = new[] { "pull", "-o", "bffe3340-e269-4f2a-9587-e793b70f5c3d", "--force" };
		string[] RenderPage = new[] { "render", "-o", "bffe3340-e269-4f2a-9587-e793b70f5c3d" };
		string[] RenderBug1Page = new[] { "render", "-o", "21d2c360-daaa-4787-896c-fb06354cd74a" };
		string[] RenderBug2Page = new[] { "render", "-o", "68944996-582b-453f-994f-d5562f4a6730" };
		string[] RenderBug3Page = new[] { "render", "-o", "913c5853-d37a-433a-bd2b-7b5bfc5f5754" };
		string[] RenderAllPage = new[] { "render", "--all" };
		string[] RenderEmbeddedPage = new[] { "render", "-o", "68944996-582b-453f-994f-d5562f4a6730" };
		string[] Remove = new[] { "remove", "--all" };
		string[] HelpInit = new[] { "help", "init" };
		string[] Version = new[] { "version" };
		string[] ListWithTrigger = new[] { "list", "--all", "--cancel-trigger", "d:\\temp\\test.txt" };
		string[] List = new[] { "list", "-o", "68e1d4d0-a9a0-43cf-a0dd-6a7ef877d5ec", "--all" };

		if (args.Length == 0)
			args = RenderBug3Page;
#endif

		try {
			if (DateTime.Now > DateTime.Parse("2022-10-23 00:00")) {
				Console.WriteLine("Software has expired");
				return Constants.ERRORCODE_LICENSE_ERROR;
			}

			HydrogenFramework.Instance.StartFramework();
			
			Console.CancelKeyPress += (sender, args) => {
				Console.WriteLine("Cancelling");
				args.Cancel = true;
				CancelProgram.Cancel();
			};

			return await Parser.Default.ParseArguments< 
				StatusRepositoryCommandArguments,
				InitRepositoryCommandArguments,
				RemoveRepositoryCommandArguments,
				ListContentsCommandArguments,
				SyncRepositoryCommandArguments,
				PullRepositoryCommandArguments,
				RenderCommandArguments,
				PruneCommandArguments,
				LicenseCommandArguments,
				int
			>(args).MapResult(
				(StatusRepositoryCommandArguments commandArgs) => ExecuteCommandAsync(commandArgs, ExecuteStatusCommandAsync),
				(InitRepositoryCommandArguments commandArgs) => ExecuteCommandAsync(commandArgs, ExecuteInitCommandAsync),
				(RemoveRepositoryCommandArguments commandArgs) => ExecuteCommandAsync(commandArgs, ExecuteRemoveCommandAsync),
				(ListContentsCommandArguments commandArgs) => ExecuteCommandAsync(commandArgs, ExecuteListCommand),
				(SyncRepositoryCommandArguments commandArgs) => ExecuteCommandAsync(commandArgs, ExecuteSyncCommandAsync),
				(PullRepositoryCommandArguments commandArgs) => ExecuteCommandAsync(commandArgs, ExecutePullCommandAsync),
				(RenderCommandArguments commandArgs) => ExecuteCommandAsync(commandArgs, ExecuteRenderCommandAsync),
				(PruneCommandArguments commandArgs) => ExecuteCommandAsync(commandArgs, ExecutePruneCommandAsync),
				(LicenseCommandArguments commandArgs) => ExecuteCommandAsync(commandArgs, ExecuteLicenseCommandAsync),
				ProcessCommandLineErrorsAsync
			);
		} finally {
			if (HydrogenFramework.Instance.IsStarted)
				HydrogenFramework.Instance.EndFramework();
		}
	}

}