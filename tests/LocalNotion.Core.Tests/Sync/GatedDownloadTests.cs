// Copyright (c) Herman Schoenfeld 2018 - Present. All rights reserved. (https://sphere10.com/products/localnotion)
// Author: Herman Schoenfeld <herman@sphere10.com>
//
// Distributed under the GPLv3 software license, see the accompanying file LICENSE
// or visit https://github.com/HermanSchoenfeld/localnotion/blob/master/LICENSE
//
// This notice must not be removed when duplicating this file or its contents, in whole or in part.

using LocalNotion.Core;
using NUnit.Framework;

namespace LocalNotion.Core.Tests;

[TestFixture]
[Parallelizable(ParallelScope.Children)]
public class GatedDownloadTests {

	[TestCase("photo.png", "text/html")]
	[TestCase("manual.pdf", "text/html; charset=utf-8")]
	[TestCase("LN_abcdef", "TEXT/HTML")]
	[TestCase("archive.zip", "application/xhtml+xml")]
	public void AnHtmlPageReturnedForAnotherKindOfFileIsAGate(string filename, string mimeType)
		=> Assert.That(NotionSyncOrchestrator.IsUnexpectedHtml(filename, mimeType), Is.True, "The server answered with a page, not the file; the original url is kept.");

	[TestCase("photo.png", "image/png")]
	[TestCase("manual.pdf", "application/pdf")]
	[TestCase("page.html", "text/html")]
	[TestCase("page.HTM", "text/html; charset=utf-8")]
	[TestCase("doc.xhtml", "application/xhtml+xml")]
	[TestCase("data.bin", null)]
	public void TheExpectedContentIsAccepted(string filename, string mimeType)
		=> Assert.That(NotionSyncOrchestrator.IsUnexpectedHtml(filename, mimeType), Is.False);
}
