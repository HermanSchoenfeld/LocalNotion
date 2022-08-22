using Hydrogen;
using Hydrogen.Application;
using Hydrogen.Data;
using Notion.Client;
using LocalNotion;
using CommandLine;
using System.IO;

namespace LocalNotion.CLI;

public static partial class Program {
	
	[Verb("init", HelpText = "Creates a Local Notion repository")]
	public class InitRepositoryCommandArguments {

		[Option('p', "path", HelpText = "Path to Local Notion repository")]
		public string Path { get; set; } = System.IO.Path.Combine(Environment.CurrentDirectory, Constants.DefaultRepositoryFilename);

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
		public string Path { get; set; } = System.IO.Path.Combine(Environment.CurrentDirectory, Constants.DefaultRepositoryFilename);

		[Option("confirm", Required = true, HelpText = "Mandatory option required to avoid accidental user removal")]
		public bool Confirm { get; set; }

		[Option('v', "verbose", HelpText = $"Display debug information in console output")]
		public bool Verbose { get; set; } = false;

	}

	[Verb("pull", HelpText = "Pulls Notion objects into a Local Notion repository")]
	public class PullRepositoryCommandArguments {

		[Option('o', "objects", Required = true, HelpText = "List of Notion objects to pull (i.e. pages, databases, workspaces)")]
		public IEnumerable<string> Objects { get; set; } = null;
		
		[Option('k', "key", HelpText = "Notion API key to use (overrides repository key if any)")]
		public string APIKey { get; set; } = null;

		[Option('p', "path", HelpText = "Path to Local Notion repository")]
		public string Path { get; set; } = System.IO.Path.Combine(Environment.CurrentDirectory, Constants.DefaultRepositoryFilename);

		[Option('u', "updated-on", HelpText = "Ignore objects updated before this date")]
		public DateTime? FilterLastUpdatedOn { get; set; } = null;

		//[Option("filter-source", HelpText = "Only sync CMS pages whose 'Source' property matches at least one from this list.")]
		//public IEnumerable<string> FilterSources { get; set; } = Array.Empty<string>();

		//[Option("filter-root", HelpText = "Only sync CMS pages whose 'Root' property matches at least one from this list.")]
		//public IEnumerable<string> FilterRoots { get; set; } = Array.Empty<string>();


		[Option("render", Default = (bool)true, HelpText = "Renders objects after pull")]
		public bool Render { get; set; }

		[Option("render-type", Default = PageRenderType.HTML, HelpText = "Type of rendering to use (HTML, PDF)")]
		public PageRenderType RenderType { get; set; }

		[Option("render-mode", Default = RenderMode.ReadOnly, HelpText = "Rendering mode for objects (ReadOnly, Editable)")]
		public RenderMode RenderMode { get; set; }

		[Option("fault-tolerant", Default = (bool)true, HelpText = "Continues processing on failures")]
		public bool FaultTolerant { get; set; }

		[Option("force", HelpText = "Forces downloading of objects even if unchanged")]
		public bool Force { get; set; } = false;



		[Option('v', "verbose", HelpText = $"Display debug information in console output")]
		public bool Verbose { get; set; } = false;


	}

	[Verb("prune", HelpText = "Removes from a Local Notion repository databases, pages and files that are no longer in Notion")]
	public class PruneCommandArguments {

		[Option('r', "repo-path", HelpText = "Path to Local Notion repository (default current working dir)")]
		public string Path { get; set; } = System.IO.Path.Combine(Environment.CurrentDirectory, Constants.DefaultRepositoryFilename);

		[Option('o', "objects", HelpText = "List of object ID's to keep (i.e. page(s), database(s), workspace)")]
		public string[] ObjectID { get; set; } = null;

		[Option('v', "verbose", HelpText = $"Display debug information in console output")]
		public bool Verbose { get; set; } = false;


		// TODO: add usages

	}
			
	[Verb("render", HelpText = "Renders a Local Notion object using it's local state only")]
	public class RenderCommandArguments {

		[Option('r', "repo-path", HelpText = "Path to Local Notion repository.")]
		public string Path { get; set; } = System.IO.Path.Combine(Environment.CurrentDirectory, Constants.DefaultRepositoryFilename);

		[Option('o', "objects", HelpText = "List of object ID's to render (i.e. page(s), database(s), workspace)")]
		public string[] ObjectID { get; set; } = null;

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

	public static async Task<int> ExecuteInitCommand(InitRepositoryCommandArguments arguments) {
		var consoleLogger = new ConsoleLogger() { Options = (arguments.Verbose ? LogLevel.Debug : LogLevel.Info).ToLogOptions() };
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
		return 0;
	}

	public static async Task<int> ExecuteRemoveCommand(RemoveRepositoryCommandArguments arguments) {
		var consoleLogger = new ConsoleLogger() { Options = (arguments.Verbose ? LogLevel.Debug : LogLevel.Info).ToLogOptions() };
		await LocalNotionRepository.Remove(arguments.Path, consoleLogger);
		consoleLogger.Info("Local Notion repository has been removed");
		return 0;
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
						arguments.RenderType,
						arguments.RenderMode,
						arguments.FaultTolerant,
						arguments.Force
					);
					break;
				case LocalNotionResourceType.Page:
					await syncOrchestrator.DownloadPage( @obj, arguments.Force );
					break;
				default:
					consoleLogger.Info($"Synchronizing objects of type {objType} is not supported yet");
					break;;
			}
		}

		return 0;
	}

	public static async Task<int> ExecuteRenderCommand(RenderCommandArguments arguments) {
		return 0;
		//var path = createCommand.GetSingleArgumentValueOrDefault<string>("path", Environment.CurrentDirectory);
		//var db = createCommand.GetSingleArgumentValueOrDefault<string>("db");
		//var page = createCommand.GetSingleArgumentValueOrDefault<string>("page");
		//var renderType = createCommand.GetSingleArgumentValueOrDefault<PageRenderType>("type");
		//var renderMode = createCommand.GetSingleArgumentValueOrDefault<RenderMode>("mode");

		//var repo = await LocalNotionRepository.Open(path, SystemLog.Logger);

		//var renderer = new LocalNotionRenderer(repo, SystemLog.Logger);

		//if (!string.IsNullOrWhiteSpace(db)) {
		//	SystemLog.Warning("Synchronizing Notion databases is not currently implemented");
		//}

		//if (!string.IsNullOrWhiteSpace(page)) {
		//	renderer.RenderLocalPage(page, renderType, renderMode);
		//}

	}

	public static async Task<int> ExecutePruneCommand(PruneCommandArguments arguments) {
		var consoleLogger = new ConsoleLogger() { Options = (arguments.Verbose ? LogLevel.Debug : LogLevel.Info).ToLogOptions() };
		consoleLogger.Warning("Local Notion pruning is not currently implemented");
		return 1;
	}

	public static async Task<int> ExecuteLicenseCommand(LicenseCommandArguments arguments) {
		SystemLog.Warning("Local Notion DRM is not currently implemented");
		return 1;
	}

	public static async Task<int> ProcessCommandLineErrors(IEnumerable<Error> errors) {
		System.Threading.Thread.Sleep(200); // give time for output to flush to parent process
		return 1;
	}

	/// <summary>
	/// The main entry point for the application.
	/// </summary>
	[STAThread]
	public static async Task<int> Main(string[] args) {
		try {
			HydrogenFramework.Instance.StartFramework();

			return await Parser.Default.ParseArguments<
				InitRepositoryCommandArguments,
				RemoveRepositoryCommandArguments,
				PullRepositoryCommandArguments,
				RenderCommandArguments,
				PruneCommandArguments,
				LicenseCommandArguments,
				int
			>(args).MapResult(
				(InitRepositoryCommandArguments commandArgs) => ExecuteInitCommand(commandArgs),
				(RemoveRepositoryCommandArguments commandArgs) => ExecuteRemoveCommand(commandArgs),
				(PullRepositoryCommandArguments commandArgs) => ExecutePullCommand(commandArgs),
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
