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


	public void SendCtrlC() => _controlCTrigger.Fire();

	public Task<int> Run(string arguments = null, TextWriter output = null)  {
		using IScope lockScope = MultiInstance ? new NoOpScope() : _lock.EnterWriteScope(); // not Blocks on EnterWriteScope even on async
		return RunProcess(FileName, arguments, output, _controlCTrigger);
	}


	public static async Task<int> RunProcess(string fileName, string arguments = null,  TextWriter output = null, Trigger ctrlCTrigger = null, CancellationToken cancellationToken = default) {
		var process = new Process();
		process.StartInfo.FileName = fileName;
		process.StartInfo.Arguments = arguments;
		process.StartInfo.ErrorDialog = false;
		process.StartInfo.UseShellExecute = false; 
		//process.StartInfo.CreateNoWindow = true;
		
		if (ctrlCTrigger != null) {
			process.StartInfo.RedirectStandardInput = true;

			ctrlCTrigger.Fired += () => {
				process.StandardInput.Write("\x3");
			//	process.StandardInput.Close();
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
		
		process.BeginOutputReadLine();
		process.BeginErrorReadLine();

		await process.WaitForExitAsync();
		return process.ExitCode;
		
	}


}

public class LocalNotionSentry : ProcessSentry {
	
	
	public LocalNotionSentry() : base("localnotion") {
	}

	public TextWriter Output { get; init; } = null;

	public async Task<bool> IsInstalledAsync(CancellationToken cancellationToken = default) 
		=> await Run("version", Output) == 0;

	public async Task<bool> List(CancellationToken cancellationToken = default) 
		=> await Run("list -k YOUR_NOTION_API_KEY_HERE -a", Output) == 0;

}

