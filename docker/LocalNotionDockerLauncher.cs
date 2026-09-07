using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.Serialization.Json;
using System.Text;

internal static class LocalNotionDockerLauncher {
    private static string Quote(string value) {
        var output = new StringBuilder("\"");
        int slashes = 0;
        foreach (char c in value) {
            if (c == '\\') { slashes++; continue; }
            if (c == '"') {
                output.Append('\\', slashes * 2 + 1);
                output.Append(c);
            } else {
                output.Append('\\', slashes);
                output.Append(c);
            }
            slashes = 0;
        }
        output.Append('\\', slashes * 2);
        output.Append('"');
        return output.ToString();
    }

    public static int Main(string[] args) {
        try {
            string script = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "localnotion-docker.ps1");
            if (!File.Exists(script)) {
                Console.Error.WriteLine("Local Notion Docker launcher is incomplete. Run docker/install-cli.ps1 again.");
                return 1;
            }
            var start = new ProcessStartInfo {
                FileName = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows),
                    @"System32\WindowsPowerShell\v1.0\powershell.exe"),
                Arguments = "-NoLogo -NoProfile -ExecutionPolicy Bypass -File " + Quote(script),
                WorkingDirectory = Environment.CurrentDirectory,
                UseShellExecute = false
            };
            using (var stream = new MemoryStream()) {
                new DataContractJsonSerializer(typeof(string[])).WriteObject(stream, args);
                start.EnvironmentVariables["LOCALNOTION_DOCKER_ARGS"] = Convert.ToBase64String(stream.ToArray());
            }
            // Ctrl+C reaches the child too. Give it time to stop its temporary container.
            Console.CancelKeyPress += delegate(object sender, ConsoleCancelEventArgs e) { e.Cancel = true; };
            using (var process = Process.Start(start)) {
                process.WaitForExit();
                return process.ExitCode;
            }
        } catch (Exception error) {
            Console.Error.WriteLine("Local Notion Docker: " + error.Message);
            return 1;
        }
    }
}
