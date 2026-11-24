// Copyright (c) Herman Schoenfeld 2018 - Present. All rights reserved. (https://sphere10.com/products/localnotion)
// Author: Herman Schoenfeld <herman@sphere10.com>
//
// Distributed under the GPLv3 software license, see the accompanying file LICENSE
// or visit https://github.com/HermanSchoenfeld/localnotion/blob/master/LICENSE
//
// This notice must not be removed when duplicating this file or its contents, in whole or in part.

using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Sphere10.Framework;

namespace LocalNotion.Core;

/// <summary>
/// The git integration of a repository: initializing it with its .gitignore, and committing (and optionally pushing) the content a pull or
/// render changed. Hosts call it after their repository operations rather than driving git themselves.
/// </summary>
public static class RepositoryGit {

	/// <summary>Creates the git repository and its .gitignore at <paramref name="repositoryPath"/>.</summary>
	public static async Task InitializeAsync(string repositoryPath, LocalNotionPathProfile pathProfile, ILogger logger, CancellationToken cancellationToken = default) {
		Guard.ArgumentNotNullOrWhitespace(repositoryPath, nameof(repositoryPath));
		Guard.ArgumentNotNull(pathProfile, nameof(pathProfile));
		logger ??= new NoOpLogger();
		var git = new GitSentry(repositoryPath);
		if (!await git.Init(cancellationToken)) {
			logger.Error($"git failed with error:{Environment.NewLine}{git.Output.Tabbify()}");
			throw new InvalidOperationException("Unable to create git repository");
		}
		// Local Notion's own state stays out of git; themes and rendered content are kept.
		await File.WriteAllTextAsync(Path.Combine(repositoryPath, ".gitignore"), GitIgnoreGenerator.Generate(pathProfile), cancellationToken);
	}

	/// <summary>Commits every change in a git-enabled repository and pushes it when the repository's settings say so.</summary>
	/// <returns>False when git is unavailable or a git command failed; the failure is logged.</returns>
	public static async Task<bool> CommitChangesAsync(ILocalNotionRepository repository, ILogger logger, CancellationToken cancellationToken = default) {
		Guard.ArgumentNotNull(repository, nameof(repository));
		Guard.Ensure(repository.GitSettings.Enabled, "Git tracking is not enabled on this repository");
		logger ??= new NoOpLogger();
		var git = new GitSentry(repository.Paths.GetRepositoryPath(FileSystemPathType.Absolute));
		if (!await git.TestGitInstalled(cancellationToken)) {
			logger.Error("Unable to track changes as git is not installed");
			return false;
		}

		logger.Info("Adding changes to git");
		if (!await git.AddAll(cancellationToken)) {
			logger.Error($"git failed with error:{Environment.NewLine}{git.Output.Tabbify()}");
			return false;
		}

		logger.Info("Committing changes to git");
		if (!await git.Commit($"Content updates: {DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}", cancellationToken)) {
			if (git.Output.Contains("nothing to commit", StringComparison.OrdinalIgnoreCase)) {
				logger.Info("No git changes to commit");
				return true;
			}
			logger.Error($"git failed with error:{Environment.NewLine}{git.Output.Tabbify()}");
			return false;
		}

		if (repository.GitSettings.Push) {
			logger.Info("Pushing git changes to default remote");
			if (!await git.Push(cancellationToken)) {
				logger.Error($"git failed with error:{Environment.NewLine}{git.Output.Tabbify()}");
				return false;
			}
		}

		return true;
	}
}
