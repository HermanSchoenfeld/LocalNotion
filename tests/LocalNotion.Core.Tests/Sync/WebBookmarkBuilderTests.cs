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
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;

namespace LocalNotion.Core.Tests;

[TestFixture]
[Parallelizable(ParallelScope.Children)]
public class WebBookmarkBuilderTests {

	[Test]
	public void OpenGraphWinsAndKeepsTheAuthoredDestination() {
		var result = Parse("""
			<html><head><base href="/assets/">
			<meta property="og:title" content="Primary &amp; title"><meta property="og:title" content="Second">
			<meta name="twitter:title" content="Twitter"><title>HTML title</title>
			<meta property="og:description" content="Primary description"><meta name="description" content="HTML description">
			<meta property="og:image" content="cover.png"><link rel="shortcut icon" href="icon.png">
			<meta property="og:url" content="https://other.invalid/"></head></html>
			""");
		Assert.That(result.Title, Is.EqualTo("Primary & title"));
		Assert.That(result.Summary, Is.EqualTo("Primary description"));
		Assert.That(result.Url, Is.EqualTo("https://example.test/original"));
		Assert.That(result.ImageUrl, Is.EqualTo("https://example.test/assets/cover.png"));
		Assert.That(result.IconUrl, Is.EqualTo("https://example.test/assets/icon.png"));
	}

	[Test]
	public void EmptyAndUnsafeMetadataFallsThroughToUsableValues() {
		var result = Parse("""
			<meta property="og:title" content=" "><meta property="og:title" content="">
			<meta name="twitter:title" content="Card title"><title>HTML</title>
			<meta name="twitter:description" content="Card description"><meta name="description" content="HTML description">
			<meta property="og:image" content="javascript:alert(1)"><meta name="twitter:image" content="//cdn.example.test/card.webp">
			<link rel="icon" href="data:image/png;base64,bad"><link rel="apple-touch-icon" href="/apple.png">
			""");
		Assert.That(result.Title, Is.EqualTo("Card title"));
		Assert.That(result.Summary, Is.EqualTo("Card description"));
		Assert.That(result.ImageUrl, Is.EqualTo("https://cdn.example.test/card.webp"));
		Assert.That(result.IconUrl, Is.EqualTo("https://example.test/apple.png"));
	}

	[Test]
	public void OrdinaryHtmlAndMissingMetadataHavePredictableFallbacks() {
		var result = Parse("<title> Plain &amp; simple </title><meta name='description' content='About this page'><link rel='image_src' href='/preview.jpg'>");
		Assert.That(result.Title, Is.EqualTo("Plain & simple"));
		Assert.That(result.Summary, Is.EqualTo("About this page"));
		Assert.That(result.ImageUrl, Is.EqualTo("https://example.test/preview.jpg"));
		Assert.That(result.IconUrl, Is.EqualTo("https://example.test/favicon.ico"));
		Assert.That(Parse("<body>No metadata</body>").Title, Is.EqualTo("https://example.test/original"));
	}

	[Test]
	public void HtmlEntitiesAreDecodedOnlyOnceInMetadataUrls() {
		var result = Parse("<meta property='og:image' content='https://example.test/image?a=1&amp;amp;b=2'>");
		Assert.That(result.ImageUrl, Is.EqualTo("https://example.test/image?a=1&amp;b=2"));
	}

	[Test]
	public async Task FetchUsesRedirectDestinationAndDisposesResponseButNotBorrowedClient() {
		using var handler = new PreviewHandler();
		using var client = new HttpClient(handler);
		var builder = new WebBookmarkBuilder(client);
		var result = await builder.Build("https://example.test/original");
		Assert.That(result.ImageUrl, Is.EqualTo("https://redirect.test/news/cover.png"));
		Assert.That(result.Url, Is.EqualTo("https://example.test/original"));
		Assert.That(handler.Content.Disposed, Is.True);
		await builder.Build("https://example.test/original");
		Assert.That(handler.Calls, Is.EqualTo(2), "The builder has no cache beyond the caller's pull scope.");
	}

	[Test]
	public async Task CallerCancellationPreventsFetching() {
		using var handler = new PreviewHandler();
		using var client = new HttpClient(handler);
		await Assert.ThatAsync(async () => await new WebBookmarkBuilder(client).Build("https://example.test/", new CancellationToken(true)), Throws.InstanceOf<OperationCanceledException>());
		Assert.That(handler.Calls, Is.Zero);
	}

	private static LocalNotionBookmark Parse(string html) => WebBookmarkBuilder.Parse("https://example.test/original", new Uri("https://example.test/news/page"), html);

	#region Inner Types
	private sealed class PreviewHandler : HttpMessageHandler {
		public int Calls { get; private set; }
		public PreviewContent Content { get; private set; }
		protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) {
			Calls++;
			Content = new PreviewContent();
			request.RequestUri = new Uri("https://redirect.test/news/page");
			return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { RequestMessage = request, Content = Content });
		}
	}

	private sealed class PreviewContent : StringContent {
		public PreviewContent() : base("<meta property='og:image' content='cover.png'>", System.Text.Encoding.UTF8, "text/html") { }
		public bool Disposed { get; private set; }
		protected override void Dispose(bool disposing) {
			Disposed = true;
			base.Dispose(disposing);
		}
	}
	#endregion
}
