using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Hydrogen;


namespace LocalNotion.Core;

public class ProcessSentry {
	private readonly SynchronizedObject _lock;
	private readonly Trigger _controlCTrigger;
	public ProcessSentry(string fileName) {
		FileName = fileName;
		_lock = new SynchronizedObject();
		_controlCTrigger = new Trigger();
	}

	public bool MultiInstance { get; init; } = false;

	public string FileName { get; init; }


	public virtual void SendCtrlC() => _controlCTrigger.Fire();

	public async Task<bool> CanRun(CancellationToken cancellationToken = default)  {
		try {
			await RunProcess(FileName, null, null, null,  cancellationToken);
		} catch (Exception ex) {
			return false;
		}
		return true;
	}
		

	public virtual Task<int> Run(string arguments = null, TextWriter output = null, CancellationToken cancellationToken = default)  {
		using IScope lockScope = MultiInstance ? new NoOpScope() : _lock.EnterWriteScope(); // not Blocks on EnterWriteScope even on async
		return RunProcess(FileName, arguments, output, _controlCTrigger, cancellationToken);
	}


	public static async Task<int> RunProcess(string fileName, string arguments = null,  TextWriter output = null, Trigger ctrlCTrigger = null, CancellationToken cancellationToken = default) {
		var process = new Process();
		process.StartInfo.FileName = fileName;
		process.StartInfo.Arguments = arguments;
		process.StartInfo.ErrorDialog = false;
		process.StartInfo.UseShellExecute = false; 
		process.StartInfo.CreateNoWindow = true;
		
		if (ctrlCTrigger != null) {
			process.StartInfo.RedirectStandardInput = true;

			ctrlCTrigger.Fired += () => {
				process.StandardInput.Close();
			};
		}

		if (output != null) {
			process.StartInfo.RedirectStandardOutput = true;
			process.StartInfo.RedirectStandardError = true;
			var syncObj = new SynchronizedObject();
			process.OutputDataReceived += async (_, data) => {
				using (syncObj.EnterWriteScope()) 
					output.WriteLine(data.Data);
			};
		}

		if (!process.Start())
			throw new InvalidOperationException($"Unable to start process: {fileName}");

		if (!process.HasExited) {
			Tools.Exceptions.ExecuteIgnoringException(process.BeginOutputReadLine);
			Tools.Exceptions.ExecuteIgnoringException(process.BeginErrorReadLine);
		}

		await process.WaitForExitAsync();
		return process.ExitCode;
		
	}

}

public class LocalNotionSentry : ProcessSentry {


	public LocalNotionSentry() 
		: base("localnotion") {
	}

	public TextWriter Output { get; init; } = null;

	public override void SendCtrlC()  {
		if (File.Exists(FileTriggerPath))
			File.Delete(FileTriggerPath);
	}

	private string FileTriggerPath { get; set; }

	public async Task<bool> IsInstalledAsync(CancellationToken cancellationToken = default) 
		=> await CanRun(cancellationToken) && await Run("version", Output, cancellationToken) == 0;


	public async Task<int> GetStatus(string path, TextWriter output, CancellationToken cancellationToken = default)  {
		Guard.ArgumentNotNullOrEmpty(path, nameof(path));
		Guard.DirectoryExists(path);
		return await Run($"status -p {path}", output, cancellationToken);
	}


	public async Task<bool> List(string notionApiKey, CancellationToken cancellationToken = default) 
		=> await Run($"list -k {notionApiKey} -a", Output) == 0;


	public async Task<bool> List(CancellationToken cancellationToken = default) 
		=> await Run("list -k YOUR_NOTION_API_KEY_HERE -a", Output) == 0;


	public override async Task<int> Run(string arguments = null, TextWriter output = null, CancellationToken cancellationToken = default) {
		FileTriggerPath = Path.GetTempFileName();
		await using var disposable = new ActionDisposable(SendCtrlC);
		return await base.Run($"{arguments} --cancel-trigger \"{FileTriggerPath}\"", output, cancellationToken);
	}
}

