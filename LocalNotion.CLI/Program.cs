using Hydrogen;
using Hydrogen.Application;
using Hydrogen.Data;
using Notion.Client;
using LocalNotion;
using CommandLine;
using System.IO;

namespace LocalNotion.CLI;

public static partial class Program {
	public const int ERRORCODE_OK = 0;
	public const int ERRORCODE_COMMANDLINE_ERROR = -1;
	public const int ERRORCODE_REPO_NOT_FOUND = -2;
	public const int ERRORCODE_REPO_ERROR = -3;
	public const int ERRORCODE_REPO_NO_APIKEY = -4;
	public const int ERRORCODE_NOT_IMPLEMENTED = -5;
	public const int ERRORCODE_LICENSE_ERROR = -6;
	
	
	[Verb("status", HelpText = "Provides status of the Local Notion repository")]
	public class StatusRepositoryCommandArguments {
		[Option('p', "path", HelpText = "Path to Local Notion repository")]
		public string Path { get; set; } = System.IO.Path.Combine(Environment.CurrentDirectory, Constants.DefaultRegistryFolder,  Constants.DefaultRepositoryFilename);

		
		[Option('v', "verbose", HelpText = $"Display debug information in console output")]
		public bool Verbose { get; set; } = false;
	}

	
	[Verb("init", HelpText = "Creates a Local Notion repository")]
	public class InitRepositoryCommandArguments {

		[Option('p', "path", HelpText = "Path to Local Notion repository")]
		public string Path { get; set; } = System.IO.Path.Combine(Environment.CurrentDirectory, Constants.DefaultRegistryFolder,  Constants.DefaultRepositoryFilename);

		[Option('k', "key", HelpText = "Notion API key to use when contacting notion (do not pass in low security environment)")]
		public string APIKey { get; set; } = null;

		[Option('m', "mode", HelpText = "Sets the Local Notion mode. Use \"offline\" for filesystem, use \"online\" for browsing content via a web server")]
		public LocalNotionMode Mode { get; set; }

		[Option('u', "base-url", HelpText = $"Base Url pre-pended to generated content links (default is \"/\")")]
		public string BaseUrl { get; set; } = "/";

		[Option('o', "objects-path", HelpText = $"Path to directory which stores Notion objects")]
		public string ObjectsPath { get; set; } = null;

		[Option('o', "pages-path", HelpText = $"Path to directory that stores rendered Notion pages")]
		public string PagesPath { get; set; } = null;

		[Option('o', "files-path", HelpText = $"Path to directory that stores mirrored Notion files")]
		public string FilesPath { get; set; } = null;

		[Option('t', "themes-path", HelpText = $"Path to directory that stores Local Notion rendering themes")]
		public string ThemesPath { get; set; } = null;

		[Option('l', "logs-path", HelpText = $"Path to directory that stores mirrored Notion files")]
		public string LogsPath { get; set; } = null;

		[Option('x', "log-level", Default = LogLevel.Info, HelpText = $"Logging level in log files (Debug, Info, Warning, Error)")]
		public LogLevel LogLevel { get; set; }

		[Option('v', "verbose", HelpText = $"Display debug information in console output")]
		public bool Verbose { get; set; } = false;
	
	}

	[Verb("remove", HelpText = "Removes a Local Notion repository")]
	public class RemoveRepositoryCommandArguments {

		[Option('r', "repo-path", HelpText = "Path to Local Notion repository (default current working dir)")]
		public string Path { get; set; } = System.IO.Path.Combine(Environment.CurrentDirectory, Constants.DefaultRegistryFolder, Constants.DefaultRepositoryFilename);

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

		[Option('p', "path", HelpText = "Path to Local Notion repository")]
		public string Path { get; set; } = System.IO.Path.Combine(Environment.CurrentDirectory, Constants.DefaultRegistryFolder, Constants.DefaultRepositoryFilename);

		[Option('u', "updated-on", HelpText = "Ignore objects updated before this date")]
		public DateTime? FilterLastUpdatedOn { get; set; } = null;

		//[Option("filter-source", HelpText = "Only sync CMS pages whose 'Source' property matches at least one from this list.")]
		//public IEnumerable<string> FilterSources { get; set; } = Array.Empty<string>();

		//[Option("filter-root", HelpText = "Only sync CMS pages whose 'Root' property matches at least one from this list.")]
		//public IEnumerable<string> FilterRoots { get; set; } = Array.Empty<string>();

		[Option("render", Default = (bool)true, HelpText = "Renders objects after pull")]
		public bool Render { get; set; }

		[Option("render-output", Default = RenderOutput.HTML, HelpText = "Type of rendering to use (HTML, PDF)")]
		public RenderOutput RenderOutput { get; set; }

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

		[Option('r', "repo-path", HelpText = "Path to Local Notion repository.")]
		public string Path { get; set; } = System.IO.Path.Combine(Environment.CurrentDirectory, Constants.DefaultRegistryFolder, Constants.DefaultRepositoryFilename);

		[Option('o', "objects", Required = false, HelpText = "List of object ID's to render (i.e. page(s), database(s), workspace)")]
		public IEnumerable<string> Objects { get; set; } = null;

		[Option('a', "all", HelpText = "Renders all objects in repository")]
		public bool RenderAll { get; set; }

		[Option("render-output", Default = RenderOutput.HTML, HelpText = "Type of rendering to use (HTML, PDF)")]
		public RenderOutput RenderOutput { get; set; }

		[Option("render-mode", Default = RenderMode.ReadOnly, HelpText = "Rendering mode for objects (ReadOnly, Editable)")]
		public RenderMode RenderMode { get; set; }

		[Option("fault-tolerant", Default = (bool)true, HelpText = "Continues processing on failures")]
		public bool FaultTolerant { get; set; }

		[Option('v', "verbose", HelpText = $"Display debug information in console output")]
		public bool Verbose { get; set; } = false;

	}


	[Verb("prune", HelpText = "Removes objects from a Local Notion that no longer exist in Notion")]
	public class PruneCommandArguments {

		[Option('r', "repo-path", HelpText = "Path to Local Notion repository (default current working dir)")]
		public string Path { get; set; } = System.IO.Path.Combine(Environment.CurrentDirectory, Constants.DefaultRegistryFolder, Constants.DefaultRepositoryFilename);

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
		var consoleLogger = new ConsoleLogger { Options = (arguments.Verbose ? LogLevel.Debug : LogLevel.Info).ToLogOptions() };

		if (!File.Exists(arguments.Path)) {
			consoleLogger.Error($"Repository not found: {arguments.Path}");
			return ERRORCODE_REPO_NOT_FOUND;
		}

		var repo = await LocalNotionRepository.Open(arguments.Path, consoleLogger);
		consoleLogger.Info("Location Notion repository has been created");
		return ERRORCODE_OK;

	}

	public static async Task<int> ExecuteInitCommand(InitRepositoryCommandArguments arguments) {
		var consoleLogger = new ConsoleLogger { Options = (arguments.Verbose ? LogLevel.Debug : LogLevel.Info).ToLogOptions() };
		await LocalNotionRepository.CreateNew(
			arguments.Path,
			arguments.APIKey,
			arguments.ObjectsPath,
			arguments.PagesPath,
			arguments.FilesPath,
			arguments.ThemesPath,
			arguments.LogsPath,
			arguments.Mode,
			arguments.BaseUrl,
			arguments.LogLevel,
			logger: consoleLogger
		);
		consoleLogger.Info("Location Notion repository has been created");
		return ERRORCODE_OK;
	}

	public static async Task<int> ExecuteRemoveCommand(RemoveRepositoryCommandArguments arguments) {
		var consoleLogger = new ConsoleLogger() { Options = (arguments.Verbose ? LogLevel.Debug : LogLevel.Info).ToLogOptions() };
		await LocalNotionRepository.Remove(arguments.Path, consoleLogger);
		consoleLogger.Info("Local Notion repository has been removed");
		return ERRORCODE_OK;
	}

	public static async Task<int> ExecutePullCommand(PullRepositoryCommandArguments arguments) {
		var consoleLogger = new ConsoleLogger() { Options = (arguments.Verbose ? LogLevel.Debug : LogLevel.Info).ToLogOptions() };
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
		var consoleLogger = new ConsoleLogger() { Options = (arguments.Verbose ? LogLevel.Debug : LogLevel.Info).ToLogOptions() };
		var repo = await LocalNotionRepository.Open(arguments.Path, consoleLogger);
		var renderer = new LocalNotionRenderer(repo, consoleLogger);
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
		var consoleLogger = new ConsoleLogger() { Options = (arguments.Verbose ? LogLevel.Debug : LogLevel.Info).ToLogOptions() };
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

	/// <summary>
	/// The main entry point for the application.
	/// </summary>
	[STAThread]
	public static async Task<int> Main(string[] args) {
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
				(StatusRepositoryCommandArguments commandArgs) => ExecuteStatusCommand(commandArgs),
				(InitRepositoryCommandArguments commandArgs) => ExecuteInitCommand(commandArgs),
				(RemoveRepositoryCommandArguments commandArgs) => ExecuteRemoveCommand(commandArgs),
				(PullRepositoryCommandArguments commandArgs) => ExecutePullCommand(commandArgs),
				(SyncRepositoryCommandArguments commandArgs) => ExecuteSyncCommand(commandArgs),
				(RenderCommandArguments commandArgs) => ExecuteRenderCommand(commandArgs),
				(PruneCommandArguments commandArgs) => ExecutePruneCommand(commandArgs),
				(LicenseCommandArguments commandArgs) => ExecuteLicenseCommand(commandArgs),
				ProcessCommandLineErrors
			);
		} finally {
			if (HydrogenFramework.Instance.IsStarted)
				HydrogenFramework.Instance.EndFramework();
		}
	}


}

