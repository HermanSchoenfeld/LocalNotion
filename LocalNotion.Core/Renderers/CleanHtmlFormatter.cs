// Copyright (c) Herman Schoenfeld 2018 - Present. All rights reserved. (https://sphere10.com/products/localnotion)
// Author: Herman Schoenfeld <herman@sphere10.com>
//
// Distributed under the GPLv3 software license, see the accompanying file LICENSE
// or visit https://github.com/HermanSchoenfeld/localnotion/blob/master/LICENSE
//
// This notice must not be removed when duplicating this file or its contents, in whole or in part.

#if CleanHTML
using System;
using System.Linq;
using AngleSharp.Dom;
using AngleSharp.Html;

namespace LocalNotion.Core;

/// <summary>
/// Formats structural markup while preserving mixed content and pre-formatted sources.
/// </summary>
internal class CleanHtmlFormatter : HtmlMarkupFormatter {

	#region Fields

	private String _indentString;

	private String _newLineString;

	#endregion

	#region ctor

	/// <summary>
	/// Creates a new instance of the pretty markup formatter.
	/// </summary>
	public CleanHtmlFormatter() {
		_indentString = "\t";
		_newLineString = "\n";
	}

	#endregion

	#region Properties

	/// <summary>
	/// Gets or sets the indentation string.
	/// </summary>
	public String Indentation {
		get => _indentString;
		set => _indentString = value;
	}

	/// <summary>
	/// Gets or sets the newline string.
	/// </summary>
	public String NewLine {
		get => _newLineString;
		set => _newLineString = value;
	}

	#endregion

	#region Methods

	/// <inheritdoc />
	public override String Comment(IComment comment) =>
		(CanFormatChildren(comment.ParentElement) ? IndentBefore(comment) : String.Empty) + base.Comment(comment);

	/// <inheritdoc />
	public override String Doctype(IDocumentType doctype) {
		var before = String.Empty;

		if (doctype.ParentElement != null)
			before = IndentBefore(doctype);

		return before + base.Doctype(doctype) + NewLine;
	}

	/// <inheritdoc />
	public override String Processing(IProcessingInstruction processing) =>
		(CanFormatChildren(processing.ParentElement) ? IndentBefore(processing) : String.Empty) + base.Processing(processing);

	/// <inheritdoc />
	public override String Text(ICharacterData text) {
		// Reindent structural whitespace only. Inline code, prose and raw sources
		// must retain their original text when the optional formatter is enabled.
		return CanFormatChildren(text.ParentElement) && String.IsNullOrWhiteSpace(text.Data)
			? String.Empty
			: base.Text(text);
	}

	/// <inheritdoc />
	public override String OpenTag(IElement element, Boolean selfClosing) {
		var before = CanFormatChildren(element.ParentElement) ? IndentBefore(element) : String.Empty;
		return before + base.OpenTag(element, selfClosing);
	}

	/// <inheritdoc />
	public override String CloseTag(IElement element, Boolean selfClosing) {
		var close = base.CloseTag(element, selfClosing);
		var before = close.Length > 0 && element.HasChildNodes && CanFormatChildren(element) ? IndentBefore(element) : String.Empty;
		return before + close;
	}

	#endregion

	#region Helpers

	private static bool CanFormatChildren(IElement element) {
		for (var current = element; current != null; current = current.ParentElement) {
			if (!IsStructural(current.TagName) || current.ChildNodes.OfType<IText>().Any(text => !String.IsNullOrWhiteSpace(text.Data)))
				return false;
			if (current.Children.Any(child => IsInline(child.TagName)))
				return false;
		}
		return element != null;
	}

	private static bool IsStructural(string name) => name is "HTML" or "HEAD" or "BODY" or "DIV" or "SECTION" or "ARTICLE" or "NAV" or "MAIN" or "HEADER" or "FOOTER" or "UL" or "OL" or "DL" or "TABLE" or "THEAD" or "TBODY" or "TFOOT" or "TR" or "COLGROUP";

	private static bool IsInline(string name) => name is "A" or "SPAN" or "CODE" or "STRONG" or "EM" or "B" or "I" or "SMALL" or "BR" or "WBR" or "IMG" or "INPUT" or "LABEL" or "BUTTON" or "SVG" or "SUP" or "SUB";

	private String IndentBefore(INode node) {
		var depth = 0;
		for (var parent = node.ParentElement; parent != null; parent = parent.ParentElement)
			depth++;
		return NewLine + String.Concat(Enumerable.Repeat(Indentation, depth));
	}

	#endregion
}
#endif
