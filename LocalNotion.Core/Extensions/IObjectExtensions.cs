using JsonSubTypes;
using LocalNotion.Core.DataObjects;
using Notion.Client;
using Sphere10.Framework;


namespace LocalNotion.Core;

public static class IObjectExtensions {

	public static string GetTitle(this IObject obj) 
		=> obj switch {
			Page page => page.GetTextTitle(),
			Database database => database.GetTextTitle(),
			DataSource dataSource => dataSource.GetTextTitle(),
			Block block => block.GetTextTitle(),
			User user => user.Name,
			Comment comment => comment.GetTextTitle(),
			FileUpload fileUpload => fileUpload.GetTextTitle(),
			_ => throw new NotSupportedException($"{obj.GetType()}")
		};

	public static DateTime? GetLastEditedDate(this IObject obj) 
		=> obj switch {
			Page page => page.LastEditedTime,
			Database database => database.LastEditedTime,
			DataSource dataSource => dataSource.LastEditedTime,
			Block block => block.LastEditedTime,
			User user => null,
			Comment comment => comment.LastEditedTime,
			FileUpload fileUpload => fileUpload.LastEditedTime,
			_ => default
		};

	public static IEnumerable<string> GetTextContents(this IObject obj)
		=> obj switch {
			CreateCommentRequest createCommentRequest => createCommentRequest.RichText.Select(x => x.PlainText ?? string.Empty),
			Comment comment => comment.RichText.Select(x => x.PlainText ?? string.Empty),
			BookmarkBlock bookmarkBlock => bookmarkBlock.Bookmark.Caption.Select(x => x.PlainText ?? string.Empty),
			BreadcrumbBlock breadcrumbBlock => Enumerable.Empty<string>(),
			BulletedListItemBlock bulletedListItemBlock => bulletedListItemBlock.BulletedListItem.RichText.Select(x => x.PlainText ?? string.Empty),
			CalloutBlock calloutBlock => calloutBlock.Callout.RichText.Select(x => x.PlainText ?? string.Empty).Concat(calloutBlock.Callout.Children.ToEmptyIfNull().SelectMany(x => x.GetTextContents())),
			ChildDatabaseBlock childDatabaseBlock => new[] { childDatabaseBlock.ChildDatabase.Title },
			ChildPageBlock childPageBlock => new [] { childPageBlock.ChildPage.Title },
			CodeBlock codeBlock => codeBlock.Code.Caption.Select(x => x.PlainText ?? string.Empty).Concat(codeBlock.Code.RichText.Select(x => x.PlainText ?? string.Empty)).Concat(codeBlock.Code.Language),
			ColumnBlock columnBlock => columnBlock.Column.Children.ToEmptyIfNull().SelectMany(x => x.GetTextContents()),
			ColumnListBlock columnListBlock => columnListBlock.ColumnList.Children.ToEmptyIfNull().SelectMany(x => x?.GetTextContents()),
			AudioBlock audioBlock => audioBlock.Audio.Caption.Select(x => x.PlainText ?? string.Empty),
			DividerBlock dividerBlock => Enumerable.Empty<string>(),
			EmbedBlock embedBlock => embedBlock.Embed.Caption.Select(x => x.PlainText ?? string.Empty).Concat(embedBlock.Embed.Url),
			EquationBlock equationBlock => new [] { equationBlock.Equation.Expression },
			FileBlock fileBlock => fileBlock.File.Caption.Select(x => x.PlainText ?? string.Empty),
			HeadingOneBlock headingOneBlock => headingOneBlock.Heading_1.RichText.Select(x => x.PlainText ?? string.Empty),
			HeadingThreeBlock headingThreeeBlock => headingThreeeBlock.Heading_3.RichText.Select(x => x.PlainText ?? string.Empty),
			HeadingTwoBlock headingTwoBlock => headingTwoBlock.Heading_2.RichText.Select(x => x.PlainText ?? string.Empty),
			ImageBlock imageBlock => imageBlock.Image.Caption.Select(x => x.PlainText ?? string.Empty),
			LinkPreviewBlock linkPreviewBlock => Enumerable.Empty<string>(),
			LinkToPageBlock linkToPageBlock => Enumerable.Empty<string>(),
			NumberedListItemBlock numberedListItemBlock => numberedListItemBlock.NumberedListItem.RichText.Select(x => x.PlainText ?? string.Empty).Concat(numberedListItemBlock.NumberedListItem.Children.ToEmptyIfNull().SelectMany(x => x.GetTextContents())),
			ParagraphBlock paragraphBlock => paragraphBlock.Paragraph.RichText.Select(x => x.PlainText ?? string.Empty),
			PDFBlock pdfBlock => pdfBlock.PDF.Caption.Select(x => x.PlainText ?? string.Empty),
			QuoteBlock quoteBlock => quoteBlock.Quote.RichText.Select(x => x.PlainText ?? string.Empty).Concat(quoteBlock.Quote.Children.ToEmptyIfNull().SelectMany(x => x.GetTextContents())),
			SyncedBlockBlock syncedBlockBlock => Enumerable.Empty<string>(),
			TableBlock tableBlock => tableBlock.Table.Children.SelectMany(GetTextContents),
			TableOfContentsBlock tableOfContentsBlock => Enumerable.Empty<string>(),
			TableRowBlock tableRowBlock => tableRowBlock.TableRow.Cells.SelectMany(x => x).Select(x => x.PlainText),
			TemplateBlock templateBlock => templateBlock.Template.RichText.Select(x => x.PlainText ?? string.Empty),
			ToDoBlock toDoBlock => toDoBlock.ToDo.RichText.Select(x => x.PlainText ?? string.Empty).Concat(toDoBlock.ToDo.Children.ToEmptyIfNull().SelectMany(x => x.GetTextContents())),
			ToggleBlock toggleBlock => toggleBlock.Toggle.RichText.Select(x => x.PlainText ?? string.Empty).Concat(toggleBlock.Toggle.Children.ToEmptyIfNull().SelectMany(x => x.GetTextContents())),
			UnsupportedBlock unsupportedBlock => Enumerable.Empty<string>(),
			VideoBlock videoBlock => videoBlock.Video.Caption.Select(x => x.PlainText ?? string.Empty),
			Database database => database.Title.Select(x => x.PlainText ?? string.Empty).Concat(database.Description.Select(x => x.PlainText ?? string.Empty)),
			Page page => page.Properties.Values.Select(x => x.ToPlainText()),
			PartialUser partialUser => Enumerable.Empty<string>(),
			User user => new [] { user.Name },
			_ => Enumerable.Empty<string>(),
		};

	public static bool HasFileAttachment(this IObject block)
		=> block switch {
			AudioBlock audioBlock => true,
			FileBlock fileBlock => true,
			ImageBlock imageBlock => true,
			VideoBlock videoBlock => true,
			PDFBlock pdfBlock => true,
			CalloutBlock { Callout.Icon: CustomEmojiPageIcon or FilePageIcon } => true,
			_ => false
		};

	public static WrappedNotionFile GetFileAttachment(this IObject @object) 
		=> @object.GetFileAttachmentOrDefault() ?? throw new InvalidOperationException("Object type does not have file attachment property");

	public static WrappedNotionFile GetFileAttachmentOrDefault(this IObject block)
		=> block switch {
			AudioBlock audioBlock => new WrappedNotionFile(audioBlock.Audio),
			FileBlock fileBlock => new WrappedNotionFile(fileBlock.File),
			ImageBlock imageBlock => new WrappedNotionFile(imageBlock.Image),
			VideoBlock videoBlock => new WrappedNotionFile(videoBlock.Video),
			PDFBlock pdfBlock => new WrappedNotionFile(pdfBlock.PDF),
			CalloutBlock { Callout.Icon: CustomEmojiPageIcon or FilePageIcon } calloutBlock => new WrappedNotionFile(calloutBlock.Callout.Icon),
			_ => default
		};

	public static ParentObject GetParent(this IObject obj) 
		=> TryGetParent(obj, out var parent) ? parent : throw new InvalidOperationException($"{nameof(IObject)} of type {obj.GetType().Name} does not have a parent");

	public static bool TryGetParent(this IObject obj, out ParentObject parent) {
		switch (obj) {
			case Comment comment:
				parent = new(comment.Parent);
				return true;

			case Database database:
				parent = new(database.Parent);
				return true;

			case IBlock block:
				parent = new(block.Parent);
				return true;

			case Page page:
				parent = new(page.Parent);
				return true;

			case PartialUser partialUser:
			case User user:
			default:
				parent = null;
				break;
		}
		return false;
	}


}

