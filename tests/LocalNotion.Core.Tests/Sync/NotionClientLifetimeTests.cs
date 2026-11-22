// Copyright (c) Herman Schoenfeld 2018 - Present. All rights reserved. (https://sphere10.com/products/localnotion)
// Author: Herman Schoenfeld <herman@sphere10.com>
//
// Distributed under the GPLv3 software license, see the accompanying file LICENSE
// or visit https://github.com/HermanSchoenfeld/localnotion/blob/master/LICENSE
//
// This notice must not be removed when duplicating this file or its contents, in whole or in part.

using System;
using System.Net;
using System.Net.Http;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using Notion.Client;
using NUnit.Framework;

namespace LocalNotion.Core.Tests;

[TestFixture]
[Category("Unit")]
public class NotionClientLifetimeTests {

	[TestCase(200)]
	[TestCase(400)]
	public async Task OwnedClientClosesItsKeepAliveConnectionAfterEveryRequest(int status) {
		using var listener = new TcpListener(IPAddress.Loopback, 0);
		listener.Start();
		using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(10));
		var endpoint = (IPEndPoint)listener.LocalEndpoint;
		var client = new RestClient(new ClientOptions { BaseUrl = $"http://127.0.0.1:{endpoint.Port}/" });
		for (var request = 0; request < 2; request++) {
			var connectionClosed = ObserveConnection();
			if (status == 200)
				await client.GetAsync<JObject>("test", cancellationToken: deadline.Token);
			else
				Assert.That(async () => await client.GetAsync<JObject>("test", cancellationToken: deadline.Token), Throws.InstanceOf<NotionApiException>());
			Assert.That(await connectionClosed, Is.True, "The SDK must dispose its own client, not retain the keep-alive connection.");
		}

		async Task<bool> ObserveConnection() {
			using var connection = await listener.AcceptTcpClientAsync(deadline.Token);
			using var stream = connection.GetStream();
			var buffer = new byte[4096];
			var headers = new StringBuilder();
			while (!headers.ToString().Contains("\r\n\r\n", StringComparison.Ordinal)) {
				var count = await stream.ReadAsync(buffer, deadline.Token);
				Assert.That(count, Is.GreaterThan(0));
				headers.Append(Encoding.ASCII.GetString(buffer, 0, count));
			}
			var body = status == 200 ? "{}" : "{\"object\":\"error\",\"code\":\"validation_error\",\"message\":\"test\"}";
			var response = $"HTTP/1.1 {status} Test\r\nContent-Type: application/json\r\nContent-Length: {Encoding.UTF8.GetByteCount(body)}\r\nConnection: keep-alive\r\n\r\n{body}";
			await stream.WriteAsync(Encoding.UTF8.GetBytes(response), deadline.Token);
			// The server deliberately keeps the socket open. EOF must come from client disposal.
			return await stream.ReadAsync(buffer, deadline.Token) == 0;
		}
	}

	[Test]
	public async Task InjectedClientRemainsOwnedByItsCaller() {
		using var handler = new TrackingHandler();
		using var http = new HttpClient(handler);
		var client = new RestClient(new ClientOptions { BaseUrl = "http://example.invalid/", HttpClient = http });
		await client.GetAsync<JObject>("first");
		await client.GetAsync<JObject>("second");
		Assert.That(handler.Disposed, Is.False);
		http.Dispose();
		Assert.That(handler.Disposed, Is.True);
	}

	private sealed class TrackingHandler : HttpMessageHandler {
		public bool Disposed { get; private set; }

		protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
			=> Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("{}") });

		protected override void Dispose(bool disposing) {
			Disposed = true;
			base.Dispose(disposing);
		}
	}
}
