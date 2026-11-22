// Copyright (c) Herman Schoenfeld 2018 - Present. All rights reserved. (https://sphere10.com/products/localnotion)
// Author: Herman Schoenfeld <herman@sphere10.com>
//
// Distributed under the GPLv3 software license, see the accompanying file LICENSE
// or visit https://github.com/HermanSchoenfeld/localnotion/blob/master/LICENSE
//
// This notice must not be removed when duplicating this file or its contents, in whole or in part.

using System;
using System.IO;
using System.Net;
using NUnit.Framework;

namespace LocalNotion.Core.Tests;

[TestFixture]
[Category("Integration")]
[FixtureLifeCycle(LifeCycle.InstancePerTestCase)]
[Parallelizable(ParallelScope.Children)]
public class RepositoryRenderComparisonTests {
	private string _fixtureRoot;
	private string _beforeRoot;
	private string _afterRoot;

	private static string OriginalAssetUrl => "https://cdn.jsdelivr.net/gh/sphere10/cdn/local-notion/themes/default/resources/site.css";

	private static string RelocatedAssetUrl => "/render-assets/assets/example/resources/site.css";

	[SetUp]
	public void SetUp() {
		_fixtureRoot = Path.Combine(Path.GetTempPath(), "localnotion-comparison-tests-" + Guid.NewGuid().ToString("N"));
		_beforeRoot = Path.Combine(_fixtureRoot, "before");
		_afterRoot = Path.Combine(_fixtureRoot, "after");
		Directory.CreateDirectory(_beforeRoot);
		Directory.CreateDirectory(_afterRoot);
	}

	[TearDown]
	public void TearDown() {
		var fullPath = Path.GetFullPath(_fixtureRoot);
		if (!fullPath.StartsWith(Path.GetFullPath(Path.GetTempPath()), StringComparison.OrdinalIgnoreCase))
			throw new InvalidOperationException("Fixture path escaped its temporary parent.");
		if (Directory.Exists(fullPath))
			Directory.Delete(fullPath, true);
	}

	[Test]
	public void IdenticalDocumentsMatchAtEveryComparisonLevel() {
		const string html = "<!DOCTYPE html><html><head><title>Example</title></head><body><p>Identical</p></body></html>";
		var result = Compare(html, html);
		Assert.That(result.ExactMatch, Is.True);
		Assert.That(result.DomMatch, Is.True);
		Assert.That(result.EquivalentMatch, Is.True);
	}

	[TestCase("<p>Before</p>", "<p>After</p>", TestName = "ChangedTextIsNotEquivalent")]
	[TestCase("<a href=\"/first\">Link</a>", "<a href=\"/second\">Link</a>", TestName = "ChangedLinkIsNotEquivalent")]
	[TestCase("<p class=\"first\">Text</p>", "<p class=\"second\">Text</p>", TestName = "ChangedClassIsNotEquivalent")]
	[TestCase("<pre><code>a  b</code></pre>", "<pre><code>a b</code></pre>", TestName = "ChangedCodeWhitespaceIsNotEquivalent")]
	[TestCase("<script>const text = 'a b';</script>", "<script>const text = 'ab';</script>", TestName = "ChangedScriptIsNotEquivalent")]
	[TestCase("<p><b>one </b><i>two</i></p>", "<p><b>one</b><i>two</i></p>", TestName = "InlineBoundarySpaceIsSignificant")]
	[TestCase("<p><b>one</b>\n<i>two</i></p>", "<p><b>one</b><i>two</i></p>", TestName = "InlineBoundaryNewlineIsSignificant")]
	[TestCase("<p><b>one</b>\n<!--comment--><i>two</i></p>", "<p><b>one</b><!--comment--><i>two</i></p>", TestName = "InlineNewlineBeforeCommentIsSignificant")]
	[TestCase("<p><b>one</b><!--comment-->\n<i>two</i></p>", "<p><b>one</b><!--comment--><i>two</i></p>", TestName = "InlineNewlineAfterCommentIsSignificant")]
	[TestCase("<p><b>one</b>\n<!--first--><!--second-->\n<i>two</i></p>", "<p><b>one</b><!--first--><!--second--><i>two</i></p>", TestName = "InlineNewlinesAroundCommentsAreSignificant")]
	[TestCase("<p>one<!--comment-->\n<i>two</i></p>", "<p>one<!--comment--><i>two</i></p>", TestName = "TextBoundaryNewlineAfterCommentIsSignificant")]
	[TestCase("<p>one&nbsp;two</p>", "<p>one two</p>", TestName = "NonBreakingSpaceIsSignificant")]
	[TestCase("<p><img src=\"one.png\">\n<img src=\"two.png\"></p>", "<p><img src=\"one.png\"><img src=\"two.png\"></p>", TestName = "InlineImageBoundaryNewlineIsSignificant")]
	[TestCase("<p>&nbsp;\n</p>", "<p></p>", TestName = "WhitespaceNodeContainingNonBreakingSpaceIsSignificant")]
	public void ContentDifferencesRemainVisible(string before, string after) {
		Assert.That(Compare(before, after).EquivalentMatch, Is.False);
	}

	[Test]
	public void AttributeOrderingIsEquivalent() {
		var result = Compare("<p id=\"item\" class=\"example\">Text</p>", "<p class=\"example\" id=\"item\">Text</p>");
		Assert.That(result.ExactMatch, Is.False);
		Assert.That(result.EquivalentMatch, Is.True);
	}

	[Test]
	public void RemovingDoctypeIsNotEquivalent() {
		var result = Compare("<!DOCTYPE html><html><body><p>Text</p></body></html>", "<html><body><p>Text</p></body></html>");
		Assert.That(result.DomMatch, Is.False, "The doctype determines browser quirks mode.");
		Assert.That(result.EquivalentMatch, Is.False);
	}

	[TestCase("ul", "")]
	[TestCase("ol", " start=\"1\"")]
	public void GeneratedWrapperCanReplaceInheritedDuplicateId(string listTag, string newAttributes) {
		var before = $"<main id=\"page\"><{listTag} id=\"page\" class=\"ln-color-{{color}}\"><li id=\"item\">Text</li></{listTag}></main>";
		var after = $"<main id=\"page\"><{listTag} id=\"visual_1\" class=\"ln-color-default\"{newAttributes}><li id=\"item\">Text</li></{listTag}></main>";
		var result = Compare(before, after);
		Assert.That(result.EquivalentMatch, Is.True);
		Assert.That(result.GeneratedListContainers, Is.EqualTo(1));
	}

	[Test]
	public void ChangedItemAnchorIsNotEquivalent() {
		var before = "<main id=\"page\"><ul id=\"page\"><li id=\"source-item\">Text</li></ul></main>";
		var after = "<main id=\"page\"><ul id=\"visual_1\"><li id=\"different-item\">Text</li></ul></main>";
		Assert.That(Compare(before, after).EquivalentMatch, Is.False);
	}

	[Test]
	public void GeneratedWrapperMustNotHideLossOfUniqueListAnchor() {
		var before = "<a href=\"#source-list\">List</a><ul id=\"source-list\"><li>Text</li></ul>";
		var after = "<a href=\"#source-list\">List</a><ul id=\"visual_1\"><li>Text</li></ul>";
		Assert.That(Compare(before, after).EquivalentMatch, Is.False);
	}

	[TestCase(false)]
	[TestCase(true)]
	public void RelocatedAssetsWithIdenticalBytesAreEquivalent(bool legacy) {
		var relocatedUrl = legacy ? "/.localnotion" + RelocatedAssetUrl : RelocatedAssetUrl;
		CreateAssets("p { color: blue; }", "p { color: blue; }", relocatedUrl);
		var result = Compare("<link rel=\"stylesheet\" href=\"" + OriginalAssetUrl + "\">", "<link rel=\"stylesheet\" href=\"" + relocatedUrl + "\">");
		Assert.That(result.ExactMatch, Is.False);
		Assert.That(result.EquivalentMatch, Is.True);
		Assert.That(result.EquivalentAssets, Is.EqualTo(1));
	}

	[Test]
	public void RelocatedAssetsWithDifferentBytesAreNotEquivalent() {
		CreateAssets("p { color: blue; }", "p { color: red; }");
		var result = Compare("<link rel=\"stylesheet\" href=\"" + OriginalAssetUrl + "\">", "<link rel=\"stylesheet\" href=\"" + RelocatedAssetUrl + "\">");
		Assert.That(result.EquivalentMatch, Is.False);
		Assert.That(result.EquivalentAssets, Is.EqualTo(0));
	}

	[TestCase("#first", "#second")]
	[TestCase("?variant=first", "?variant=second")]
	public void AssetUrlSuffixChangesRemainVisible(string beforeSuffix, string afterSuffix) {
		CreateAssets("identical asset", "identical asset");
		var before = "<img src=\"" + OriginalAssetUrl + beforeSuffix + "\">";
		var after = "<img src=\"" + RelocatedAssetUrl + afterSuffix + "\">";
		Assert.That(Compare(before, after).EquivalentMatch, Is.False);
	}

	[Test]
	public void AssetUrlsInLiteralAttributesAreNotNormalized() {
		CreateAssets("identical asset", "identical asset");
		var result = Compare("<p title=\"" + OriginalAssetUrl + "\">Text</p>", "<p title=\"" + RelocatedAssetUrl + "\">Text</p>");
		Assert.That(result.EquivalentMatch, Is.False, "A tooltip is visible content rather than an asset reference.");
	}

	[Test]
	public void BlockIndentationAroundCommentsIsEquivalent() {
		var result = Compare("<div>one</div>\n<!--comment-->\n<div>two</div>", "<div>one</div><!--comment--><div>two</div>");
		Assert.That(result.EquivalentMatch, Is.True);
	}

	[TestCase("--caption:'{url}'", TestName = "CssSingleQuotedAssetTextIsNotNormalized")]
	[TestCase("--caption:\"{url}\"", TestName = "CssDoubleQuotedAssetTextIsNotNormalized")]
	[TestCase("--caption:{url}", TestName = "CssBareAssetTextIsNotNormalized")]
	[TestCase("content:\"url('{url}')\"", TestName = "CssDoubleQuotedUrlFunctionTextIsNotNormalized")]
	[TestCase("content:'url(\"{url}\")'", TestName = "CssSingleQuotedUrlFunctionTextIsNotNormalized")]
	[TestCase("/* background:url('{url}') */ color:blue", TestName = "CssCommentAssetTextIsNotNormalized")]
	[TestCase("--caption:'escaped \\' quote url(\"{url}\")'", TestName = "CssEscapedQuoteAssetTextIsNotNormalized")]
	[TestCase("content:'unterminated url(\"{url}\")", TestName = "CssUnterminatedStringAssetTextIsNotNormalized")]
	public void AssetUrlsInCssLiteralContentAreNotNormalized(string style) {
		CreateAssets("identical asset", "identical asset");
		var beforeStyle = style.Replace("{url}", OriginalAssetUrl, StringComparison.Ordinal);
		var afterStyle = style.Replace("{url}", RelocatedAssetUrl, StringComparison.Ordinal);
		var result = Compare("<p style=\"" + WebUtility.HtmlEncode(beforeStyle) + "\">Text</p>", "<p style=\"" + WebUtility.HtmlEncode(afterStyle) + "\">Text</p>");
		Assert.That(result.EquivalentMatch, Is.False);
		Assert.That(result.EquivalentAssets, Is.EqualTo(0));
	}

	[TestCase("background-image:url('{url}')")]
	[TestCase("background-image:url(\"{url}\")")]
	[TestCase("background-image:url({url})")]
	[TestCase("background-image:URL( '{url}' )")]
	public void AssetsInCssUrlFunctionsWithIdenticalBytesAreEquivalent(string style) {
		CreateAssets("identical asset", "identical asset");
		var beforeStyle = style.Replace("{url}", OriginalAssetUrl, StringComparison.Ordinal);
		var afterStyle = style.Replace("{url}", RelocatedAssetUrl, StringComparison.Ordinal);
		var result = Compare("<p style=\"" + WebUtility.HtmlEncode(beforeStyle) + "\">Text</p>", "<p style=\"" + WebUtility.HtmlEncode(afterStyle) + "\">Text</p>");
		Assert.That(result.EquivalentMatch, Is.True);
		Assert.That(result.EquivalentAssets, Is.EqualTo(1));
	}

	[Test]
	public void AssetUrlEmbeddedInAnotherLinkIsNotNormalized() {
		CreateAssets("identical asset", "identical asset");
		var result = Compare("<a href=\"/show?caption=" + OriginalAssetUrl + "\">Link</a>", "<a href=\"/show?caption=" + RelocatedAssetUrl + "\">Link</a>");
		Assert.That(result.EquivalentMatch, Is.False);
		Assert.That(result.EquivalentAssets, Is.EqualTo(0));
	}

	private void CreateAssets(string beforeContent, string afterContent, string relocatedUrl = null) {
		var beforeFile = Path.Combine(_beforeRoot, ".localnotion", "themes", "default", "resources", "site.css");
		var afterFile = Path.GetFullPath((relocatedUrl ?? RelocatedAssetUrl).TrimStart('/'), _afterRoot);
		Directory.CreateDirectory(Path.GetDirectoryName(beforeFile));
		Directory.CreateDirectory(Path.GetDirectoryName(afterFile));
		File.WriteAllText(beforeFile, beforeContent);
		File.WriteAllText(afterFile, afterContent);
	}

	private RepositoryRenderComparison Compare(string beforeHtml, string afterHtml) {
		File.WriteAllText(Path.Combine(_beforeRoot, "page.html"), beforeHtml);
		File.WriteAllText(Path.Combine(_afterRoot, "page.html"), afterHtml);
		var result = new RepositoryRenderComparison { BeforePath = "page.html", AfterPath = "page.html" };
		result.Compare(_beforeRoot, _afterRoot);
		return result;
	}
}
