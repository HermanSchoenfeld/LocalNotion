//using Hydrogen;
//using Notion.Client;
//using System.Runtime.Serialization;
//using System.Text;

//namespace LocalNotion;

//public class TextRenderer : PageRendererBase<string> {

//	public TextRenderer(LocalNotionPage page, IUrlResolver resolver) 
//		: base(page, resolver) {
//		TabLevel = 0;
//	}

//	public string PreferredFileExt => ".txt";

//	private int TabLevel { get; set; }

//	protected override string Merge(IEnumerable<string> outputs)
//		=> outputs.ToDelimittedString(string.Empty);

//	#region Values

//	protected override string Render(EmojiObject emojiObject)
//		 => $"[{emojiObject.Type}] {emojiObject.Emoji}";

//	protected override string Render(ExternalFile externalFile) =>
//		 $"[{externalFile.Type}] Url: '{externalFile.External.Url}', Caption: '{Render(externalFile.Caption)}'";

//	protected override string Render(UploadedFile uploadedFile)
//		 => $"[{uploadedFile.Type}] Url: '{Resolver.ResolveUploadedFileUrl(uploadedFile).ResultSafe()}', Expiry: '{Render(uploadedFile.File.ExpiryTime)}', Caption: '{Render(uploadedFile.Caption)}'";

//	protected override string Render(Link link)
//		=> $"[{link.Type}] [{link.Url}]";

//	protected override string Render(Date date) {
//		var sb = new StringBuilder();
//		sb.Append("[Date] ");
//		var start = Render(date.Start);
//		var end = Render(date.End);
//		return date.End == null ? start : $"{start} - {end}";
//	}

//	protected override string Render(DateTime? date)
//		=> date != null ? date.ToString("yyyy-MM-dd HH:mm:ss.fff") : "Empty";

//	protected override string Render(bool? val)
//		=> !val.HasValue ? "Empty Bool" : val.Value ? "[X]" : "[ ]";

//	protected override string Render(double? val)
//		=> !val.HasValue ? "Empty Number" : $"{val.Value:G}";

//	#endregion

//	#region Owners

//	protected override string Render(UserOwner owner)
//		=> $"[{owner.Type}] {Render(owner.User)}";

//	protected override string Render(WorkspaceIntegrationOwner owner)
//		=> $"[{owner.Type}] ({(owner.Workspace ? "workspace owner" : "not a workspace owner")})";

//	#endregion

//	#region Users

//	protected override string Render(User user)
//		=> $"[User] {user.Name} ({(user.Person != null ? Render(user.Person) : string.Empty)}{(user.Bot != null ? Render(user.Bot) : string.Empty)})";

//	protected override string Render(Person user)
//		=> $"[Person] {user.Email})";

//	protected override string Render(Bot user)
//		=> $"[Bot] ({user.Owner?.Type} - {this.Render(user.Owner)})";

//	#endregion

//	#region Database

//	protected override string Render(Database database)
//		=> $"[Database] {database.Id} {database.Title}{NewLine()} DATABASE RENDERING NOT IMPLEMENTED";

//	#endregion

//	#region Text

//	protected override string Render(IEnumerable<RichTextBase> text)
//		=> text.Select(this.Render).ToDelimittedString(string.Empty);

//	protected override string Render(RichTextEquation text)
//		=> $"[{ToString(text.Type)}] {text.Equation.Expression}";

//	protected override string Render(RichTextMention text)
//		=> text.Mention.Type switch {
//			"user" => $"[{text.Mention.Type}] {Render(text.Mention.User)}",
//			"page" => $"[{text.Mention.Type}] {Resolver.ResolveOrDefault(text.Mention.Page.Id, $"Unresolved link to '{text.Mention.Page.Id}'").ResultSafe()}",
//			"database" => $"[{text.Mention.Type}]{text.Mention.Database.Id}",
//			"date" => $"[{text.Mention.Type}] {this.Render(text.Mention.Date)}",
//			_ => throw new InvalidOperationException($"Unrecognized mention type '{text.Mention.Type}'")
//		};

//	protected override string Render(RichTextText text)
//		=> $"{ToString(text.Annotations)}{text.Text.Content}";

//	#endregion

//	#region Property Values

//	protected override string Render(CheckboxPropertyValue propertyValue)
//		=> $"[{ToString(propertyValue.Type)}] {Render(propertyValue.Checkbox)}";

//	protected override string Render(CreatedByPropertyValue propertyValue)
//		=> $"[{ToString(propertyValue.Type)}] {Render(propertyValue.CreatedBy)}";

//	protected override string Render(CreatedTimePropertyValue propertyValue)
//		=> $"[{ToString(propertyValue.Type)}] {propertyValue.CreatedTime}";

//	protected override string Render(DatePropertyValue propertyValue)
//		=> $"[{ToString(propertyValue.Type)}] {Render(propertyValue.Date)}";

//	protected override string Render(EmailPropertyValue propertyValue)
//		=> $"[{ToString(propertyValue.Type)}] {propertyValue.Email}";

//	protected override string Render(FilesPropertyValue propertyValue) {
//		var sb = new StringBuilder();
//		sb.Append($"[{ToString(propertyValue.Type)}]");
//		TabLevel++;
//		sb.Append(NewLine());
//		foreach (var file in propertyValue.Files) {
//			sb.Append($"[{file.Type}] {file.Name}{NewLine()}");
//		}
//		TabLevel--;
//		return sb.ToString();
//	}

//	protected override string Render(FormulaPropertyValue propertyValue)
//		=> propertyValue.Formula.Type switch {
//			"string" => $"{ToString(propertyValue.Type)}:{propertyValue.Formula.Type} [{propertyValue.Formula.String}]",
//			"number" => $"{ToString(propertyValue.Type)}:{propertyValue.Formula.Type} [{Render(propertyValue.Formula.Number)}]",
//			"date" => $"{ToString(propertyValue.Type)}:{propertyValue.Formula.Type} [{Render(propertyValue.Formula.Date)}]",
//			"array" => $"{ToString(propertyValue.Type)}:{propertyValue.Formula.Type} [{Render(propertyValue.Formula.Boolean)}]",
//			_ => throw new InvalidOperationException($"Unrecognized formula type '{propertyValue.Formula.Type}'")
//		};

//	protected override string Render(LastEditedByPropertyValue propertyValue)
//		=> $"[{ToString(propertyValue.Type)}] {Render(propertyValue.LastEditedBy)}";

//	protected override string Render(LastEditedTimePropertyValue propertyValue)
//		=> $"[{ToString(propertyValue.Type)}] {propertyValue.LastEditedTime}";

//	protected override string Render(MultiSelectPropertyValue propertyValue)
//		=> $"[{ToString(propertyValue.Type)}] {propertyValue.MultiSelect.Select(ToString).ToDelimittedString(", ")}";

//	protected override string Render(NumberPropertyValue propertyValue)
//		=> $"[{ToString(propertyValue.Type)}] {Render(propertyValue.Number)}";

//	protected override string Render(PeoplePropertyValue propertyValue)
//		=> $"[{ToString(propertyValue.Type)}] {propertyValue.People.Select(Render).ToDelimittedString(", ")}";

//	protected override string Render(PhoneNumberPropertyValue propertyValue)
//		=> $"[{ToString(propertyValue.Type)}] {propertyValue.PhoneNumber}";

//	protected override string Render(RelationPropertyValue propertyValue)
//		=> $"[{ToString(propertyValue.Type)}] {propertyValue.Relation.Select(x => $"{x.Id}").ToDelimittedString(", ")}";

//	protected override string Render(RichTextPropertyValue propertyValue)
//		=> $"[{ToString(propertyValue.Type)}] {Render(propertyValue.RichText)}";

//	protected override string Render(RollupPropertyValue propertyValue) {
//		return propertyValue.Rollup.Type switch {
//			"number" => $"{ToString(propertyValue.Type)}:{propertyValue.Rollup.Type} [{Render(propertyValue.Rollup.Number)}]",
//			"date" => $"{ToString(propertyValue.Type)}:{propertyValue.Rollup.Type} [{Render(propertyValue.Rollup.Date)}]",
//			"array" => $"{ToString(propertyValue.Type)}:{propertyValue.Rollup.Type} [{propertyValue.Rollup.Array.Select(Render).ToDelimittedString(", ")}]",
//			_ => throw new InvalidOperationException($"Unrecognized rollup type '{propertyValue.Rollup.Type}'")
//		};
//	}

//	protected override string Render(SelectPropertyValue propertyValue)
//		=> $"[{ToString(propertyValue.Type)}] ({propertyValue.Select.Color})";

//	protected override string Render(TitlePropertyValue propertyValue)
//		=> $"[{ToString(propertyValue.Type)}] {Render(propertyValue.Title)}";

//	protected override string Render(UrlPropertyValue propertyValue)
//		=> $"[{ToString(propertyValue.Type)}] {propertyValue.Url}";

//	#endregion

//	#region Page
	
//	protected override string  Render(Page page) 
//		=> RenderChildItems();

//	protected override string Render(AudioBlock block)
//		=> $"[{ToString(block.Type)}] {this.Render(block.Audio)} {(block.HasChildren ? RenderChildItems() : NewLine())}";

//	protected override string Render(BookmarkBlock block)
//		=> $"[{ToString(block.Type)}] ({block.Bookmark.Caption}) [{block.Bookmark.Url}]{(block.HasChildren ? RenderChildItems() : NewLine())}";

//	protected override string Render(BreadcrumbBlock block)
//		=> $"[{ToString(block.Type)}] {block.Id}{NewLine()}";

//	protected override string Render(int number, BulletedListItemBlock block)
//		=> $"[{ToString(block.Type)} ({number})] - {Render(block.BulletedListItem.Text)}{(block.HasChildren ? RenderChildItems() : NewLine())}";

//	protected override string Render(CalloutBlock block)
//		=> $"[{ToString(block.Type)} Icon: {this.Render(block.Callout.Icon)}] {Render(block.Callout.Text)}{(block.HasChildren ? RenderChildItems() : NewLine())}";

//	protected override string Render(ChildDatabaseBlock block)
//		=> $"[{ToString(block.Type)}] {block.ChildDatabase.Title} {(block.HasChildren ? RenderChildItems() : NewLine())}";

//	protected override string Render(ChildPageBlock block)
//		=> $"[Child Page] {block.ChildPage.Title}{NewLine()} {RenderChildItems()}{(block.HasChildren ? RenderChildItems() : NewLine())}";

//	protected override string Render(CodeBlock block)
//		=> $"[{ToString(block.Type)}-{block.Code.Language}]{NewLine()}{Render(block.Code.Text)}{(block.HasChildren ? RenderChildItems() : NewLine())}";

//	protected override string Render(ColumnBlock block)
//		=> $"[{ToString(block.Type)}]{(block.HasChildren ? RenderChildItems() : NewLine())}";

//	protected override string Render(ColumnListBlock block)
//		=> $"[{ToString(block.Type)}]{(block.HasChildren ? RenderChildItems() : NewLine())}";

//	protected override string Render(DividerBlock block)
//		=> $"[{ToString(block.Type)}] {(block.HasChildren ? RenderChildItems() : NewLine())}";

//	protected override string Render(EmbedBlock block)
//		=> $"[{ToString(block.Type)}]({block.Embed.Url}) {(block.HasChildren ? RenderChildItems() : NewLine())}";

//	protected override string Render(EquationBlock block)
//		=> $"[{ToString(block.Type)}]({block.Equation.Expression}){(block.HasChildren ? RenderChildItems() : NewLine())}";

//	protected override string Render(FileBlock block)
//		=> $"[{ToString(block.Type)}] {this.Render(block.File)}{(block.HasChildren ? RenderChildItems() : NewLine())}";

//	protected override string Render(HeadingOneBlock block)
//		=> $"[{ToString(block.Type)}] {Render(block.Heading_1.Text)}{(block.HasChildren ? RenderChildItems() : NewLine())}";

//	protected override string Render(HeadingThreeeBlock block)
//		=> $"[{ToString(block.Type)}] {Render(block.Heading_3.Text)}{(block.HasChildren ? RenderChildItems() : NewLine())}";

//	protected override string Render(HeadingTwoBlock block)
//		=> $"[{ToString(block.Type)}] {Render(block.Heading_2.Text)}{(block.HasChildren ? RenderChildItems() : NewLine())}";

//	protected override string Render(ImageBlock block)
//		=> $"[{ToString(block.Type)}] {this.Render(block.Image)}{(block.HasChildren ? RenderChildItems() : NewLine())}";

//	protected override string Render(LinkToPageBlock block)
//		=> $"[{ToString(block.Type)}] {this.Render(block.LinkToPage)}{(block.HasChildren ? RenderChildItems() : NewLine())}";

//	protected override string Render(int number, NumberedListItemBlock block)
//		=> $"[{ToString(block.Type)}] {number}. {Render(block.NumberedListItem.Text)}{(block.HasChildren ? RenderChildItems() : NewLine())}";

//	protected override string Render(PDFBlock block)
//		=> $"[{ToString(block.Type)}] {this.Render(block.PDF)}{(block.HasChildren ? RenderChildItems() : NewLine())}";

//	protected override string Render(ParagraphBlock block)
//		=> $"[{ToString(block.Type)}] {Render(block.Paragraph.Text)}{(block.HasChildren ? RenderChildItems() : NewLine())}";

//	protected override string Render(QuoteBlock block)
//		=> $"[{ToString(block.Type)}] {Render(block.Quote.Text)}{(block.HasChildren ? RenderChildItems() : NewLine())}";

//	protected override string Render(SyncedBlockBlock block)
//		=> $"[{ToString(block.Type)}:{block.SyncedBlock.SyncedFrom.Type}] {block.SyncedBlock.SyncedFrom.BlockId} {(block.HasChildren ? RenderChildItems() : NewLine())}";

//	protected override string Render(TableOfContentsBlock block)
//		=> $"[{ToString(block.Type)}]{(block.HasChildren ? RenderChildItems() : NewLine())}";

//	protected override string Render(TemplateBlock block)
//		=> $"[{ToString(block.Type)}] {block.Id}";

//	protected override string Render(ToDoBlock block)
//		=> $"[{ToString(block.Type)}] [{(block.ToDo.IsChecked ? "X" : " ")}] {Render(block.ToDo.Text)}{(block.HasChildren ? RenderChildItems() : NewLine())}";

//	protected override string Render(ToggleBlock block)
//		=> $"[{ToString(block.Type)}]{(block.HasChildren ? RenderChildItems() : NewLine())}";

//	protected override string Render(UnsupportedBlock block)
//		=> $"[{ToString(block.Type)}] {block.Id} {NewLine()}";

	
//	protected override string RenderYouTubeEmbed(VideoBlock videoBlock, string youTubeVideoID) 
//		=> $"[{videoBlock.Type}] [YouTube] VideoID: {youTubeVideoID} {NewLine()}";

//	protected override string RenderVideoEmbed(VideoBlock videoBlock, string url) 
//		=> $"[{videoBlock.Type}] [Link] Url: {url} {NewLine()}";

//	#endregion

//	#region Parent

//	protected override string Render(DatabaseParent parent)
//		=> $"[{ToString(parent.Type)}] {parent.DatabaseId}";

//	protected override string Render(PageParent parent)
//		=> $"[{ToString(parent.Type)}] {parent.PageId}";

//	protected override string Render(WorkspaceParent parent)
//		=> $"[{ToString(parent.Type)}]";

//	#endregion

//	#region Aux


//	private string ToString(Annotations annotations) {
//		var sb = new FastStringBuilder();
//		if (annotations.IsBold)
//			sb.Append("B");
//		if (annotations.IsCode)
//			sb.Append("C");
//		if (annotations.IsItalic)
//			sb.Append("I");
//		if (annotations.IsStrikeThrough)
//			sb.Append("S");
//		if (annotations.IsUnderline)
//			sb.Append("U");
//		if (sb.Length > 0) {
//			sb.Prepend("[");
//			sb.Append("]");
//		}
//		return sb.ToString();
//	}

//	private string ToString(ParentType blockType)
//		=> $"[{blockType.GetAttribute<EnumMemberAttribute>().Value}]";

//	private string ToString(BlockType blockType)
//		=> $"{blockType.GetAttribute<EnumMemberAttribute>().Value}";

//	private string ToString(RichTextType richTextType)
//		=> $"{richTextType.GetAttribute<EnumMemberAttribute>().Value}";

//	private string ToString(PropertyValueType propertyValueType)
//		=> $"{propertyValueType.GetAttribute<EnumMemberAttribute>().Value}";

//	private string NewLine() => $"{Environment.NewLine}{Tools.Array.Gen(TabLevel, "\t").ToDelimittedString(string.Empty)}";

//	private string ToString(SelectOption? val)
//		=> $"{val?.Name} [{val?.Color}]";

//	#endregion
//}
