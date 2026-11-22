// Copyright (c) Herman Schoenfeld 2018 - Present. All rights reserved. (https://sphere10.com/products/localnotion)
// Author: Herman Schoenfeld <herman@sphere10.com>
//
// Distributed under the GPLv3 software license, see the accompanying file LICENSE 
// or visit https://github.com/HermanSchoenfeld/localnotion/blob/master/LICENSE
//
// This notice must not be removed when duplicating this file or its contents, in whole or in part.

using Sphere10.Framework;
using Sphere10.Framework.Application;
using Notion.Client;
using CommandLine;
using System.Runtime.Serialization;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using LocalNotion.Core;
using Newtonsoft.Json.Linq;


namespace LocalNotion.CLI;

public static partial class Program {


	private const string LinuxNGinxReloadCommand = "systemctl reload nginx";
	private const string WinNGinxReloadCommand = "nginx -s reload";

	private static CancellationTokenSource CancelProgram { get; } = new CancellationTokenSource();

	public static async Task<int> ExecuteStatusCommandAsync(StatusRepositoryCommandArguments arguments, CancellationToken cancellationToken) {
		var consoleLogger = new ConsoleLogger { Options = arguments.Verbose ? LogOptions.VerboseProfile : LogOptions.UserDisplayProfile };

		if (!Directory.Exists(arguments.Path)) {
			consoleLogger.Error($"Repository not found: {arguments.Path}");
			return Constants.ERRORCODE_REPO_NOT_FOUND;
		}

		await using var repo = await OpenRepository(arguments.Path, consoleLogger);
		System.Console.WriteLine(
$@"Local Notion Status:
	Total Resources: {repo.Resources.Count()}
	Total Objects: {repo.Objects.Count()}
	Total Graphs: {repo.Graphs.Count()}");

		Console.WriteLine();
		Console.WriteLine($"\tLocal Notion Resource Paths (relative):");
		foreach (var resourceType in Enum.GetValues<LocalNotionResourceType>())
			Console.WriteLine($"\t\t{resourceType}: {repo.Paths.GetResourceTypeFolderPath(resourceType, FileSystemPathType.Relative)}");
		Console.WriteLine();
		Console.WriteLine($"\tInternal Resource Paths (relative):");
		Console.WriteLine($"\t\tRegistry: {repo.Paths.GetRegistryFilePath(FileSystemPathType.Relative)}");
		foreach (var internalResourceType in Enum.GetValues<InternalResourceType>())
			Console.WriteLine($"\t\t{internalResourceType}: {repo.Paths.GetInternalResourceFolderPath(internalResourceType, FileSystemPathType.Relative)}");

		return Constants.ERRORCODE_OK;
	}

	public static async Task<int> ExecuteGitCommandAsync(GitRepositoryCommandArguments arguments, CancellationToken cancellationToken) {
		Guard.ArgumentNotNull(arguments, nameof(arguments));
		// One-value sequence options require explicit values; the parser treats bool options as valueless switches.
		bool? enabled = null;
		bool? push = null;
		var enabledValues = arguments.Enabled?.ToArray() ?? Array.Empty<string>();
		var pushValues = arguments.Push?.ToArray() ?? Array.Empty<string>();
		if (enabledValues.Length > 0) {
			if (enabledValues.Length != 1 || !bool.TryParse(enabledValues[0], out var value)) {
				Console.Error.WriteLine("Provide --enabled with true or false.");
				return Constants.ERRORCODE_COMMANDLINE_ERROR;
			}
			enabled = value;
		}
		if (pushValues.Length > 0) {
			if (pushValues.Length != 1 || !bool.TryParse(pushValues[0], out var value)) {
				Console.Error.WriteLine("Provide --push with true or false.");
				return Constants.ERRORCODE_COMMANDLINE_ERROR;
			}
			push = value;
		}
		try {
			cancellationToken.ThrowIfCancellationRequested();
			await using var repo = await OpenRepository(arguments.Path);
			cancellationToken.ThrowIfCancellationRequested();
			repo.UpdateGitSettings(enabled, push);
			await repo.SaveAsync();
			var settings = repo.GitSettings;
			Console.WriteLine($"{{\"git\":{{\"enabled\":{settings.Enabled.ToString().ToLowerInvariant()},\"push\":{settings.Push.ToString().ToLowerInvariant()}}}}}");
			return Constants.ERRORCODE_OK;
		} catch (OperationCanceledException) {
			Console.Error.WriteLine("Git configuration cancelled. No Git operation was run.");
			return Constants.ERRORCODE_CANCELLED;
		} catch (FileNotFoundException) {
			Console.Error.WriteLine("Repository not found: select a folder containing .localnotion/registry.json with --path.");
			return Constants.ERRORCODE_REPO_NOT_FOUND;
		} catch (DirectoryNotFoundException) {
			Console.Error.WriteLine("Repository not found: select a folder containing .localnotion/registry.json with --path.");
			return Constants.ERRORCODE_REPO_NOT_FOUND;
		} catch (Exception error) when (error is Newtonsoft.Json.JsonException or InvalidDataException or DecoderFallbackException) {
			Console.Error.WriteLine("Unable to read Git settings: the registry must contain a valid Local Notion repository configuration.");
			return Constants.ERRORCODE_REPO_ERROR;
		} catch (Exception error) when (error is IOException or UnauthorizedAccessException) {
			Console.Error.WriteLine("Unable to update or read Git settings. Check registry permissions and stop other repository writers before retrying.");
			return Constants.ERRORCODE_REPO_ERROR;
		} catch (ArgumentException) {
			Console.Error.WriteLine("Provide --path with a valid Local Notion repository folder.");
			return Constants.ERRORCODE_COMMANDLINE_ERROR;
		} catch (InvalidOperationException) {
			Console.Error.WriteLine("Unable to open or save the Local Notion repository. Check its configuration and stop other repository writers before retrying.");
			return Constants.ERRORCODE_REPO_ERROR;
		}
	}

	public static async Task<int> ExecuteRedirectsCommandAsync(RedirectsCommandArguments arguments, CancellationToken cancellationToken) {
		Guard.ArgumentNotNull(arguments, nameof(arguments));
		var add = arguments.Add?.ToArray() ?? Array.Empty<string>();
		if (add.Length is not (0 or 2) || arguments.Soft && add.Length == 0) {
			Console.Error.WriteLine("Provide --add with the old and the new address; --soft applies only to --add.");
			return Constants.ERRORCODE_COMMANDLINE_ERROR;
		}
		try {
			cancellationToken.ThrowIfCancellationRequested();
			await using var repo = await OpenRepository(arguments.Path);
			cancellationToken.ThrowIfCancellationRequested();
			if (!string.IsNullOrWhiteSpace(arguments.Remove) && !repo.RemoveRedirect(arguments.Remove)) {
				Console.Error.WriteLine($"No redirect from '{arguments.Remove}'.");
				return Constants.ERRORCODE_COMMANDLINE_ERROR;
			}
			if (add.Length == 2 && !repo.AddRedirect(add[0], add[1], arguments.Soft ? RedirectType.Soft : RedirectType.Hard)) {
				Console.Error.WriteLine($"No redirect was added: '{add[0]}' is empty, equal to the new address, or still served by a page or file.");
				return Constants.ERRORCODE_COMMANDLINE_ERROR;
			}
			await repo.SaveAsync();
			foreach (var redirect in repo.Redirects.OrderBy(redirect => redirect.From, StringComparer.Ordinal))
				Console.WriteLine($"{redirect.CreatedOn:yyyy-MM-dd} {(redirect.Type == RedirectType.Soft ? 302 : 301)} /{redirect.From} -> /{redirect.To}");
			return Constants.ERRORCODE_OK;
		} catch (OperationCanceledException) {
			Console.Error.WriteLine("Redirect command cancelled. No change was saved.");
			return Constants.ERRORCODE_CANCELLED;
		} catch (Exception error) when (error is FileNotFoundException or DirectoryNotFoundException) {
			Console.Error.WriteLine("Repository not found: select a folder containing .localnotion/registry.json with --path.");
			return Constants.ERRORCODE_REPO_NOT_FOUND;
		} catch (Exception error) when (error is IOException or UnauthorizedAccessException or InvalidOperationException) {
			Console.Error.WriteLine("Unable to open or save the Local Notion repository. Check its permissions and stop other repository writers before retrying.");
			return Constants.ERRORCODE_REPO_ERROR;
		}
	}

	public static Task<int> ExecuteCreateCMSCommandAsync(CreateCMSCommandArguments arguments, CancellationToken cancellationToken)
		=> ExecuteCreateCMSCommandAsync(arguments, cancellationToken, CreateNotionClient);

	public static async Task<int> ExecuteCreateCMSCommandAsync(
		CreateCMSCommandArguments arguments,
		CancellationToken cancellationToken,
		Func<string, INotionClient> clientFactory
	) {
		Guard.ArgumentNotNull(arguments, nameof(arguments));
		Guard.ArgumentNotNull(clientFactory, nameof(clientFactory));
		var consoleLogger = new ConsoleLogger { Options = arguments.Verbose ? LogOptions.VerboseProfile : LogOptions.UserDisplayProfile };
		if (!Guid.TryParse(arguments.Parent, out var parentPageID) || parentPageID == Guid.Empty) {
			consoleLogger.Error("Provide --parent with a valid, non-empty Notion page UUID shared with your integration.");
			return Constants.ERRORCODE_COMMANDLINE_ERROR;
		}
		if (string.IsNullOrWhiteSpace(arguments.Title) || arguments.Title.Length > 2000) {
			consoleLogger.Error("Provide --title with between 1 and 2000 characters.");
			return Constants.ERRORCODE_COMMANDLINE_ERROR;
		}

		var creationRequested = false;
		try {
			cancellationToken.ThrowIfCancellationRequested();
			// Read credentials only. Opening a repository can deploy themes and is unnecessary for creation.
			var apiKey = ResolveNotionApiKey(arguments.APIKey);
			if (string.IsNullOrWhiteSpace(apiKey) && !string.IsNullOrWhiteSpace(arguments.Path)) {
				var registryFile = System.IO.Path.Combine(ToFullPath(arguments.Path), Constants.DefaultRegistryFolderName, Constants.DefaultRegistryFileName);
				if (!File.Exists(registryFile)) {
					consoleLogger.Error("The --path repository has no .localnotion/registry.json. Supply an existing repository or another API key source.");
					return Constants.ERRORCODE_COMMANDLINE_ERROR;
				}
				var registry = JObject.Parse(await File.ReadAllTextAsync(registryFile, cancellationToken));
				apiKey = ResolveNotionApiKey(null, (string)registry["notion_api_key"]);
			}
			if (string.IsNullOrWhiteSpace(apiKey)) {
				consoleLogger.Error("Provide an API key through --key, NOTION_API_KEY_FILE, or --path to an existing repository.");
				return Constants.ERRORCODE_COMMANDLINE_ERROR;
			}

			var client = clientFactory(apiKey);
			creationRequested = true;
			var database = await new CMSDatabaseCreator(client).CreateAsync(parentPageID, arguments.Title, cancellationToken);
			if (database == null || !Guid.TryParse(database.Id, out var databaseID) || databaseID == Guid.Empty) {
				consoleLogger.Error("Notion returned no usable database ID. Check the parent page before retrying; a database may have been created.");
				return Constants.ERRORCODE_FAIL;
			}

			consoleLogger.Info("Blank CMS database verified with 20 properties, all preset Type/Status/Tags/Themes options, and zero active rows.");
			consoleLogger.Info("One temporary Hidden setup row was created and moved to Notion's trash to add presets beyond the API's 100-option request limit.");
			consoleLogger.Info($"Database ID: {databaseID:D}");
			// Construct the URL from the validated ID rather than echoing arbitrary response content.
			consoleLogger.Info($"Database URL: https://www.notion.so/{databaseID:N}");
			consoleLogger.Info("Next: create an empty local folder, then run:");
			consoleLogger.Info($"localnotion init --cms {databaseID:D} --profile website --path <empty-local-folder>");
			consoleLogger.Info("Then configure credentials and create your homepage in Notion before running pull. No local repository was initialized or published.");
			return Constants.ERRORCODE_OK;
		} catch (CMSDatabaseCreationException error) {
			consoleLogger.Error("A CMS database was created, but setup or verification is incomplete. No create request was retried.");
			consoleLogger.Info($"Database ID: {error.DatabaseID:D}");
			consoleLogger.Info($"Database URL: https://www.notion.so/{error.DatabaseID:N}");
			if (error.SetupPageID != Guid.Empty) {
				consoleLogger.Info($"Temporary setup row: https://www.notion.so/{error.SetupPageID:N}");
				consoleLogger.Warning(error.SetupPageArchived ? "The temporary setup row was moved to the trash." : "The temporary Hidden setup row could not be confirmed in the trash; move it there before publishing.");
			} else {
				consoleLogger.Warning("If setup-row creation was interrupted, inspect the database for a temporary Hidden row and move it to the trash.");
			}
			consoleLogger.Warning("Inspect and recover this database before rerunning create-cms, which would create another database. Read-content, insert-content and update-content capabilities are required.");
			return error.InnerException is OperationCanceledException ? Constants.ERRORCODE_CANCELLED : Constants.ERRORCODE_FAIL;
		} catch (OperationCanceledException) {
			consoleLogger.Warning("CMS creation cancelled. Check the parent page before retrying if a request was sent; it may have completed in Notion.");
			return Constants.ERRORCODE_CANCELLED;
		} catch (NotionApiException error) {
			// API messages may echo input. Keep authentication data and raw responses out of output/logs.
			var guidance = (int)error.StatusCode switch {
				401 => "Check the selected integration API key; Notion did not accept it.",
				403 => "Check the integration's read-content, insert-content and update-content capabilities and access to the parent page.",
				404 => "Check the parent page ID and share that page with the integration.",
				429 => "Notion's rate limit was reached. Wait before retrying.",
				_ => "Check the parent page, integration capabilities and compatibility with the CMS schema."
			};
			consoleLogger.Error($"Notion rejected CMS creation (HTTP {(int)error.StatusCode}). {guidance}");
			consoleLogger.Warning("No retry was attempted. Check the parent page before retrying after a server error.");
			return Constants.ERRORCODE_FAIL;
		} catch (IOException) {
			consoleLogger.Error(creationRequested
				? "CMS creation encountered an I/O failure. No retry was attempted. Check the parent page before retrying; a database may have been created."
				: "Unable to read the API-key file or repository registry. Check the configured file and its read permissions.");
			return Constants.ERRORCODE_FAIL;
		} catch (UnauthorizedAccessException) {
			consoleLogger.Error("Unable to read the API-key file or repository registry. Check its read permissions.");
			return Constants.ERRORCODE_FAIL;
		} catch (Newtonsoft.Json.JsonException) {
			consoleLogger.Error("Unable to read the CMS template, repository registry, or Notion response. Check the parent page before retrying if a request was sent.");
			return Constants.ERRORCODE_FAIL;
		} catch (Exception) {
			consoleLogger.Error("CMS creation failed. No retry was attempted. Check connectivity and the parent page before retrying; a database may have been created.");
			return Constants.ERRORCODE_FAIL;
		}
	}

	public enum LocalNotionProfileDescriptor {
		[EnumMember(Value = "backup")]
		Backup,

		[EnumMember(Value = "offline")]
		Offline,

		[EnumMember(Value = "publishing")]
		Publishing,

		[EnumMember(Value = "website")]
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

		[Option('c', "cms", HelpText = "Specifies that this repo will mirror Notion CMS database")]
		public string CMSDatabase { get; set; } = null;

		[Option('l', "log-level", Default = LogLevel.Info, HelpText = $"Logging level in log files (Options: debug, info, warning, error)")]
		public LogLevel LogLevel { get; set; }

		[Option('x', "profile", Default = LocalNotionProfileDescriptor.Backup, HelpText = $"Determines how to organizes files and generate links in your repository (options: backup, offline, publishing, website)")]
		public LocalNotionProfileDescriptor Profile { get; set; }

		[Option('t', "themes", HelpText = $"Built-in or custom theme(s) used for rendering")]
		public IEnumerable<string> Themes { get; set; } = new[] { "default" };

		[Option('v', "verbose", HelpText = $"Display debug information in console output")]
		public bool Verbose { get; set; } = false;

		[Option("git", Default = (bool)false, HelpText = "Enable change tracking via git")]
		public bool EnableGit { get; set; }

		[Option("git-push", Default = (bool)false, HelpText = "Push changes to default git remote/branch when committed")]
		public bool PushRemote { get; set; }

		[Option("nginx", Default = (bool)false, HelpText = "Enable NGINX hosting")]
		public bool EnableNginx { get; set; }

		[Option("nginx-reload-cmd", Default = null, HelpText = "Command line used to reload NGINX web server (executed from the \".localnotion/nginx\" dir)")]
		public string NginxReloadCommand { get; set; }

		[Option("apache", Default = false, HelpText = "Generate .htaccess file for Apache hosting")]
		public bool EnableApache { get; set; }

		[Option("override-objects-path", HelpText = $"Override path where notion objects are stored")]
		public string ObjectsPathOverride { get; set; } = null;

		[Option("override-pages-path", HelpText = $"Override path where rendered pages are stored")]
		public string PagesPathOverride { get; set; } = null;

		[Option("override-db-path", HelpText = $"Override path where rendered databases are stored")]
		public string DatabasePathOverride { get; set; } = null;

		[Option("override-workspace-path", HelpText = $"Override path where rendered workspace pages are stored")]
		public string WorkspacePathOverride { get; set; } = null;

		[Option("override-cms-path", HelpText = $"Override path where rendered cms pages are stored")]
		public string CMSPathOverride { get; set; } = null;

		[Option("override-files-path", HelpText = $"Override path where files are stored")]
		public string FilesPathOverride { get; set; } = null;

		[Option("override-themes-path", HelpText = $"Optional theme override folder (missing files use embedded defaults)")]
		public string ThemesPathOverride { get; set; } = null;

		[Option("override-logs-path", HelpText = $"Override path that stores the log files")]
		public string LogsPathOverride { get; set; } = null;

		[Option("override-mode", HelpText = "Override the link generation mode (\"offline\" generates links to local files whereas \"online\" links to remote web server")]
		public LocalNotionMode? ModeOverride { get; set; } = null;

		[Option("override-base-url", HelpText = $"Override base URL for generated content links")]
		public string BaseUrlOverride { get; set; } = null;

	}

	[Verb("git", HelpText = "Displays or changes repository Git settings without committing or pushing")]
	public class GitRepositoryCommandArguments : CommandArgumentsBase {

		[Option('p', "path", HelpText = "Path to an existing Local Notion repository (default is current directory)")]
		public string Path { get; set; } = GetDefaultRepoFolder();

		[Option("enabled", Min = 1, Max = 1, HelpText = "Set automatic Git staging/commits for later commands: true or false")]
		public IEnumerable<string> Enabled { get; set; }

		[Option("push", Min = 1, Max = 1, HelpText = "Set automatic push after later commits: true or false (requires enabled=true to take effect)")]
		public IEnumerable<string> Push { get; set; }
	}

	[Verb("redirects", HelpText = "Lists, adds or removes the redirects a repository keeps for changed page and file addresses")]
	public class RedirectsCommandArguments : CommandArgumentsBase {

		[Option('p', "path", HelpText = "Path to an existing Local Notion repository (default is current directory)")]
		public string Path { get; set; } = GetDefaultRepoFolder();

		[Option("add", Min = 2, Max = 2, HelpText = "Add a redirect: the old address, then the new address, both site-relative")]
		public IEnumerable<string> Add { get; set; }

		[Option("soft", HelpText = "With --add, answer with 302 (temporary) instead of 301 (permanent)")]
		public bool Soft { get; set; }

		[Option("remove", HelpText = "Remove the redirect from this old address")]
		public string Remove { get; set; }
	}

	[Verb("create-cms", HelpText = "Creates one blank Notion CMS database with the website schema and preset options")]
	public class CreateCMSCommandArguments : CommandArgumentsBase {

		[Option("parent", Required = true, HelpText = "ID of the Notion parent page shared with your integration")]
		public string Parent { get; set; }

		[Option("title", Default = "My Website", HelpText = "Title of the new CMS database")]
		public string Title { get; set; } = "My Website";

		[Option('k', "key", HelpText = "Notion API key (prefer NOTION_API_KEY_FILE to keep it out of shell history)")]
		public string APIKey { get; set; }

		[Option('p', "path", HelpText = "Optional existing repository to read an API key from; no repository files are changed")]
		public string Path { get; set; }

		[Option('v', "verbose", HelpText = "Display debug information in console output")]
		public bool Verbose { get; set; }
	}

	[Verb("clean", HelpText = "Cleans your local Notion repository by removing dangling pages, files and databases")]
	public class CleanRepositoryCommandArguments : CommandArgumentsBase {

		[Option('p', "path", HelpText = "Path to Local Notion repository")]
		public string Path { get; set; } = GetDefaultRepoFolder();

		[Option('v', "verbose", HelpText = $"Display debug information in console output")]
		public bool Verbose { get; set; } = false;

	}

	[Verb("remove", HelpText = "Remove resources from a Local Notion repository")]
	public class RemoveRepositoryCommandArguments : CommandArgumentsBase {

		[Option('p', "path", HelpText = "Path to Local Notion repository (default is current working dir)")]
		public string Path { get; set; } = GetDefaultRepoFolder();

		[Option("all", HelpText = "Removes entire repository")]
		public bool All { get; set; }

		[Option('o', "objects", HelpText = "List of Notion objects to remove (i.e. pages, databases, workspaces)")]
		public IEnumerable<Guid> Objects { get; set; } = null;

		[Option('v', "verbose", HelpText = $"Display debug information in console output")]
		public bool Verbose { get; set; } = false;

	}

	[Verb("list", HelpText = "Lists objects from Notion which can be pulled into Local Notion")]
	public class ListContentsCommandArguments : CommandArgumentsBase {

		[Option('o', "objects", HelpText = "List only these objects")]
		public IEnumerable<Guid> Objects { get; set; } = null;

		[Option('a', "all", HelpText = "Lists all objects your integration has access to (in CMS mode lists only CMS items)")]
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
		public IEnumerable<Guid> Objects { get; set; } = null;

		[Option('a', "all", Group = "target", HelpText = "Pull all objects into repository (in CMS mode, all CMS items only)")]
		public bool PullAll { get; set; }

		[Option('k', "key", HelpText = "Notion API key to use (overrides repository key if any)")]
		public string APIKey { get; set; } = null;

		[Option('p', "path", HelpText = "Path to Local Notion repository (default is current working dir)")]
		public string Path { get; set; } = GetDefaultRepoFolder();

		[Option("render", Default = true, HelpText = "Renders objects after pull: true or false (a bare option means true)")]
		public bool? RenderOption { get; set; }

		public bool Render { get => RenderOption ?? true; set => RenderOption = value; }

		[Option("render-type", Default = RenderType.HTML, HelpText = "Type of rendering to use (HTML)")]
		public RenderType RenderOutput { get; set; }

		[Option("render-mode", Default = RenderMode.ReadOnly, HelpText = "Rendering mode for objects (ReadOnly, Editable)")]
		public RenderMode RenderMode { get; set; }

		[Option("nginx-reload-force", Default = false, HelpText = "Reloads NGINX server after pull irrespective of updates or not")]
		public bool NginxReloadForce { get; set; }

		[Option("nginx-reload", Default = false, HelpText = "Reloads NGINX server only on update")]
		public bool NginxReload { get; set; }

		[Option("fault-tolerant", Default = true, HelpText = "Continues processing on failures: true or false (a bare option means true)")]
		public bool? FaultTolerantOption { get; set; }

		public bool FaultTolerant { get => FaultTolerantOption ?? true; set => FaultTolerantOption = value; }

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

		[Option('p', "path", HelpText = "Path to Local Notion repository (default is current working dir)")]
		public string Path { get; set; } = GetDefaultRepoFolder();

		[Option('o', "objects", Required = false, HelpText = "List of object ID's to render (i.e. page(s), database(s), workspace)")]
		public IEnumerable<Guid> Objects { get; set; } = null;

		[Option('a', "all", HelpText = "Renders all objects in repository")]
		public bool RenderAll { get; set; }

		[Option("render-type", Default = RenderType.HTML, HelpText = "Type of rendering to use (HTML)")]
		public RenderType RenderOutput { get; set; }

		[Option("render-mode", Default = RenderMode.ReadOnly, HelpText = "Rendering mode for objects (ReadOnly, Editable)")]
		public RenderMode RenderMode { get; set; }

		[Option("fault-tolerant", Default = true, HelpText = "Continues processing on failures: true or false (a bare option means true)")]
		public bool? FaultTolerantOption { get; set; }

		public bool FaultTolerant { get => FaultTolerantOption ?? true; set => FaultTolerantOption = value; }

		[Option('v', "verbose", HelpText = $"Display debug information in console output")]
		public bool Verbose { get; set; } = false;

	}

	[Verb("prune", HelpText = "Removes objects from a Local Notion that no longer exist in Notion")]
	public class PruneCommandArguments : CommandArgumentsBase {

		[Option('r', "path", HelpText = "Path to Local Notion repository (default current working dir)")]
		public string Path { get; set; } = GetDefaultRepoFolder();

		[Option('o', "objects", HelpText = "List of object ID's to keep (i.e. page(s), database(s), workspace)")]
		public IEnumerable<Guid> Objects { get; set; } = null;

		[Option('v', "verbose", HelpText = $"Display debug information in console output")]
		public bool Verbose { get; set; } = false;

	}


	[Verb("service", HelpText = "Runs as service")]
	public class ServiceCommandArguments {

		[Option('o', "objects", Required = true, Group = "target", HelpText = "List of Notion objects to pull (i.e. pages, databases)")]
		public IEnumerable<Guid> Objects { get; set; } = null;

		[Option('p', "path", Required = true, HelpText = "Path to Local Notion repository (or registry)")]
		public string Path { get; set; }

		[Option('f', "poll-frequency", Default = 30, HelpText = "How often to poll Notion for changes")]
		public int PollFrequency { get; set; }

	}

	public static async Task<int> ExecuteInitCommandAsync(InitRepositoryCommandArguments arguments, CancellationToken cancellationToken) {
		var consoleLogger = new ConsoleLogger { Options = arguments.Verbose ? LogOptions.VerboseProfile : LogOptions.UserDisplayProfile };

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
			pathProfile.BaseUrl = arguments.BaseUrlOverride; ;
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
		if (!string.IsNullOrWhiteSpace(arguments.CMSPathOverride))
			pathProfile.CMSPathR = GetInputPathRelativeToRepo(arguments.Path, arguments.CMSPathOverride);

		var gitSettings =
			arguments.EnableGit ?
			new GitSettings {
				Enabled = true,
				Push = arguments.PushRemote
			} :
			GitSettings.Default;

		var nginxSettings =
			arguments.EnableNginx ?
			new NGinxSettings {
				Enabled = true,
				ReloadCommand = arguments.NginxReloadCommand.ToNullWhenWhitespace() ?? (RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? WinNGinxReloadCommand : LinuxNGinxReloadCommand)
			} :
			NGinxSettings.Default;

		var apacheSettings =
			arguments.EnableApache ?
			new ApacheSettings {
				Enabled = true
			} :
			ApacheSettings.Default;

		// Create git repo if required
		if (gitSettings.Enabled) {
			var gitSentry = new GitSentry(arguments.Path);
			if (!await gitSentry.Init(cancellationToken)) {
				consoleLogger.Error($"git failed with error:{Environment.NewLine}{gitSentry.Output.Tabbify()}");
				throw new InvalidOperationException("Unable to create git repository");
			}

			var gitIgnoreFile = Path.Combine(arguments.Path, ".gitignore");
			// Create .gitignore files
			const string GitIgnoreContents =
				"""
				# Local Notion logs
				.localnotion/logs

				# NGINX logs
				.localnotion/nginx/logs
				""";
			await File.WriteAllTextAsync(gitIgnoreFile, GitIgnoreContents, cancellationToken);
		}


		await using var repo = await LocalNotionRepository.CreateNew(
			arguments.Path,
			arguments.APIKey,
			arguments.CMSDatabase,
			arguments.Themes.ToArray(),
			arguments.LogLevel,
			pathProfile,
			gitSettings,
			nginxSettings,
			apacheSettings,
			logger: consoleLogger
		);

		if (gitSettings.Enabled && !await ProcessChangeControl(repo, consoleLogger, cancellationToken))
			return Constants.ERRORCODE_FAIL;

		consoleLogger.Info("Location Notion repository has been created");
		return Constants.ERRORCODE_OK;
	}

	public static async Task<int> ExecuteCleanCommandAsync(CleanRepositoryCommandArguments arguments, CancellationToken cancellationToken) {
		var consoleLogger = new ConsoleLogger { Options = arguments.Verbose ? LogOptions.VerboseProfile : LogOptions.UserDisplayProfile };

		arguments.Path = ToFullPath(arguments.Path);
		if (!Directory.Exists(arguments.Path))
			throw new DirectoryNotFoundException(arguments.Path);


		await using var repo = await OpenRepository(arguments.Path, consoleLogger);
		await repo.CleanAsync();

		if (repo.RequiresSave)
			await repo.SaveAsync();

		// Do git processing if available
		if (repo.GitSettings.Enabled) {
			await ProcessChangeControl(repo, consoleLogger, cancellationToken);
		}


		consoleLogger.Info("Location Notion repository has been cleaned");
		return Constants.ERRORCODE_OK;
	}

	public static async Task<int> ExecuteRemoveCommandAsync(RemoveRepositoryCommandArguments arguments, CancellationToken cancellationToken) {
		var consoleLogger = new ConsoleLogger { Options = arguments.Verbose ? LogOptions.VerboseProfile : LogOptions.UserDisplayProfile };
		if (!arguments.All) {
			await using var repo = await OpenRepository(arguments.Path, consoleLogger);
			foreach (var objectID in arguments.Objects.Select(x => x.ToString())) {

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

			// Generate nginx mapping if applicable
			if (repo.NGinxSettings.Enabled) {
				try {
					var folderPath = await NginxMappingsGenerator.GenerateNGinxFiles(repo);
					consoleLogger.Info($"Updating NGINX hosting files: {folderPath}");
				} catch (Exception error) {
					consoleLogger.Exception(error);
				}
			}

			if (repo.RequiresSave)
				await repo.SaveAsync();

			// Do git processing if available
			if (repo.GitSettings.Enabled) {
				await ProcessChangeControl(repo, consoleLogger, cancellationToken);
			}

		} else {
			if (await LocalNotionRepository.Remove(arguments.Path, consoleLogger)) {
				consoleLogger.Info("Local Notion repository has been removed");
			} else {
				consoleLogger.Warning("No Local Notion repository was found");
			}
		}

		return Constants.ERRORCODE_OK;
	}

	public static async Task<int> ExecuteListCommand(ListContentsCommandArguments arguments, CancellationToken cancellationToken) {
		var consoleLogger = new ConsoleLogger { Options = arguments.Verbose ? LogOptions.VerboseProfile : LogOptions.UserDisplayProfile };

		var apiKey = ResolveNotionApiKey(arguments.APIKey);
		if (string.IsNullOrWhiteSpace(apiKey) && LocalNotionRepository.Exists(arguments.Path)) {
			await using var repo = await OpenRepository(arguments.Path, consoleLogger);
			apiKey = repo.DefaultNotionApiKey;
		}

		if (string.IsNullOrWhiteSpace(apiKey)) {
			consoleLogger.Info("No API key was supplied by --key, NOTION_API_KEY_FILE, or the repository");
			return Constants.ERRORCODE_COMMANDLINE_ERROR;
		}

		var client = CreateNotionClient(apiKey);

		if (!arguments.Objects.Any()) {
			// List workspace level
			Console.WriteLine($"Listing workspace {$"filtering by '{arguments.Filter}'".AsAmendmentIf(!string.IsNullOrWhiteSpace(arguments.Filter))}{"(use --all switch to include child objects)".AsAmendmentIf(!arguments.All)}");
			var searchParameters = new SearchRequest { Query = arguments.Filter };
			var results = client.EnumerateAllWorkspaceObjectsAsync(searchParameters, cancellationToken);
			if (!arguments.All)
				results = results.Where(IsWorkspaceRoot);

			await foreach (var obj in results.WithCancellation(cancellationToken))
				PrintObject(obj);

		} else {
			// Lists database contents
			foreach (var @obj in arguments.Objects.Select(x => x.ToString())) {
				switch (await client.QualifyObjectAsync(@obj, cancellationToken)) {
					case (LocalNotionResourceType.Database, _, _):
						PrintObject(await client.Databases.RetrieveAsync(@obj, cancellationToken));
						if (arguments.All) {
							var searchParameters = new QueryDataSourceRequest();
							var results = client.DataSources.EnumerateAsync(@obj, searchParameters, cancellationToken);
							await foreach (var dbPage in results)
								PrintObject(dbPage);
						}

						break;
					case (LocalNotionResourceType.Page, _, _):
						PrintObject(await client.Pages.RetrieveAsync(@obj, cancellationToken));
						break;
					default:
						Console.WriteLine($"Unrecognized object: {@obj}");
						break;

				}
				;
			}
		}

		bool IsWorkspaceRoot(IObject obj) {
			var parent = obj.GetParent();
			return parent.Type == ParentObject.ParentType.Workspace;
		}

		void PrintObject(IObject obj) {
			Console.WriteLine($"{obj.Id}   {ToAcronym(obj.Object).PadRight(2)}   {obj.GetLastEditedDate():yyyy-MM-dd HH:mm}   {obj.GetTitle()}");
		}

		string ToAcronym(ObjectType objectType)
			=> objectType.ToString() switch {
				ObjectType.PageValue => "P",
				ObjectType.DatabaseValue => "DB",
				ObjectType.DataSourceValue => "DS",
				ObjectType.BlockValue => "B",
				ObjectType.UserValue => "U",
				ObjectType.CommentValue => "C",
				ObjectType.FileUploadValue => "F",
				_ => throw new ArgumentOutOfRangeException(nameof(objectType), objectType, null)
			};

		return Constants.ERRORCODE_OK;
	}

	public static async Task<int> ExecutePullCommandAsync(PullRepositoryCommandArguments arguments, CancellationToken cancellationToken) {
		var consoleLogger = new ConsoleLogger { Options = arguments.Verbose ? LogOptions.VerboseProfile : LogOptions.UserDisplayProfile };
		await using var repo = await OpenRepository(arguments.Path, consoleLogger);

		await using (repo.EnterUpdateScope()) {
			var apiKey = ResolveNotionApiKey(arguments.APIKey, repo.DefaultNotionApiKey);

			if (string.IsNullOrWhiteSpace(apiKey)) {
				consoleLogger.Info("No API key was supplied by --key, NOTION_API_KEY_FILE, or the repository");
				return -1;
			}
			var client = CreateNotionClient(apiKey);

			var syncOrchestrator = new NotionSyncOrchestrator(client, repo);
			using var pullSession = syncOrchestrator.BeginPull();

			// If pulling all, add all objects into list of objects to pull
			if (arguments.PullAll) {
				if (repo.CMSDatabaseID is null) {
					consoleLogger.Info("Querying Notion for objects to pull");
					// Under API version 2025-09-03 Search returns Page and DataSource objects and never a
					// Database, so a database sitting directly at the workspace root would never be seen.
					// EnumerateAllWorkspaceObjectsAsync resolves each DataSource back to its parent Database
					// and yields both, which restores those roots.
					var visibleObjects = await client
						.EnumerateAllWorkspaceObjectsAsync(new SearchRequest(), cancellationToken)
						.Where(x => x is Database or Page)
						.ToArrayAsync(cancellationToken);

					bool IsWorkspaceRoot(IObject obj)
						=> obj.TryGetParent(out var parent) && parent.Type == ParentObject.ParentType.Workspace;

					// Workspace roots first, so page trees are pulled parent-before-child.
					var rootItems = visibleObjects.Where(IsWorkspaceRoot).Select(x => Guid.Parse(x.Id));

					// Then everything else Search can see. Recursion through the block tree cannot be
					// relied on to reach every page: Notion reports some pages as having a block parent
					// while that block returns has_children=false and lists no children, so the page is
					// unreachable by any tree walk. Those pages are only discoverable through Search.
					// Objects already pulled by recursion are unchanged by the time their own turn comes,
					// so this sweep is cheap rather than duplicated work.
					var nestedItems = visibleObjects.Where(x => !IsWorkspaceRoot(x)).Select(x => Guid.Parse(x.Id));

					arguments.Objects = arguments.Objects.Union(rootItems).Union(nestedItems).ToArray();
				} else {
					consoleLogger.Info($"Pulling from CMS database: {repo.CMSDatabaseID} ");
					arguments.Objects = arguments.Objects.Union([Guid.Parse(repo.CMSDatabaseID)]).ToArray();
				}
			}

			// Pull explicitly specified objects if applicable
			var itemsDownloaded = 0L;
			var failedObjects = 0;
			foreach (var @obj in arguments.Objects.Select(x => x.ToString())) {
				var downloads = Array.Empty<LocalNotionResource>();
				// One unusable root must not cost the whole mirror. The download phase reaches into
				// parsing, CMS, rendering and repository code, each of which throws on data shapes
				// Notion introduces without notice; the handlers inside the orchestrator cover the
				// objects *within* a root, never the root itself. Without this, one bad root aborts
				// every root after it.
				try {
					var objType = await client.QualifyObjectAsync(@obj, cancellationToken);
					switch (objType) {
						case (null, _, _):
							consoleLogger.Info($"Unrecognized object: {@obj}");
							break;
						case (_, _, true):
							if (repo.ContainsResource(obj))
								repo.RemoveResource(@obj, true);
							break;
						case (LocalNotionResourceType.Database, var lastEditedTime, _):
							downloads = await syncOrchestrator.DownloadDatabaseAsync(
								@obj,
								lastEditedTime,
								new DownloadOptions {
									Render = arguments.Render,
									RenderType = arguments.RenderOutput,
									RenderMode = arguments.RenderMode,
									ForceRefresh = arguments.Force,
									FaultTolerant = arguments.FaultTolerant
								},
								cancellationToken
							);
							break;
						case (LocalNotionResourceType.Page, var lastEditTimeNotion, _):
							downloads = await syncOrchestrator.DownloadPageAsync(
								@obj,
								lastEditTimeNotion,
								new DownloadOptions() {
									Render = arguments.Render,
									RenderType = arguments.RenderOutput,
									RenderMode = arguments.RenderMode,
									ForceRefresh = arguments.Force,
									FaultTolerant = arguments.FaultTolerant
								},
								cancellationToken
							);
							break;
						default:
							consoleLogger.Info($"Synchronizing objects of type {objType} is not supported yet");
							break; ;
					}
				} catch (TaskCanceledException) {
					throw;
				} catch (Exception error) {
					failedObjects++;
					consoleLogger.Error($"Failed to pull object '{@obj}'.");
					consoleLogger.Exception(error);
					if (!arguments.FaultTolerant)
						throw;
				}
				itemsDownloaded += downloads.Length;
			}
			consoleLogger.Info($"Updated {itemsDownloaded} items");
			if (failedObjects > 0)
				consoleLogger.Error($"{failedObjects} of {arguments.Objects.Count()} requested objects failed to pull");

			// Generate nginx mapping if applicable
			if (repo.NGinxSettings.Enabled) {
				try {
					var nginxMappingFilePath = NginxMappingsGenerator.CalculateMappingFile(repo);
					var specifiesNginxReload = arguments.NginxReload || arguments.NginxReloadForce;
					var shouldReloadNGinx = arguments.NginxReloadForce || (arguments.NginxReload && !File.Exists(nginxMappingFilePath) || itemsDownloaded > 0);
					if (shouldReloadNGinx) {
						var folderPath = await NginxMappingsGenerator.GenerateNGinxFiles(repo);
						consoleLogger.Info($"Generated NGINX hosting files: {folderPath}");

						if (arguments.NginxReload) {
							var reloadCmd = repo.NGinxSettings.ReloadCommand;
							consoleLogger.Info($"Reloading NGINX configuration: {reloadCmd}");
							var (executable, args) = Tools.Runtime.ParseCommandLine(reloadCmd);

							var process = Process.Start(new ProcessStartInfo(executable, args) { UseShellExecute = true, WorkingDirectory = folderPath });
							await process.WaitForExitAsync(cancellationToken);
							consoleLogger.Info($"NGINX reload exited with code {process.ExitCode}");

						}
					} else {
						consoleLogger.Info("NGINX configurations not changed as no changes detected");
					}
				} catch (Exception error) {
					consoleLogger.Exception(error);
				}
			}

			if (repo.RequiresSave)
				await repo.SaveAsync();

			// Do git processing if available
			if (repo.GitSettings.Enabled) {
				await ProcessChangeControl(repo, consoleLogger, cancellationToken);
			}
		}
		return 0;
	}

	public static async Task<int> ExecuteSyncCommandAsync(SyncRepositoryCommandArguments arguments, CancellationToken cancellationToken) {
		// NOTE: FilterLastUpdateOn not requied since SyncOrchestrator intelligently determines 
		// what to fetch

		var consoleLogger = new ConsoleLogger { Options = arguments.Verbose ? LogOptions.VerboseProfile : LogOptions.UserDisplayProfile };

		Console.WriteLine($"Synchronizing every {arguments.PollFrequency} seconds (send Break or CTRL-C to stop)");
		while (true) {
			Console.WriteLine($"Synchronizing Updates: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
			//arguments.FilterLastUpdatedOn = DateTime.Now;
			var result = await ExecutePullCommandAsync(arguments, cancellationToken);
			if (result != Constants.ERRORCODE_OK && !arguments.FaultTolerant)
				return result;
			await Task.Delay(TimeSpan.FromSeconds(arguments.PollFrequency), cancellationToken);
			//arguments.FilterLastUpdatedOn = DateTime.UtcNow;
			GC.Collect();
			GC.WaitForPendingFinalizers();
		}
		return Constants.ERRORCODE_OK;
	}

	public static async Task<int> ExecuteRenderCommandAsync(RenderCommandArguments arguments, CancellationToken cancellationToken) {
		var consoleLogger = new ConsoleLogger { Options = arguments.Verbose ? LogOptions.VerboseProfile : LogOptions.UserDisplayProfile };
		await using var repo = await OpenRepository(arguments.Path, consoleLogger);
		var hadRenderFailures = false;
		await using (repo.EnterUpdateScope()) {
			var renderer = new RenderingManager(repo, repo.Logger);
			using var renderBatch = renderer.BeginBatch();
			var toRender = (arguments.RenderAll ? LocalNotionHelper.FilterRenderableResources(repo.Resources).Select(x => x.ID) : arguments.Objects.Select(x => x.ToString())).ToHashSet();
			var cmsItemsToRender = (arguments.RenderAll ? repo.CMSItems : repo.CMSItems.Where(x => x.ReferencesAnyResources(toRender))).ToArray();
			// CMS sites host cms/ output. --all should re-render those pages, not every source resource.
			if (arguments.RenderAll && cmsItemsToRender.Length > 0)
				toRender.Clear();
			if (toRender.Count == 0 && cmsItemsToRender.Length == 0) {
				consoleLogger.Warning("Nothing to render");
				return Constants.ERRORCODE_OK;
			}

			renderer.PrepareRenderPaths(toRender, arguments.RenderOutput, cmsItemsToRender, arguments.FaultTolerant, cancellationToken);

			foreach (var resource in toRender) {
				try {
					cancellationToken.ThrowIfCancellationRequested();
					renderer.RenderLocalResource(resource, arguments.RenderOutput, arguments.RenderMode);
				} catch (OperationCanceledException) {
					throw;
				} catch (Exception error) {
					hadRenderFailures = true;
					consoleLogger.Exception(error);
					if (!arguments.FaultTolerant)
						throw;
				}
			}


			foreach (var cmsItem in cmsItemsToRender) {
				try {
					cancellationToken.ThrowIfCancellationRequested();
					renderer.RenderCMSItem(cmsItem);
				} catch (OperationCanceledException) {
					throw;
				} catch (Exception error) {
					hadRenderFailures = true;
					consoleLogger.Exception(error);
					if (!arguments.FaultTolerant)
						throw;
				}
			}
		}

		if (repo.RequiresSave)
			await repo.SaveAsync();

		// Publish hosting only after a full batch has finalized every CMS destination.
		if (repo.NGinxSettings.Enabled && arguments.RenderAll) {
			if (hadRenderFailures) {
				consoleLogger.Warning("Skipped NGINX hosting generation and CMS cleanup because one or more renders failed.");
			} else {
				var folderPath = await NginxMappingsGenerator.GenerateNGinxFiles(repo);
				consoleLogger.Info($"Generated NGINX hosting files: {folderPath}");
			}
		}

		// Do git processing if available
		if (repo.GitSettings.Enabled) {
			await ProcessChangeControl(repo, consoleLogger, cancellationToken);
		}

		return Constants.ERRORCODE_OK;
	}

	public static async Task<int> ExecutePruneCommandAsync(PruneCommandArguments arguments, CancellationToken cancellationToken) {
		var consoleLogger = new ConsoleLogger { Options = arguments.Verbose ? LogOptions.VerboseProfile : LogOptions.UserDisplayProfile };
		consoleLogger.Warning("Local Notion pruning is not currently implemented");
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
			Console.WriteLine(error.ToDisplayString());
			return Constants.ERRORCODE_FAIL;
		}
	}

	public static ParserResult<object> ParseCommandLineArguments(string[] args, Parser parser = null) {
		// Nullable booleans consume explicit values; preserve the existing bare switches by supplying true.
		if (args.Length > 0 && args[0] is "pull" or "sync" or "render") {
			var expanded = new List<string>(args.Length);
			for (var i = 0; i < args.Length; i++) {
				expanded.Add(args[i]);
				if (args[i] == "--") {
					expanded.AddRange(args.Skip(i + 1));
					break;
				}
				if ((args[i] == "--fault-tolerant" || args[i] == "--render" && args[0] != "render")
					&& (i + 1 == args.Length || args[i + 1].StartsWith("-", StringComparison.Ordinal)))
					expanded.Add("true");
			}
			args = expanded.ToArray();
		}
		return (parser ?? Parser.Default).ParseArguments<
			StatusRepositoryCommandArguments,
			InitRepositoryCommandArguments,
			GitRepositoryCommandArguments,
			RedirectsCommandArguments,
			CreateCMSCommandArguments,
			CleanRepositoryCommandArguments,
			RemoveRepositoryCommandArguments,
			ListContentsCommandArguments,
			SyncRepositoryCommandArguments,
			PullRepositoryCommandArguments,
			RenderCommandArguments,
			PruneCommandArguments,
			int
		>(args);
	}

	public static Task<int> ProcessCommandLineAsync(string[] args, Parser parser = null, Func<string, INotionClient> cmsClientFactory = null) {
		return ParseCommandLineArguments(args, parser).MapResult(
			(StatusRepositoryCommandArguments commandArgs) => ExecuteCommandAsync(commandArgs, ExecuteStatusCommandAsync),
			(InitRepositoryCommandArguments commandArgs) => ExecuteCommandAsync(commandArgs, ExecuteInitCommandAsync),
			(GitRepositoryCommandArguments commandArgs) => ExecuteCommandAsync(commandArgs, ExecuteGitCommandAsync),
			(RedirectsCommandArguments commandArgs) => ExecuteCommandAsync(commandArgs, ExecuteRedirectsCommandAsync),
			(CreateCMSCommandArguments commandArgs) => ExecuteCommandAsync(commandArgs,
				(arguments, token) => ExecuteCreateCMSCommandAsync(arguments, token, cmsClientFactory ?? CreateNotionClient)),
			(CleanRepositoryCommandArguments commandArgs) => ExecuteCommandAsync(commandArgs, ExecuteCleanCommandAsync),
			(RemoveRepositoryCommandArguments commandArgs) => ExecuteCommandAsync(commandArgs, ExecuteRemoveCommandAsync),
			(ListContentsCommandArguments commandArgs) => ExecuteCommandAsync(commandArgs, ExecuteListCommand),
			(SyncRepositoryCommandArguments commandArgs) => ExecuteCommandAsync(commandArgs, ExecuteSyncCommandAsync),
			(PullRepositoryCommandArguments commandArgs) => ExecuteCommandAsync(commandArgs, ExecutePullCommandAsync),
			(RenderCommandArguments commandArgs) => ExecuteCommandAsync(commandArgs, ExecuteRenderCommandAsync),
			(PruneCommandArguments commandArgs) => ExecuteCommandAsync(commandArgs, ExecutePruneCommandAsync),
			ProcessCommandLineErrorsAsync
		);
	}


	/// <summary>
	/// The main entry point for the application.
	/// </summary>
	[STAThread]
	public static async Task<int> Main(string[] args) {
		try {
			var frameworkOptions = Sphere10FrameworkOptions.Default;
			Sphere10Framework.Instance.Build()
				.WithOptions(frameworkOptions)
				.AddModule<ModuleConfiguration>()
				.Start();

			Console.CancelKeyPress += (sender, args) => {
				Console.WriteLine("Cancelling");
				args.Cancel = true;
				CancelProgram.Cancel();
			};
			return await ProcessCommandLineAsync(args);
		} finally {
			if (Sphere10Framework.Instance.IsStarted)
				Sphere10Framework.Instance.EndFramework();
		}
	}

	private static string GetDefaultRepoFolder()
		=> System.IO.Path.Combine(Environment.CurrentDirectory);

	private static string ResolveNotionApiKey(string explicitKey, string repositoryKey = null) {
		if (!string.IsNullOrWhiteSpace(explicitKey))
			return explicitKey;

		var secretPath = Environment.GetEnvironmentVariable("NOTION_API_KEY_FILE");
		if (!string.IsNullOrWhiteSpace(secretPath) && File.Exists(secretPath)) {
			var secret = File.ReadAllText(secretPath).Trim();
			if (!string.IsNullOrWhiteSpace(secret))
				return secret;
		}

		return repositoryKey;
	}

	private static string ToFullPath(string userEnteredPath) {
		if (Path.IsPathFullyQualified(userEnteredPath))
			return userEnteredPath;
		return Path.GetFullPath(userEnteredPath, Environment.CurrentDirectory);
	}

	private static string GetInputPathRelativeToRepo(string repoPath, string userEnteredPath) {
		if (Path.IsPathFullyQualified(userEnteredPath))
			return Path.GetRelativePath(repoPath, userEnteredPath); ;
		return Path.GetRelativePath(repoPath, Path.GetFullPath(userEnteredPath, Environment.CurrentDirectory));
	}

	private static async Task<bool> ProcessChangeControl(ILocalNotionRepository repo, ILogger logger, CancellationToken cancellationToken = default) {
		Guard.Ensure(repo.GitSettings.Enabled, "Git tracking is not enabled on this repository");
		var gitSentry = new GitSentry(repo.Paths.GetRepositoryPath(FileSystemPathType.Absolute));
		if (!await gitSentry.TestGitInstalled(cancellationToken)) {
			logger.Error("Unable to track changes as git is not installed");
			return false;
		}

		logger.Info("Adding changes to git");
		if (!await gitSentry.AddAll(cancellationToken)) {
			logger.Error($"git failed with error:{Environment.NewLine}{gitSentry.Output.Tabbify()}");
			return false;
		}

		logger.Info("Committing changes to git");
		if (!await gitSentry.Commit($"Content updates: {DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}", cancellationToken)) {
			if (gitSentry.Output.Contains("nothing to commit", StringComparison.OrdinalIgnoreCase)) {
				logger.Info("No git changes to commit");
				return true;
			}
			logger.Error($"git failed with error:{Environment.NewLine}{gitSentry.Output.Tabbify()}");
			return false;
		}

		if (repo.GitSettings.Push) {
			logger.Info("Pushing git changes to default remote");
			if (!await gitSentry.Push()) {
				logger.Error($"git failed with error:{Environment.NewLine}{gitSentry.Output.Tabbify()}");
				return false;
			}
		}

		return true;
	}

	private static INotionClient CreateNotionClient(string apiKey)
		=> NotionClientFactory.Create(new ClientOptions { AuthToken = apiKey });

	private static async Task<ILocalNotionRepository> OpenRepository(string path, ILogger logger = null)
		=> await LocalNotionRepository.Open(path, logger);

}
