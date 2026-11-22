// Copyright (c) Herman Schoenfeld 2018 - Present. All rights reserved. (https://sphere10.com/products/localnotion)
// Author: Herman Schoenfeld <herman@sphere10.com>
//
// Distributed under the GPLv3 software license, see the accompanying file LICENSE
// or visit https://github.com/HermanSchoenfeld/localnotion/blob/master/LICENSE
//
// This notice must not be removed when duplicating this file or its contents, in whole or in part.

using System;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using Notion.Client;
using NUnit.Framework;

namespace LocalNotion.Core.Tests;

[TestFixture]
[Category("Integration")]
[NonParallelizable]
public class CMSDatabaseHttpTests {

	[Test]
	public async Task ActualSDKHTTPCompletesPresetsWithThePublishedAPILimit() {
		var state = new RecordingNotionTransport();
		await using var server = new LoopbackNotionServer(state);
		var client = NotionClientFactory.Create(new ClientOptions { AuthToken = "synthetic-loopback-key", BaseUrl = server.BaseUrl });
		var result = await new CMSDatabaseCreator(client, new CMSDatabaseTemplate(), TimeSpan.Zero)
			.CreateAsync(Guid.NewGuid(), "New website");
		Assert.That(result.Id, Is.EqualTo(state.Response.Id));
		Assert.That(server.NotionVersion, Is.EqualTo("2026-03-11"));
		Assert.That(state.CreateCount, Is.EqualTo(1));
		Assert.That(state.PageCreateCount, Is.EqualTo(1));
		Assert.That(state.PageUpdateCount, Is.EqualTo(1));
		Assert.That(state.ArchiveCount, Is.EqualTo(1));
		Assert.That(state.QueryCount, Is.EqualTo(1));
		Assert.That(state.SavedSchema["Themes"]["multi_select"]["options"].Count(), Is.EqualTo(235));
		Assert.That(state.SetupPageArchived, Is.True);
	}

	[Test]
	public async Task ActualSDKHTTPFailureStillArchivesAndReportsTheKnownPartialDatabase() {
		var state = new RecordingNotionTransport {
			PageUpdateError = new NotionApiException(HttpStatusCode.BadRequest, null, "synthetic invalid update")
		};
		await using var server = new LoopbackNotionServer(state);
		var client = NotionClientFactory.Create(new ClientOptions { AuthToken = "synthetic-loopback-key", BaseUrl = server.BaseUrl });
		await Assert.ThatAsync(() => new CMSDatabaseCreator(client, new CMSDatabaseTemplate(), TimeSpan.Zero)
			.CreateAsync(Guid.NewGuid(), "New website"), Throws.TypeOf<CMSDatabaseCreationException>()
			.With.Property("DatabaseID").EqualTo(Guid.Parse(state.Response.Id))
			.And.Property("SetupPageArchived").True.And.InnerException.TypeOf<NotionApiException>());
		Assert.That(state.CreateCount, Is.EqualTo(1));
		Assert.That(state.PageCreateCount, Is.EqualTo(1));
		Assert.That(state.ArchiveCount, Is.EqualTo(1));
		Assert.That(state.SetupPageArchived, Is.True);
	}

	[Test]
	public async Task ActualSDKHTTPCancellationStillCompletesCleanupOverASeparateRequest() {
		var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
		var pending = new TaskCompletionSource<Page>(TaskCreationOptions.RunContinuationsAsynchronously);
		var state = new RecordingNotionTransport { PendingPageUpdate = token => {
			entered.SetResult();
			return pending.Task.WaitAsync(token);
		} };
		await using var server = new LoopbackNotionServer(state);
		var client = NotionClientFactory.Create(new ClientOptions { AuthToken = "synthetic-loopback-key", BaseUrl = server.BaseUrl });
		using var cancellation = new CancellationTokenSource();
		var creation = new CMSDatabaseCreator(client, new CMSDatabaseTemplate(), TimeSpan.Zero)
			.CreateAsync(Guid.NewGuid(), "New website", cancellation.Token);
		await entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
		cancellation.Cancel();
		await Assert.ThatAsync(async () => await creation, Throws.TypeOf<CMSDatabaseCreationException>()
			.With.Property("SetupPageArchived").True.And.InnerException.InstanceOf<OperationCanceledException>());
		Assert.That(state.CreateCount, Is.EqualTo(1));
		Assert.That(state.PageCreateCount, Is.EqualTo(1));
		Assert.That(state.ArchiveCount, Is.EqualTo(1));
		Assert.That(state.SetupPageArchived, Is.True);
	}
}
