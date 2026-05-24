// Copyright (c) Herman Schoenfeld 2018 - Present. All rights reserved. (https://sphere10.com/products/localnotion)
// Author: Herman Schoenfeld <herman@sphere10.com>
//
// Distributed under the GPLv3 software license, see the accompanying file LICENSE 
// or visit https://github.com/HermanSchoenfeld/localnotion/blob/master/LICENSE
//
// This notice must not be removed when duplicating this file or its contents, in whole or in part.

using System.Text;
using Sphere10.Framework;

namespace LocalNotion.Core;

public class GitSentry : ProcessSentry {

	private const string GitExecutable = "git";
	private readonly StringBuilder _stringBuilder;

	public GitSentry(string rootDir) : base(GitExecutable) {
		_stringBuilder = new StringBuilder();
		WorkingDirectory = rootDir;
		OutputWriter = new StringWriter(_stringBuilder);
	}

	public string Output => _stringBuilder.ToString().Trim();

	public async Task<bool> Init(CancellationToken cancellationToken = default) {
		return (await base.RunAsync("init", cancellationToken)) == 0;
	}

	public async Task<bool> AddAll(CancellationToken cancellationToken = default) {
		_stringBuilder.Clear();
		return (await base.RunAsync("add --all", cancellationToken)) == 0;
	}

	public async Task<bool> Commit(string message, CancellationToken cancellationToken = default) {
		_stringBuilder.Clear();
		return (await base.RunAsync($"commit -m \"{message}\"", cancellationToken)) == 0;
	}

	public async Task<bool> Push(CancellationToken cancellationToken = default) {
		_stringBuilder.Clear();
		return (await base.RunAsync("push", cancellationToken)) == 0;
	}

	public async Task<bool> TestGitInstalled(CancellationToken cancellationToken = default) {
		_stringBuilder.Clear();
		try {
			return (await base.RunAsync("help", cancellationToken)) == 0;
		} catch {
			return false;
		}
	}
}
