using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Sphere10.Framework;

namespace LocalNotion.Core;

public class LocalNotionSentry : ProcessSentry {
	public const string ExecutableFileName = "localnotion.exe";

	public LocalNotionSentry() 
		: base(ExecutableFileName) {
	}

	public override void BreakRunningProcess()  {
		if (File.Exists(FileTriggerPath))
			File.Delete(FileTriggerPath);
	}

	private string FileTriggerPath { get; set; }

	public new static Task<bool> CanRunAsync(CancellationToken cancellationToken = default)  
		=> ProcessSentry.CanRunAsync(ExecutableFileName, cancellationToken);

	//public async Task<bool> IsInstalledAsync(CancellationToken cancellationToken = default) 
	//	=> await CanRunAsync(cancellationToken) && await RunAsync("version", cancellationToken) == 0;

	public async Task<int> GetStatus(string path, TextWriter output, CancellationToken cancellationToken = default)  {
		Guard.ArgumentNotNullOrEmpty(path, nameof(path));
		Guard.DirectoryExists(path);
		return await RunAsync($"status -p {path}", cancellationToken);
	}

	public Task List(string[] objectIDs = null, bool includeChild = false, string filter = null, string notionApiKey = null, string repoPath = null, bool verbose = false,  CancellationToken cancellationToken = default) {
		var objIdsArgs = $" -o {objectIDs?.ToDelimittedString(", ")}".AsAmendmentIf(objectIDs is { Length: > 0 });
		var includeChildArgs = $" -a".AsAmendmentIf(includeChild);
		var filterArgs = $" -f \"{filter}\"".AsAmendmentIf(!string.IsNullOrWhiteSpace(filter));
		var notionApiKeyArgs = $" -k {notionApiKey}".AsAmendmentIf(!string.IsNullOrWhiteSpace(notionApiKey));
		var pathArgs = $" -p {repoPath}".AsAmendmentIf(!string.IsNullOrEmpty(repoPath));
		var verboseArgs = $" -v".AsAmendmentIf(verbose);
		return RunWithErrorCodeCheckAsync($"list{objIdsArgs}{includeChildArgs}{filterArgs}{notionApiKeyArgs}{pathArgs}{verboseArgs}", cancellationToken:cancellationToken);
	}

	public override async Task<int> RunAsync(string arguments = null, CancellationToken cancellationToken = default) {
		FileTriggerPath = Path.GetTempFileName();
		await using var disposable = new ActionDisposable(BreakRunningProcess);
		return await base.RunAsync($"{arguments} --cancel-trigger \"{FileTriggerPath}\"", cancellationToken);
	}
}