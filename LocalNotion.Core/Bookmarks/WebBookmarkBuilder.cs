// Copyright (c) Herman Schoenfeld 2018 - Present. All rights reserved. (https://sphere10.com/products/localnotion)
// Author: Herman Schoenfeld <herman@sphere10.com>
//
// Distributed under the GPLv3 software license, see the accompanying file LICENSE
// or visit https://github.com/HermanSchoenfeld/localnotion/blob/master/LICENSE
//
// This notice must not be removed when duplicating this file or its contents, in whole or in part.

using System.Net.Http;
using System.Text;
using HtmlAgilityPack;
using Sphere10.Framework;

namespace LocalNotion.Core;

/// <summary>Materializes public HTML preview metadata; persistence and pull-session reuse belong to the caller.</summary>
public class WebBookmarkBuilder : IBookmarkBuilder {

	private const int MaximumResponseBytes = 2 * 1024 * 1024;
	private readonly HttpClient _client;

	public WebBookmarkBuilder(HttpClient client = null) => _client = client;

	public async Task<LocalNotionBookmark> Build(string url, CancellationToken cancellationToken = default) {
		Guard.ArgumentNotNullOrWhitespace(url, nameof(url));
		cancellationToken.ThrowIfCancellationRequested();
		if (!Uri.TryCreate(url, UriKind.Absolute, out var address) || address.Scheme is not ("http" or "https"))
			return new LocalNotionBookmark { Url = url, Title = url };
		using var ownedClient = _client is null ? new HttpClient { Timeout = TimeSpan.FromSeconds(15) } : null;
		var client = _client ?? ownedClient;
		using var request = new HttpRequestMessage(HttpMethod.Get, address);
		request.Headers.UserAgent.ParseAdd("LocalNotion/1.0 (bookmark preview)");
		using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
		deadline.CancelAfter(TimeSpan.FromSeconds(15));
		using var response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, deadline.Token);
		response.EnsureSuccessStatusCode();
		var contentType = response.Content.Headers.ContentType;
		if (contentType?.MediaType is { } mediaType && !mediaType.Equals("text/html", StringComparison.OrdinalIgnoreCase) && !mediaType.Equals("application/xhtml+xml", StringComparison.OrdinalIgnoreCase))
			return new LocalNotionBookmark { Url = url, Title = url };
		if (response.Content.Headers.ContentLength > MaximumResponseBytes)
			throw new InvalidDataException("Bookmark HTML exceeds the preview size limit.");
		await using var stream = await response.Content.ReadAsStreamAsync(deadline.Token);
		using var buffer = new MemoryStream();
		var chunk = new byte[16384];
		int count;
		while ((count = await stream.ReadAsync(chunk, deadline.Token)) > 0) {
			if (buffer.Length + count > MaximumResponseBytes)
				throw new InvalidDataException("Bookmark HTML exceeds the preview size limit.");
			buffer.Write(chunk, 0, count);
		}
		var encoding = Encoding.UTF8;
		if (!string.IsNullOrWhiteSpace(contentType?.CharSet)) {
			try {
				encoding = Encoding.GetEncoding(contentType.CharSet.Trim('"'));
			} catch (ArgumentException) {
				// Unrecognized server charset: retain the UTF-8 fallback.
			}
		}
		return Parse(url, response.RequestMessage?.RequestUri ?? address, encoding.GetString(buffer.ToArray()));
	}

	public static LocalNotionBookmark Parse(string url, Uri address, string html) {
		Guard.ArgumentNotNull(address, nameof(address));
		Guard.ArgumentNotNull(html, nameof(html));
		var document = new HtmlDocument();
		document.LoadHtml(html);
		// Preserve document order within repeated properties (Open Graph's first-value rule).
		var metadata = document.DocumentNode.Descendants("meta")
			.Select(node => new KeyValuePair<string, string>(
				Clean(node.GetAttributeValue("property", null)) ?? Clean(node.GetAttributeValue("name", null)) ?? string.Empty,
				Clean(node.GetAttributeValue("content", null))))
			.ToLookup(item => item.Key, item => item.Value, StringComparer.OrdinalIgnoreCase);
		IEnumerable<string> Values(params string[] names) => names.SelectMany(name => metadata[name]).Where(value => value != null);
		var baseHref = Clean(document.DocumentNode.SelectSingleNode("//base[@href]")?.GetAttributeValue("href", null));
		var baseUrl = WebUrl(address, baseHref) is { } resolvedBase ? new Uri(resolvedBase) : address;
		var links = document.DocumentNode.Descendants("link").ToArray();
		IEnumerable<string> Links(string relation) => links
			.Where(node => node.GetAttributeValue("rel", "").Split((char[])null, StringSplitOptions.RemoveEmptyEntries).Contains(relation, StringComparer.OrdinalIgnoreCase))
			.Select(node => Clean(node.GetAttributeValue("href", null)));
		string FirstUrl(IEnumerable<string> values) => values.Select(value => WebUrl(baseUrl, value)).FirstOrDefault(value => value != null);

		// Preview tags win; ordinary HTML supplies the fallback. Never change the authored destination to og:url.
		return new LocalNotionBookmark {
			Url = url,
			Title = Values("og:title", "twitter:title").FirstOrDefault() ?? Clean(document.DocumentNode.SelectSingleNode("//title")?.InnerText) ?? url,
			Summary = Values("og:description", "twitter:description", "description").FirstOrDefault(),
			ImageUrl = FirstUrl(Values("og:image", "og:image:url", "og:image:secure_url", "twitter:image", "twitter:image:src").Concat(Links("image_src"))),
			IconUrl = FirstUrl(Links("icon").Concat(Links("apple-touch-icon"))) ?? new Uri(address, "/favicon.ico").AbsoluteUri
		};
	}

	private static string Clean(string value)
		=> string.IsNullOrWhiteSpace(value) ? null : HtmlEntity.DeEntitize(value).Trim();

	private static string WebUrl(Uri address, string value)
		=> !string.IsNullOrWhiteSpace(value) && Uri.TryCreate(address, value, out var result) && result.Scheme is "http" or "https"
			? result.AbsoluteUri : null;
}
