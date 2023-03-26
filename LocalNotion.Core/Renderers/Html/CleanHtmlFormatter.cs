using AngleSharp.Html;

namespace LocalNotion.Core;

using AngleSharp.Dom;
using AngleSharp.Text;
using System;
using System.Linq;

/// <summary>
/// A version of <see cref="PrettyMarkupFormatter"/> that correctly handles pre-formatted tags.
/// </summary>
internal class CleanHtmlFormatter : HtmlMarkupFormatter {
	#region Fields

	private String _indentString;
	private String _newLineString;
	private Int32 _indentCount;

	#endregion

	#region ctor

	/// <summary>
	/// Creates a new instance of the pretty markup formatter.
	/// </summary>
	public CleanHtmlFormatter() {
		_indentCount = 0;
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

	protected bool ApplyFormatting { get; set; } = true;

	#endregion

	#region Methods

	/// <inheritdoc />
	public override String Comment(IComment comment) =>
		IndentBefore() + base.Comment(comment);

	/// <inheritdoc />
	public override String Doctype(IDocumentType doctype) {
		var before = String.Empty;

		if (doctype.ParentElement != null) {
			before = IndentBefore();
		}

		return before + base.Doctype(doctype) + NewLine;
	}

	/// <inheritdoc />
	public override String Processing(IProcessingInstruction processing) =>
		IndentBefore() + base.Processing(processing);

	/// <inheritdoc />
	public override String Text(ICharacterData text) {
		if (!ApplyFormatting)
			return text.Data;

		var content = text.Data;
		var before = String.Empty;
		var singleLine = content.Replace(Symbols.LineFeed, Symbols.Space);

		if (text.NextSibling is ICharacterData == false) {
			singleLine = singleLine.TrimEnd();
		}

		if (singleLine.Length > 0 && text.PreviousSibling is ICharacterData == false && singleLine[0].IsSpaceCharacter()) {
			singleLine = singleLine.TrimStart();
			before = IndentBefore();
		}

		return before + HtmlMarkupFormatter.EscapeText(singleLine);
	}

	/// <inheritdoc />
	public override String OpenTag(IElement element, Boolean selfClosing) {
		if (!ApplyFormatting)
			return base.OpenTag(element, selfClosing);

		if (element.TagName == "PRE") {
			EncounteredPreformatBegin();
		}
		var before = String.Empty;
		var previousSibling = element.PreviousSibling as IText;

		if (element.ParentElement != null && (previousSibling is null || EndsWithSpace(previousSibling))) {
			before = IndentBefore();
		}

		_indentCount++;
		return before + base.OpenTag(element, selfClosing) + (element.TagName == "CODE" ? this.NewLine : string.Empty);
	}

	/// <inheritdoc />
	public override String CloseTag(IElement element, Boolean selfClosing) {
		if (!ApplyFormatting && element.TagName != "PRE")
			return base.CloseTag(element, selfClosing);

		_indentCount--;
		var before = String.Empty;
		var lastChild = element.LastChild as IText;

		if (element.HasChildNodes && (lastChild is null || EndsWithSpace(lastChild))) {
			before = IndentBefore();
		}
		
		if (element.TagName == "PRE") {
			EncounteredPreformatEnd();
		}

		return before + base.CloseTag(element, selfClosing);
	}

	#endregion

	#region Helpers

	private static Boolean EndsWithSpace(ICharacterData text) {
		var content = text.Data;
		return content.Length > 0 && content[content.Length - 1].IsSpaceCharacter();
	}

	private String IndentBefore() => _newLineString + String.Join(String.Empty, Enumerable.Repeat(_indentString, _indentCount));

	private void EncounteredPreformatBegin() {
		ApplyFormatting = false;
	}

	private void EncounteredPreformatEnd() {
			ApplyFormatting = true;
	}


	#endregion
}