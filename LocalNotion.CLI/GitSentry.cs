// Copyright (c) Herman Schoenfeld 2018 - Present. All rights reserved. (https://sphere10.com/products/localnotion)
// Author: Herman Schoenfeld <herman@sphere10.com>
//
// Distributed under the GPLv3 software license, see the accompanying file LICENSE
// or visit https://github.com/HermanSchoenfeld/localnotion/blob/master/LICENSE
//
// This notice must not be removed when duplicating this file or its contents, in whole or in part.

using System.Text;
using System.Text.RegularExpressions;
using Sphere10.Framework;

namespace LocalNotion.CLI;

public class GitSentry : ProcessSentry {

	private const string GitExecutable = "git";
	private readonly StringBuilder _stringBuilder;
	private readonly string _safeDirectory;

	public GitSentry(string rootDir) : base(GitExecutable) {
		_stringBuilder = new StringBuilder();
		WorkingDirectory = Path.TrimEndingDirectorySeparator(Path.GetFullPath(rootDir));
		_safeDirectory = OperatingSystem.IsWindows() ? WorkingDirectory.Replace('\\', '/') : WorkingDirectory;
		OutputWriter = new StringWriter(_stringBuilder);
	}

	public string Output => _stringBuilder.ToString().Trim();

	public async Task<bool> Init(CancellationToken cancellationToken = default) {
		return (await RunGitAsync(cancellationToken, "init")) == 0;
	}

	public async Task<bool> AddAll(CancellationToken cancellationToken = default) {
		for (var attempt = 1; ; attempt++) {
			cancellationToken.ThrowIfCancellationRequested();
			_stringBuilder.Clear();
			if (await RunGitAsync(cancellationToken, "add", "--all") == 0)
				return true;
			if (!OperatingSystem.IsWindows() || attempt >= 3 || !HasObjectFinalizationPermissionError())
				return false;

			// Windows can briefly deny renaming a completed temporary object while another handle is open.
			await WaitForAddRetryAsync(TimeSpan.FromMilliseconds(250 * attempt), cancellationToken);
		}
	}

	public async Task<bool> Commit(string message, CancellationToken cancellationToken = default) {
		_stringBuilder.Clear();
		return (await RunGitAsync(cancellationToken, "commit", "-m", message)) == 0;
	}

	public async Task<bool> Push(CancellationToken cancellationToken = default) {
		_stringBuilder.Clear();
		return (await RunGitAsync(cancellationToken, "push")) == 0;
	}

	public async Task<bool> TestGitInstalled(CancellationToken cancellationToken = default) {
		_stringBuilder.Clear();
		try {
			return (await RunGitAsync(cancellationToken, "help")) == 0;
		} catch {
			return false;
		}
	}

	protected virtual Task<int> RunGitAsync(CancellationToken cancellationToken, params string[] arguments) {
		// Trust only the selected repository for this Git process, without changing the user's configuration.
		var command = new[] { "-c", $"safe.directory={_safeDirectory}" }.Concat(arguments);
		return base.RunAsync(string.Join(" ", command.Select(QuoteArgument)), cancellationToken);
	}

	protected virtual Task WaitForAddRetryAsync(TimeSpan delay, CancellationToken cancellationToken)
		=> Task.Delay(delay, cancellationToken);

	private bool HasObjectFinalizationPermissionError()
		=> Regex.IsMatch(Output,
			@"^error: unable to write file (?:[^\r\n]*[\\/])?objects[\\/][0-9a-f]{2}[\\/](?:[0-9a-f]{38}|[0-9a-f]{62}): Permission denied\r?$",
			RegexOptions.Multiline | RegexOptions.CultureInvariant
		);

	private static string QuoteArgument(string argument) {
		// ProcessSentry takes ProcessStartInfo.Arguments, so quote for its Windows/Unix argument parser.
		var result = new StringBuilder("\"");
		var backslashes = 0;
		foreach (var character in argument ?? string.Empty) {
			if (character == '\\') {
				backslashes++;
				continue;
			}
			result.Append('\\', character == '"' ? backslashes * 2 + 1 : backslashes);
			result.Append(character);
			backslashes = 0;
		}
		result.Append('\\', backslashes * 2);
		result.Append('"');
		return result.ToString();
	}
}
