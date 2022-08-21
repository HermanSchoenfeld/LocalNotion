//using Hydrogen;
//using Hydrogen.Application;
//using Hydrogen.Data;
//using Notion.Client;
//using LocalNotion;
//using static Hydrogen.AMS;
//using Hydrogen.Environment;

//namespace LocalNotion.CLI;

//public static partial class Program {



//	private static CommandLine Parameters = new CommandLine {
//		Header = new[] {
//					"{ProductName} v{ProductVersion}",
//					"{CopyrightNotice}"
//				},

//		Footer = new[] {
//					"Credits: {AuthorName} <{AuthorEmail}>",
//					"Url: {ProductUrl}"
//				},

//		Root = new CommandLineParameter {
					
					
//					new("create", "ExecuteCreateCommand Local Notion repository",
//						new CommandLineParameter[] {
//							new("key", "Notion API key", CommandLineParameterOptions.Mandatory | CommandLineParameterOptions.RequiresValue),
//							new("path", "Absolute path to Local Notion repository (default is current working folder)", CommandLineParameterOptions.Optional | CommandLineParameterOptions.RequiresValue),
//							new("mode", $"Sets the Local Notion mode. Use \"offline\" for filesystem, use \"online\" for browsing content via a web server.", CommandLineParameterOptions.Optional | CommandLineParameterOptions.RequiresValue),
//							new("base-url", $"Base Url pre-pended to generated content links (default is \"/\")", CommandLineParameterOptions.Optional | CommandLineParameterOptions.RequiresValue),
//							new("objects-path", $"Absolute path to directory which stores Notion objects", CommandLineParameterOptions.Optional | CommandLineParameterOptions.RequiresValue),
//							new("pages-path", $"Absolute path to directory which stores rendered Notion pages", CommandLineParameterOptions.Optional | CommandLineParameterOptions.RequiresValue),
//							new("files-path", $"Absolute path to directory which stores files downloaded from Notion", CommandLineParameterOptions.Optional | CommandLineParameterOptions.RequiresValue),
//							new("templates-path", $"Absolute path to directory which contains the rendering templates used by Local Notion for rendering pages", CommandLineParameterOptions.Optional | CommandLineParameterOptions.RequiresValue),
//							new("log-level", $"Sets the logging level. Options are \"none\", \"debug\", \"info\", \"warning\", \"error\"", CommandLineParameterOptions.Optional | CommandLineParameterOptions.RequiresValue),
//							new("log-path", $"Absolute path to directory which contains the rendering templates used by Local Notion for rendering pages", CommandLineParameterOptions.Optional | CommandLineParameterOptions.RequiresValue),
//						}
//					),

//					new("sync", "Synchronizes a Local Notion repository with Notion",
//						new CommandLineParameter[] {
//							new("key", "Notion API key", CommandLineParameterOptions.Mandatory | CommandLineParameterOptions.RequiresValue),
//							new("path", "Absolute path to Local Notion repository (default is current working folder)", CommandLineParameterOptions.Optional | CommandLineParameterOptions.RequiresValue),
//							new("workspace", "Notion workspace to synchronize", CommandLineParameterOptions.Optional | CommandLineParameterOptions.RequiresValue),
//							new("db", "Notion database to synchronize", CommandLineParameterOptions.Optional | CommandLineParameterOptions.RequiresValue),
//							new("page", "Notion page to synchronize", CommandLineParameterOptions.Optional | CommandLineParameterOptions.RequiresValue),
//							new("cms", "Notion CMS database to synchronize", CommandLineParameterOptions.Optional | CommandLineParameterOptions.RequiresValue),
//							new("filter-source", "Only CMS pages with this source", CommandLineParameterOptions.Optional | CommandLineParameterOptions.RequiresValue),
//							new("filter-root", "Only CMS pages with this root", CommandLineParameterOptions.Optional | CommandLineParameterOptions.RequiresValue),
//							new("filter-updated-on", "Only pages updated on or after this date", CommandLineParameterOptions.Optional | CommandLineParameterOptions.RequiresValue),
//							new("fault-tolerant", "Will continue processing if a failure occurs (options: \"true\" or \"false\" default is \"false\")", CommandLineParameterOptions.Optional | CommandLineParameterOptions.RequiresValue),
//						}
//					),

//					new("render", "Renders Local Notion objects",
//						new CommandLineParameter[] {
//							new("path", "Absolute path to Local Notion repository (default is current working folder)", CommandLineParameterOptions.Optional | CommandLineParameterOptions.RequiresValue),
//							new("db", "Renders the Local Notion database (or CMS database)", CommandLineParameterOptions.Optional | CommandLineParameterOptions.RequiresValue),
//							new("page", "Renders the Local Notion page", CommandLineParameterOptions.Optional | CommandLineParameterOptions.RequiresValue),
//						}
//					),

//					new("prune", "Removes pages and files from Local Notion that are no longer in Notion",
//						new CommandLineParameter[] {
//							new("key", "Notion API key", CommandLineParameterOptions.Mandatory | CommandLineParameterOptions.RequiresValue),
//							new("path", "Absolute path to Local Notion repository (default is current working folder)", CommandLineParameterOptions.Optional | CommandLineParameterOptions.RequiresValue),
//							new("workspace", "Notion workspace of files to keep (everything else removed)", CommandLineParameterOptions.Mandatory | CommandLineParameterOptions.RequiresValue),
//							new("db", "Notion database to keep (everything else removed)", CommandLineParameterOptions.Optional | CommandLineParameterOptions.RequiresValue),
//							new("page", "Notion page to keep (everything else removed)", CommandLineParameterOptions.Optional | CommandLineParameterOptions.RequiresValue),
//							new("cms", "Notion CMS database to keep (everything else removed)", CommandLineParameterOptions.Optional | CommandLineParameterOptions.RequiresValue),
							
//						}
//					),

//					new("license", "Synchronizes with Notion",
//						new CommandLineParameter[] {
//							new("key", "Notion API key", CommandLineParameterOptions.Mandatory | CommandLineParameterOptions.RequiresValue),
//							new("register", "Register a Local Notion license key", CommandLineParameterOptions.Mandatory | CommandLineParameterOptions.RequiresValue),
//							new("verify", "Verifies the registered Local Notion license key", CommandLineParameterOptions.Mandatory | CommandLineParameterOptions.RequiresValue),
//						}
//					),
//				}
//	};

//	public static async Task ExecuteCreateCommand(CommandLineResults createCommand) {
//		var path = createCommand.GetSingleArgumentValueOrDefault<string>("path", Environment.CurrentDirectory);
//		var mode = createCommand.GetSingleArgumentValueOrDefault("mode", LocalNotionMode.Offline);
//		var baseUrl = createCommand.GetSingleArgumentValueOrDefault<string>("base-url");
//		var objectsPath = createCommand.GetSingleArgumentValueOrDefault<string>("objects-path");
//		var pagePath = createCommand.GetSingleArgumentValueOrDefault<string>("pages-path");
//		var filesPath = createCommand.GetSingleArgumentValueOrDefault<string>("files-path");
//		var templatesPath = createCommand.GetSingleArgumentValueOrDefault<string>("templates-path");
//		var logLevel = createCommand.GetSingleArgumentValueOrDefault("log-level", LogLevel.Info);
//		var logPath = createCommand.GetSingleArgumentValueOrDefault<string>("log-path");
//		await LocalNotionRepository.CreateNew(
//			path,
//			mode,
//			baseUrl,
//			objectsPath,
//			pagePath,
//			filesPath,
//			templatesPath,
//			logPath,
//			logLevel
//		);
//		SystemLog.Info("Location Notion repository has been created");
//	}

//	public static async Task ExecuteSyncCommand(CommandLineResults createCommand) {
//		var key = createCommand.GetSingleArgumentValue<string>("key");
//		var path = createCommand.GetSingleArgumentValueOrDefault<string>("path", Environment.CurrentDirectory);
//		var workspace = createCommand.GetSingleArgumentValueOrDefault<string>("workspace");
//		var db = createCommand.GetSingleArgumentValueOrDefault<string>("db");
//		var page = createCommand.GetSingleArgumentValueOrDefault<string>("page");
//		var cms = createCommand.GetSingleArgumentValueOrDefault<string>("cms");
//		var filterSource = createCommand.GetSingleArgumentValueOrDefault<string>("filter-source");
//		var filterRoot = createCommand.GetSingleArgumentValueOrDefault<string>("filter-root");
//		var filterUpdatedOn = createCommand.GetSingleArgumentValueOrDefault<DateTime?>("filter-updated-on", null);
//		var faultTolerant = createCommand.GetSingleArgumentValueOrDefault<bool>("fault-tolerant", false);

//		var client = NotionClientFactory.Create(new ClientOptions { AuthToken = key });
//		var repo = await LocalNotionRepository.Open(path,  SystemLog.Logger);

//		var syncOrchestrator = new NotionSyncOrchestrator(client, repo, SystemLog.Logger);
	
//		if (!string.IsNullOrWhiteSpace(workspace)) {
//			SystemLog.Warning("Synchronizing Notion workspaces is not currently implemented");
//		}

//		if (!string.IsNullOrWhiteSpace(db)) {
//			SystemLog.Warning("Synchronizing Notion databases is not currently implemented");
//		}

//		if (!string.IsNullOrWhiteSpace(page)) {
//			SystemLog.Warning("Synchronizing workspaces is not currently implemented");
//		}

//		if (!string.IsNullOrWhiteSpace(cms)) {
//			SystemLog.Info($"Synchronizing Notion CMS '{cms}' ([filters] source: {filterSource ?? "all"}, root: {filterRoot ?? "all"}, last-updated-on: {filterUpdatedOn:yyyy-MM-dd HH:mm:ss})");
//			await syncOrchestrator.DownloadDatabasePages(cms, filterSource, filterRoot, filterUpdatedOn, faultTolerant);
//		}
//	}

//	public static async Task ExecuteRenderCommand(CommandLineResults createCommand) {
//		var path = createCommand.GetSingleArgumentValueOrDefault<string>("path", Environment.CurrentDirectory);
//		var db = createCommand.GetSingleArgumentValueOrDefault<string>("db");
//		var page = createCommand.GetSingleArgumentValueOrDefault<string>("page");
//		var renderType = createCommand.GetSingleArgumentValueOrDefault<PageRenderType>("type");
//		var renderMode = createCommand.GetSingleArgumentValueOrDefault<RenderMode>("mode");

//		var repo = await LocalNotionRepository.Open(path,  SystemLog.Logger);

//		var renderer = new LocalNotionRenderer(repo,  SystemLog.Logger);

//		if (!string.IsNullOrWhiteSpace(db)) {
//			SystemLog.Warning("Synchronizing Notion databases is not currently implemented");
//		}

//		if (!string.IsNullOrWhiteSpace(page)) {
//			renderer.RenderLocalPage(page, renderType, renderMode);
//		}

//	}

//	public static async Task ExecutePruneCommand(CommandLineResults createCommand) {
//		SystemLog.Warning("Local Notion pruning is not currently implemented");
//	}

//	public static async Task ExecuteLicenseCommand(CommandLineResults createCommand) {
//		SystemLog.Warning("Local Notion DRM is not currently implemented");
//	}

//	/// <summary>
//	/// The main entry point for the application.
//	/// </summary>
//	[STAThread]
//	public static async Task Main(string[] args) {
//		try {
//			HydrogenFramework.Instance.StartFramework();

//			var userArgsResult = Parameters.TryParseArguments(args);
//			if (userArgsResult.Failure) {
//				userArgsResult.ErrorMessages.ForEach(Console.WriteLine);
//				return;
//			}
//			var userArgs = userArgsResult.Value;
//			if (userArgs.HelpRequested) {
//				Parameters.PrintHelp();
//				return;
//			}

//			switch (userArgs.SubCommand?.CommandName.ToUpperInvariant()) {
//				case null:
//				case "":
//					Parameters.PrintHelp();
//					return;
//				case "CREATE":
//					await ExecuteCreateCommand(userArgs.SubCommand);
//					break;
//				case "SYNC":
//					await ExecuteSyncCommand(userArgs.SubCommand);
//					break;
//				case "RENDER":
//					await ExecuteRenderCommand(userArgs.SubCommand);
//					break;
//				case "PRUNE":
//					await ExecutePruneCommand(userArgs.SubCommand);
//					break;
//				case "LICENSE":
//					await ExecuteLicenseCommand(userArgs.SubCommand);
//					break;
//			}
//			Environment.ExitCode = 0;
//		} catch (Exception error) {
//			Console.WriteLine(error.ToDiagnosticString());
//			System.Threading.Thread.Sleep(200); // give time for output to flush to parent process
//			Environment.ExitCode = -1;
//		} finally {
//			if (HydrogenFramework.Instance.IsStarted)
//				HydrogenFramework.Instance.EndFramework();
//		}
//	}


//}
