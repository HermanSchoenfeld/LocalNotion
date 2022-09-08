using Hydrogen;
using Hydrogen.Application;
using Hydrogen.Data;
using Notion.Client;
using LocalNotion;
using CommandLine;
using System.IO;
using System.Runtime.Serialization;
using LocalNotion.Core;
using System.Drawing;
using System.Security.AccessControl;

namespace LocalNotion.CLI;

public static partial class Program {
	public const int ERRORCODE_OK = 0;
	public const int ERRORCODE_COMMANDLINE_ERROR = -1;
	public const int ERRORCODE_REPO_NOT_FOUND = -2;
	public const int ERRORCODE_REPO_ERROR = -3;
	public const int ERRORCODE_REPO_NO_APIKEY = -4;
	public const int ERRORCODE_NOT_IMPLEMENTED = -5;
	public const int ERRORCODE_LICENSE_ERROR = -6;
	public const int ERRORCODE_FAIL = -7;
	
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

	public enum LocalNotionProfileDescriptor {
		[EnumMember(Value = "backup")]
		Backup,

		[EnumMember(Value = "ebook")]
		EBook,

		[EnumMember(Value = "website")]
		Website,

	}

	
	[Verb("status", HelpText = "Provides status of the Local Notion repository")]
	public class StatusRepositoryCommandArguments {

		[Option('p', "path", HelpText = "Path to Local Notion repository")]
		public string Path { get; set; } = GetDefaultRepoFolder();

		
		[Option('v', "verbose", HelpText = $"Display debug information in console output")]
		public bool Verbose { get; set; } = false;
	}

	
	[Verb("init", HelpText = "Creates a Local Notion repository")]
	public class InitRepositoryCommandArguments {

		[Option('p', "path", HelpText = "Path to Local Notion repository")]
		public string Path { get; set; } = GetDefaultRepoFolder();

		[Option('k', "key", HelpText = "Notion API key to use when contacting notion (do not pass in low security environment)")]
		public string APIKey { get; set; } = null;

		[Option('l', "log-level", Default = LogLevel.Info, HelpText = $"Logging level in log files (Debug, Info, Warning, Error)")]
		public LogLevel LogLevel { get; set; }

		[Option('x', "profile", Default = LocalNotionProfileDescriptor.Backup, HelpText = $"Determines how to organizes files and generate links in your repository (options are \"backup\", \"ebook\", \"website\")")]
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

	
	[Verb("remove", HelpText = "Removes a Local Notion repository")]
	public class RemoveRepositoryCommandArguments {

		[Option('r', "path", HelpText = "Path to Local Notion repository (default is current working dir)")]
		public string Path { get; set; } = GetDefaultRepoFolder();

		[Option("confirm", Required = true, HelpText = "Mandatory option required to avoid accidental user removal")]
		public bool Confirm { get; set; }

		[Option('v', "verbose", HelpText = $"Display debug information in console output")]
		public bool Verbose { get; set; } = false;

	}

	[Verb("pull", HelpText = "Pulls Notion objects into a Local Notion repository")]
	public class PullRepositoryCommandArguments {

		[Option('o', "objects", HelpText = "List of Notion objects to pull (i.e. pages, databases, workspaces)")]
		public IEnumerable<string> Objects { get; set; } = null;
		
		[Option('k', "key", HelpText = "Notion API key to use (overrides repository key if any)")]
		public string APIKey { get; set; } = null;

		[Option('p', "path", HelpText = "Path to Local Notion repository (default is current working dir)")]
		public string Path { get; set; } = GetDefaultRepoFolder();
			
		[Option('d', "last-updated-on-property", HelpText = "Property on which has a 'Last Edited Time' value")]
		public string FilterLastUpdatedOnPropertyName { get; set; } = null;

		[Option('u', "last-updated-on", HelpText = "Filters objects not edited on or after this date")]
		public DateTime? FilterLastUpdatedOn { get; set; } = null;

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
	public class SyncRepositoryCommandArguments : PullRepositoryCommandArguments{

		[Option('f', "poll-frequency", Default = 30, HelpText = "How often to poll Notion for changes")]
		public int PollFrequency { get; set; } 

	}
			
	[Verb("render", HelpText = "Renders a Local Notion object (using local state only)")]
	public class RenderCommandArguments {

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
	public class PruneCommandArguments {

		[Option('r', "path", HelpText = "Path to Local Notion repository (default current working dir)")]
		public string Path { get; set; } = GetDefaultRepoFolder();

		[Option('o', "objects", HelpText = "List of object ID's to keep (i.e. page(s), database(s), workspace)")]
		public IEnumerable<string> Objects { get; set; } = null;

		[Option('v', "verbose", HelpText = $"Display debug information in console output")]
		public bool Verbose { get; set; } = false;

	}

	[Verb("license", HelpText = "Manages Local Notion license")]
	public class LicenseCommandArguments {

		[Option('a', "activate", HelpText = "Local Notion key.")]
		public string ProductKey { get; set; } = string.Empty;

		[Option('v', "verify", Hidden= true)]
		public bool Verify { get; set; } = false;

		[Option('v', "verbose", HelpText = $"Display debug information in console output")]
		public bool Verbose { get; set; } = false;

	}

	public static async Task<int> ExecuteStatusCommand(StatusRepositoryCommandArguments arguments) {
		var consoleLogger = new ConsoleLogger { Options =  arguments.Verbose ? LogOptions.VerboseProfile : LogOptions.UserDisplayProfile };

		if (!Directory.Exists(arguments.Path)) {
			consoleLogger.Error($"Repository not found: {arguments.Path}");
			return ERRORCODE_REPO_NOT_FOUND;
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
		
		return ERRORCODE_OK;
	}

	public static async Task<int> ExecuteInitCommand(InitRepositoryCommandArguments arguments) {
		var consoleLogger = new ConsoleLogger { Options =  arguments.Verbose ? LogOptions.VerboseProfile : LogOptions.UserDisplayProfile };

		arguments.Path = ToFullPath(arguments.Path);
		if (!Directory.Exists(arguments.Path)) 
			throw new DirectoryNotFoundException(arguments.Path);

		var pathProfile = arguments.Profile switch {
			LocalNotionProfileDescriptor.Backup => LocalNotionPathProfile.Backup,
			LocalNotionProfileDescriptor.EBook => LocalNotionPathProfile.PublishingProfile,
			LocalNotionProfileDescriptor.Website => LocalNotionPathProfile.HostingProfile,
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
		return ERRORCODE_OK;
	}

	public static async Task<int> ExecuteRemoveCommand(RemoveRepositoryCommandArguments arguments) {
		var consoleLogger = new ConsoleLogger { Options =  arguments.Verbose ? LogOptions.VerboseProfile : LogOptions.UserDisplayProfile };
		await LocalNotionRepository.Remove(arguments.Path, consoleLogger);
		consoleLogger.Info("Local Notion repository has been removed");
		return ERRORCODE_OK;
	}

	public static async Task<int> ExecutePullCommand(PullRepositoryCommandArguments arguments) {
		var consoleLogger = new ConsoleLogger { Options =  arguments.Verbose ? LogOptions.VerboseProfile : LogOptions.UserDisplayProfile };
		var repo = await LocalNotionRepository.Open(arguments.Path, consoleLogger);

		var apiKey =arguments.APIKey ?? repo.DefaultNotionApiKey;
		if (string.IsNullOrWhiteSpace(apiKey)) {
			consoleLogger.Info("No API key was specified in argument or registered in repository");
			return -1;
		}
		var client = NotionClientFactory.Create(new ClientOptions { AuthToken = apiKey });
		
		var syncOrchestrator = new NotionSyncOrchestrator(client, repo);
		
		foreach (var @obj in arguments.Objects) {
			var objType = await syncOrchestrator.QualifyObject(@obj);
			switch (objType) {
				case null:
					consoleLogger.Info($"Unrecognized object: {@obj}");
					break;
				case LocalNotionResourceType.Database:
					await syncOrchestrator.DownloadDatabasePages(
						@obj, 
						arguments.FilterLastUpdatedOnPropertyName,
						arguments.FilterLastUpdatedOn,
						arguments.Render,
						arguments.RenderOutput,
						arguments.RenderMode,
						arguments.FaultTolerant,
						arguments.Force
					);
					break;
				case LocalNotionResourceType.Page:
					await syncOrchestrator.DownloadPage( @obj, arguments.Render, arguments.RenderOutput, arguments.RenderMode, arguments.FaultTolerant, arguments.Force);
					break;
				default:
					consoleLogger.Info($"Synchronizing objects of type {objType} is not supported yet");
					break;;
			}
		}

		return 0;
	}

	public static async Task<int> ExecuteSyncCommand(SyncRepositoryCommandArguments arguments) {
		// TODO: needs transactional files to avoid corrupting repo
		while(true) {
			await ExecutePullCommand(arguments);
			await Task.Delay(TimeSpan.FromSeconds(arguments.PollFrequency));
			arguments.FilterLastUpdatedOn = DateTime.UtcNow;
		}
		return ERRORCODE_OK;
	}

	public static async Task<int> ExecuteRenderCommand(RenderCommandArguments arguments) {
		var consoleLogger = new ConsoleLogger { Options =  arguments.Verbose ? LogOptions.VerboseProfile : LogOptions.UserDisplayProfile };
		var repo = await LocalNotionRepository.Open(arguments.Path, consoleLogger);
		var renderer = new ResourceRenderer(repo, repo.Logger);
		var toRender = arguments.RenderAll ? repo.Resources.Where(x => x is LocalNotionPage).Select(x => x.ID) : arguments.Objects;
		if (!toRender.Any()) {
			consoleLogger.Warning("Nothing to render");
			return ERRORCODE_OK;
		}

		foreach (var resource in toRender) {
			renderer.RenderLocalResource(resource, arguments.RenderOutput, arguments.RenderMode);
		}
		return ERRORCODE_OK;
	}

	public static async Task<int> ExecutePruneCommand(PruneCommandArguments arguments) {
		var consoleLogger = new ConsoleLogger { Options =  arguments.Verbose ? LogOptions.VerboseProfile : LogOptions.UserDisplayProfile };
		consoleLogger.Warning("Local Notion pruning is not currently implemented");
		return ERRORCODE_NOT_IMPLEMENTED;
	}

	public static async Task<int> ExecuteLicenseCommand(LicenseCommandArguments arguments) {
		SystemLog.Warning("Local Notion DRM is not currently implemented");
		return ERRORCODE_NOT_IMPLEMENTED;
	}

	public static async Task<int> ProcessCommandLineErrors(IEnumerable<Error> errors) {
		System.Threading.Thread.Sleep(200); // give time for output to flush to parent process
		return ERRORCODE_COMMANDLINE_ERROR;
	}

	public static async Task<int> ExecuteCommand<T>(T args, Func<T, Task<int>> command) {
		try {
			return await command(args);
		} catch (Exception error) {
			SystemLog.Exception(error);
			Console.WriteLine($"ERROR: {error.ToDisplayString()}");
			return ERRORCODE_FAIL;
		}
		return ERRORCODE_OK;
	}

	/// <summary>
	/// The main entry point for the application.
	/// </summary>
	[STAThread]
	public static async Task<int> Main(string[] args) {
		string[] InitCmd = new [] { "init", "-k", "YOUR_NOTION_API_KEY_HERE" };
		string[] PullCmd = new[] { "pull", "-o", "68e1d4d0-a9a0-43cf-a0dd-6a7ef877d5ec" };
		string[] PullPage = new[] { "pull", "-o", "bffe3340-e269-4f2a-9587-e793b70f5c3d" };
		string[] RenderPage = new[] { "render", "-o", "bffe3340-e269-4f2a-9587-e793b70f5c3d" };
		string[] RenderEmbeddedPage = new[] { "render", "-o", "68944996-582b-453f-994f-d5562f4a6730" };
		string[] Remove = new [] { "remove", "--confirm" };
		
		if (args.Length == 0)
			args = PullCmd;

		try {
			if (DateTime.Now > DateTime.Parse("2022-09-23 00:00"))  {
				Console.WriteLine("Software has expired");
				return ERRORCODE_LICENSE_ERROR;
			}

			HydrogenFramework.Instance.StartFramework();

			return await Parser.Default.ParseArguments< 
				StatusRepositoryCommandArguments,
				InitRepositoryCommandArguments,
				RemoveRepositoryCommandArguments,
				PullRepositoryCommandArguments,
				SyncRepositoryCommandArguments,
				RenderCommandArguments,
				PruneCommandArguments,
				LicenseCommandArguments,
				int
			>(args).MapResult(
				(StatusRepositoryCommandArguments commandArgs) => ExecuteCommand(commandArgs, ExecuteStatusCommand),
				(InitRepositoryCommandArguments commandArgs) => ExecuteCommand(commandArgs, ExecuteInitCommand),
				(RemoveRepositoryCommandArguments commandArgs) => ExecuteCommand(commandArgs, ExecuteRemoveCommand),
				(PullRepositoryCommandArguments commandArgs) => ExecuteCommand(commandArgs, ExecutePullCommand),
				(SyncRepositoryCommandArguments commandArgs) => ExecuteCommand(commandArgs, ExecuteSyncCommand),
				(RenderCommandArguments commandArgs) => ExecuteCommand(commandArgs, ExecuteRenderCommand),
				(PruneCommandArguments commandArgs) => ExecuteCommand(commandArgs, ExecutePruneCommand),
				(LicenseCommandArguments commandArgs) => ExecuteCommand(commandArgs, ExecuteLicenseCommand),
				ProcessCommandLineErrors
			);
		} finally {
			if (HydrogenFramework.Instance.IsStarted)
				HydrogenFramework.Instance.EndFramework();
		}
	}

}