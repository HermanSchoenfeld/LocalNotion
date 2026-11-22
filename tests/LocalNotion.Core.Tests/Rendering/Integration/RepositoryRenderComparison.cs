// Copyright (c) Herman Schoenfeld 2018 - Present. All rights reserved. (https://sphere10.com/products/localnotion)
// Author: Herman Schoenfeld <herman@sphere10.com>
//
// Distributed under the GPLv3 software license, see the accompanying file LICENSE
// or visit https://github.com/HermanSchoenfeld/localnotion/blob/master/LICENSE
//
// This notice must not be removed when duplicating this file or its contents, in whole or in part.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using AngleSharp.Dom;
using AngleSharp.Html.Parser;
using Newtonsoft.Json;
using Sphere10.Framework;

namespace LocalNotion.Core.Tests;

public class RepositoryRenderComparison {
	public string Id { get; set; }

	public string Category { get; set; }

	public string BeforePath { get; set; }

	public string AfterPath { get; set; }

	public string BeforeHash { get; set; }

	public string AfterHash { get; set; }

	public bool ExactMatch { get; set; }

	public bool DomMatch { get; set; }

	public bool EquivalentMatch { get; set; }

	public int EquivalentAssets { get; set; }

	public int GeneratedListContainers { get; set; }

	public void Compare(string baseline, string rendered) {
		var beforeFile = Path.GetFullPath(BeforePath, baseline);
		var afterFile = Path.GetFullPath(AfterPath, rendered);
		var beforeBytes = File.ReadAllBytes(beforeFile);
		var afterBytes = File.ReadAllBytes(afterFile);
		BeforeHash = SHA256.HashData(beforeBytes).ToHexString(true).ToUpperInvariant();
		AfterHash = SHA256.HashData(afterBytes).ToHexString(true).ToUpperInvariant();
		ExactMatch = beforeBytes.AsSpan().SequenceEqual(afterBytes);
		var parser = new HtmlParser();
		var before = parser.ParseDocument(File.ReadAllText(beforeFile));
		var after = parser.ParseDocument(File.ReadAllText(afterFile));
		DomMatch = before.DocumentElement.OuterHtml == after.DocumentElement.OuterHtml && Canonical(before.Doctype) == Canonical(after.Doctype);
		var beforeAssets = NormalizeAssets(before, baseline);
		var afterAssets = NormalizeAssets(after, rendered);
		EquivalentAssets = beforeAssets.Intersect(afterAssets).Count();
		NormalizeListContainers(before, after);
		var beforeCanonical = Canonical(before);
		var afterCanonical = Canonical(after);
		EquivalentMatch = beforeCanonical == afterCanonical;
		WriteCanonical(baseline, BeforePath, beforeCanonical);
		WriteCanonical(rendered, AfterPath, afterCanonical);
	}

	private void NormalizeListContainers(IDocument before, IDocument after) {
		var beforeLists = before.QuerySelectorAll("ul,ol");
		var afterLists = after.QuerySelectorAll("ul,ol");
		if (beforeLists.Length != afterLists.Length)
			return;
		for (var index = 0; index < beforeLists.Length; index++) {
			var oldList = beforeLists[index];
			var newList = afterLists[index];
			// Source item anchors remain exact. Only generated list wrappers replace inherited duplicate IDs.
			if (oldList.LocalName != newList.LocalName || !Regex.IsMatch(newList.Id ?? "", @"^visual_\d+$") ||
				!string.IsNullOrEmpty(oldList.Id) && before.All.Count(element => element.Id == oldList.Id) < 2)
				continue;
			oldList.RemoveAttribute("id");
			newList.RemoveAttribute("id");
			if (oldList.ClassName?.Contains("ln-color-{color}", StringComparison.Ordinal) == true)
				oldList.ClassName = oldList.ClassName.Replace("ln-color-{color}", "ln-color-default", StringComparison.Ordinal);
			if (newList.GetAttribute("start") == "1" && !oldList.HasAttribute("start"))
				newList.RemoveAttribute("start");
			GeneratedListContainers++;
		}
	}

	private static HashSet<string> NormalizeAssets(IDocument document, string root) {
		var hashes = new HashSet<string>(StringComparer.Ordinal);
		foreach (var element in document.All)
			foreach (var attribute in element.Attributes.Where(attribute => attribute.Name is "src" or "href" or "poster" or "style").ToArray()) {
				var value = attribute.Name == "style"
					? NormalizeStyleAssets(attribute.Value, root, hashes)
					: NormalizeAssetUrl(attribute.Value, root, hashes);
				element.SetAttribute(attribute.Name, value);
			}
		return hashes;
	}

	private static string NormalizeStyleAssets(string style, string root, HashSet<string> hashes) {
		// Consume CSS comments and strings before considering url() functions, so literal content stays exact.
		return Regex.Replace(style,
			@"(?<ignored>/\*[\s\S]*?(?:\*/|\z)|""(?:\\[\s\S]|[^""\\])*(?:""|\z)|'(?:\\[\s\S]|[^'\\])*(?:'|\z))|(?<![\w\\#-])url\([ \t\r\n\f]*(?:""(?<url>[^""\\]*)""|'(?<url>[^'\\]*)'|(?<url>[^ \t\r\n\f""'()\\]*))[ \t\r\n\f]*\)",
			match => {
				var url = match.Groups["url"];
				if (!url.Success)
					return match.Value;
				var start = url.Index - match.Index;
				return match.Value[..start] + NormalizeAssetUrl(url.Value, root, hashes) + match.Value[(start + url.Length)..];
			}, RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
	}

	private static string NormalizeAssetUrl(string url, string root, HashSet<string> hashes) {
		var match = Regex.Match(url, @"\A(?:https://cdn\.jsdelivr\.net/gh/sphere10/cdn/local-notion/themes/([^\s'""<>]+)|/((?:\.localnotion/)?render-assets/[^\s'""<>]+))\z");
		if (!match.Success)
			return url;
		var relative = match.Groups[1].Success ? ".localnotion/themes/" + match.Groups[1].Value : match.Groups[2].Value;
		var path = Path.GetFullPath(Uri.UnescapeDataString(relative.Split('?', '#')[0]), root);
		if (!path.StartsWith(Path.TrimEndingDirectorySeparator(Path.GetFullPath(root)) + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase) || !File.Exists(path))
			return url;
		var hash = SHA256.HashData(File.ReadAllBytes(path)).ToHexString(true).ToUpperInvariant();
		hashes.Add(hash);
		var suffixIndex = url.IndexOfAny(['?', '#']);
		return "asset-sha256:" + hash + (suffixIndex < 0 ? "" : url[suffixIndex..]);
	}

	private static string Canonical(INode root) {
		var result = new StringBuilder();
		Visit(root, false);
		return result.ToString();

		void Visit(INode node, bool preserveWhitespace) {
			if (node is IDocument) {
				foreach (var child in node.ChildNodes)
					Visit(child, preserveWhitespace);
			} else if (node is IDocumentType doctype) {
				result.Append("<!DOCTYPE ").Append(doctype.Name).Append(' ').Append(JsonConvert.SerializeObject(doctype.PublicIdentifier))
					.Append(' ').Append(JsonConvert.SerializeObject(doctype.SystemIdentifier)).AppendLine(">");
			} else if (node is IElement element) {
				preserveWhitespace |= element.LocalName is "pre" or "code" or "textarea" or "script" or "style";
				result.Append('<').Append(element.LocalName);
				foreach (var attribute in element.Attributes.OrderBy(attribute => attribute.Name, StringComparer.Ordinal))
					result.Append(' ').Append(attribute.Name).Append('=').Append(JsonConvert.SerializeObject(attribute.Value));
				result.AppendLine(">");
				foreach (var child in node.ChildNodes)
					Visit(child, preserveWhitespace);
				result.Append("</").Append(element.LocalName).AppendLine(">");
			} else if (node is IText text) {
				var value = preserveWhitespace ? text.Data.Replace("\r\n", "\n", StringComparison.Ordinal) : Regex.Replace(text.Data, @"[ \t\r\n]+", " ");
				if (!preserveWhitespace && text.Data.All(character => character is ' ' or '\t' or '\r' or '\n') && text.Data.IndexOfAny(['\r', '\n']) >= 0 &&
					!(HasInlineNeighbor(text, true) && HasInlineNeighbor(text, false)))
					return;
				if (value.Length > 0)
					result.AppendLine(JsonConvert.SerializeObject(value));
			} else if (node is IComment comment)
				result.Append("<!--").Append(comment.Data).AppendLine("-->");
		}
	}

	private static bool HasInlineNeighbor(INode node, bool previous) {
		do {
			node = previous ? node.PreviousSibling : node.NextSibling;
		} while (node is IComment || node is IText text && text.Data.All(character => character is ' ' or '\t' or '\r' or '\n'));
		return node is IText || IsInline(node);
	}

	private static bool IsInline(INode node) => node is IElement element && element.LocalName is not (
		"html" or "head" or "body" or "div" or "section" or "article" or "main" or "header" or "footer" or "nav" or "p" or
		"h1" or "h2" or "h3" or "h4" or "h5" or "h6" or "ul" or "ol" or "li" or "table" or "thead" or "tbody" or "tfoot" or
		"tr" or "td" or "th" or "caption" or "colgroup" or "blockquote" or "pre" or "hr" or "form" or "fieldset" or "figure" or
		"figcaption" or "dl" or "dt" or "dd" or "details" or "summary" or "script" or "style" or "meta" or "link" or "title"
	);

	private static void WriteCanonical(string root, string relative, string canonical) {
		var path = Path.Combine(Path.GetDirectoryName(root), "dom", Path.GetFileName(root), relative + ".txt");
		Directory.CreateDirectory(Path.GetDirectoryName(path));
		File.WriteAllText(path, canonical);
	}
}
