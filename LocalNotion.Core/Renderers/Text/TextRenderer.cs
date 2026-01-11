using Sphere10.Framework;
using Notion.Client;
using System.Runtime.Serialization;
using System.Text.RegularExpressions;

namespace LocalNotion.Core;

public class TextRenderer : RecursiveRendererBase<string> {
	private int _toggleCount = 0;

	public TextRenderer(ILogger logger) : base(RenderMode.ReadOnly, logger) {
	}

	protected override string Merge(IEnumerable<string> outputs)
		=> outputs.ToDelimittedString(string.Empty);

	#region Values

	protected override string Render(EmojiPageIcon emojiObject)
		 => emojiObject.Emoji;

	protected override string Render(Link link) => string.Empty;

	protected override string Render(Date date) {
		if (date == null)
			return string.Empty;

		var start = Render(date.Start);
		var end = Render(date.End);
		return date.End == null ? start : $"{start} - {end}";
	}

	protected override string Render(DateTimeOffset? date)
		=> date != null ? $"{date:yyyy-MM-dd HH:mm zzz}" : "Empty";

	protected override string Render(bool? val)
		=> !val.HasValue ? string.Empty : val.Value ? "[X]" : "[ ]";

	protected override string Render(double? val)
		=> !val.HasValue ? string.Empty : $"{val.Value:G}";

	protected override string Render(string val)
		=> !string.IsNullOrEmpty(val) ? val : string.Empty;

	#endregion

	#region Owners

	protected override string Render(UserOwner owner)
		=> Render(owner.User);

	protected override string Render(WorkspaceIntegrationOwner owner)
		=> RenderUnsupported(owner);

	#endregion

	#region Users

	protected override string Render(User user)
		=> RenderEmailLink(user.Name, user.Person?.Email);

	protected virtual string RenderEmailLink(string linkTitle, string email) => !string.IsNullOrWhiteSpace(linkTitle) ? $"{linkTitle} <{email}>" : email;

	protected override string Render(Person user) => RenderUnsupported(user);

	protected override string Render(Bot user) => Render(user.Owner);

	#endregion

	#region Database

	protected override string Render(Database database, bool inline) {
		return string.Empty;
		//var graph = Repository.GetEditableResourceGraph(database.Id);
		//var rows = graph.Children.Select(x => Repository.GetObject(x.ObjectID)).Cast<Page>();

		//return inline switch {
		//	false => RenderTemplate(
		//			"page_database",
		//			new RenderTokens(database) {
		//				["title"] = database.Title.ToPlainText(),   // html title
		//				["page_name"] = RenderingContext.Resource.Name,
		//				["description"] = Render(database.Description),
		//				["page_title"] = RenderTemplate("page_title", new() { ["text"] = RenderingContext.Resource.Title }),   // title on the page 
		//				["style"] = "wide",
		//				["page_cover"] = RenderingContext.Resource.Cover switch {
		//					null => string.Empty,
		//					_ => RenderTemplate(
		//						"page_cover",
		//						new RenderTokens {
		//							["cover_url"] = SanitizeUrl(RenderingContext.Resource.Cover) ?? string.Empty,
		//						}
		//					)
		//				},
		//				["thumbnail"] = RenderingContext.Resource.Thumbnail.Type switch {
		//					ThumbnailType.None => string.Empty,
		//					ThumbnailType.Emoji => RenderTemplate(
		//						RenderingContext.Resource.Cover != null ? "thumbnail_emoji_on_cover" : "thumbnail_emoji",
		//						new RenderTokens {
		//							["thumbnail_emoji"] = RenderingContext.Resource.Thumbnail.Data,
		//						}
		//					),
		//					ThumbnailType.Image => RenderTemplate(
		//						RenderingContext.Resource.Cover != null ? "thumbnail_image_on_cover" : "thumbnail_image",
		//						new RenderTokens {
		//							["thumbnail_url"] = SanitizeUrl(RenderingContext.Resource.Thumbnail.Data)
		//						}
		//					),
		//				},
		//				["id"] = RenderingContext.Resource.ID,
		//				["created_time"] = RenderingContext.Resource.CreatedOn,
		//				["last_updated_time"] = RenderingContext.Resource.LastEditedOn,
		//				["contents"] = Render(database, true)
		//			}
		//		),

		//	true => RenderTemplate(
		//		"database",
		//		new RenderTokens(database) {
		//			["header"] = Merge(database.Properties.Select(x => RenderTemplate("database_header_cell", new RenderTokens(x) { ["contents"] = Render(x.Key, x.Value) }))),
		//			["contents"] = Merge(
		//				rows.Select(page =>
		//					RenderTemplate(
		//						"database_row",
		//						new RenderTokens(database) {
		//							["contents"] = Merge(
		//								database.Properties.Select(x => RenderTemplate("database_row_cell", new RenderTokens(x) { ["contents"] = Render(page, page.Properties[x.Key]) }))
		//							)
		//						}
		//					)
		//				)
		//			)
		//		}
		//	),
		//};
	}

	#region Properties

	protected override string Render(CheckboxProperty property) => RenderPropertyCommon(property, null, property.Name);

	protected override string Render(CreatedByProperty property) => RenderPropertyCommon(property, null, property.Name);

	protected override string Render(CreatedTimeProperty property) => RenderPropertyCommon(property, null, property.Name);

	protected override string Render(DateProperty property) => RenderPropertyCommon(property, null, property.Name);

	protected override string Render(EmailProperty property) => RenderPropertyCommon(property, null, property.Name);

	protected override string Render(FilesProperty property) => RenderPropertyCommon(property, null, property.Name);

	protected override string Render(FormulaProperty property) => RenderPropertyCommon(property, null, property.Name);

	protected override string Render(LastEditedByProperty property) => RenderPropertyCommon(property, null, property.Name);

	protected override string Render(LastEditedTimeProperty property) => RenderPropertyCommon(property, null, property.Name);

	protected override string Render(MultiSelectProperty property) => RenderPropertyCommon(property, null, property.Name);

	protected override string Render(NumberProperty property) => RenderPropertyCommon(property, null, property.Name);

	protected override string Render(PeopleProperty property) => RenderPropertyCommon(property, null, property.Name);

	protected override string Render(PhoneNumberProperty property) => RenderPropertyCommon(property, null, property.Name);

	protected override string Render(RelationProperty property) => RenderPropertyCommon(property, null, property.Name);

	protected override string Render(RichTextProperty property) => RenderPropertyCommon(property, null, property.Name);

	protected override string Render(RollupProperty property) => RenderPropertyCommon(property, null, property.Name);

	protected override string Render(SelectProperty property) => RenderPropertyCommon(property, null, property.Name);

	protected override string Render(StatusProperty property) => RenderPropertyCommon(property, null, property.Name);

	protected override string Render(TitleProperty property) => RenderPropertyCommon(property, null, property.Name);

	protected override string Render(UrlProperty property) => RenderPropertyCommon(property, null, property.Name);

	protected virtual string RenderPropertyCommon(Property property, string icon, string text)
		=> text + "\t";

	#endregion

	#region Property Values

	protected override string Render(CheckboxPropertyValue propertyValue)
		=> $"[{(propertyValue.Checkbox ? "X" : " ")}]" + "\t";

	protected override string Render(CreatedByPropertyValue propertyValue)
		=> Render(propertyValue.CreatedBy) + "\t";

	protected override string Render(CreatedTimePropertyValue propertyValue)
		=> Render(propertyValue.CreatedTime.ChompEnd(":00")) + "\t";

	protected override string Render(DatePropertyValue propertyValue)
		=> Render(propertyValue.Date) + "\t";

	protected override string Render(EmailPropertyValue propertyValue)
		=> RenderEmailLink(propertyValue.Email, propertyValue.Email) + "\t";

	protected override string Render(FilesPropertyValue propertyValue)
		=> string.Empty + "\t";

	protected override string Render(FormulaPropertyValue propertyValue)
		=> propertyValue.Formula.Type switch {
			"string" => Render(propertyValue.Formula.String),
			"number" => Render(propertyValue.Formula.Number),
			"date" => Render(propertyValue.Formula.Date),
			"array" => Render(propertyValue.Formula.String),
			_ => throw new NotSupportedException(propertyValue.Formula.Type.ToString())
		} + "\t";

	protected override string Render(LastEditedByPropertyValue propertyValue)
		=> Render(propertyValue.LastEditedBy) + "\t";

	protected override string Render(LastEditedTimePropertyValue propertyValue)
		=> Render(propertyValue.LastEditedTime.ChompEnd(":00")) + "\t";

	protected override string Render(MultiSelectPropertyValue propertyValue)
		=> Merge(propertyValue.MultiSelect.Select(x => Render(x.Name))) + "\t";

	protected override string Render(NumberPropertyValue propertyValue)
		=> Render(propertyValue.Number) + "\t";

	protected override string Render(PeoplePropertyValue propertyValue)
		=> Merge(propertyValue.People.Select(Render)) + "\t";

	protected override string Render(PhoneNumberPropertyValue propertyValue)
		=> Render(propertyValue.PhoneNumber) + "\t";

	protected override string Render(RelationPropertyValue propertyValue)
		=> Render(propertyValue.ToPlainText()) + "\t";

	protected override string Render(RichTextPropertyValue propertyValue)
		=> Render(propertyValue.RichText) + "\t";

	protected override string Render(Page page, RollupPropertyValue propertyValue)
		=> propertyValue.Rollup.Type switch {
			"number" => Render(propertyValue.Rollup.Number),
			"date" => Render(propertyValue.Rollup.Date),
			"array" => Merge(propertyValue.Rollup.Array.Select(x => Render(page, x))),
			_ => throw new NotSupportedException(propertyValue.Rollup.Type.ToString())
		} + "\t";

	protected override string Render(SelectPropertyValue propertyValue)
		=> (propertyValue.Select != null ? Render(propertyValue.Select.Name) : string.Empty) + "\t";

	protected override string Render(StatusPropertyValue propertyValue)
		=> Render(propertyValue.Status.Name) + "\t";

	protected override string Render(TitlePropertyValue propertyValue, string pageID)
		=> RenderReference(pageID, false, true) + "\t";

	protected override string Render(UrlPropertyValue propertyValue)
		=> (!string.IsNullOrWhiteSpace(propertyValue.Url) ? propertyValue.Url : string.Empty) +
		   (!string.IsNullOrWhiteSpace(propertyValue.Url) ? Render(propertyValue.Url) : string.Empty) + Environment.NewLine;

	#endregion

	#endregion

	#region Text

	protected override string Render(IEnumerable<RichTextBase> text)
		=> text.Select(Render).ToDelimittedString(string.Empty);

	protected override string Render(RichTextEquation text)
		=> text.Equation.Expression;

	protected override string Render(RichTextMention text)
		=> text.Mention.Type switch {
			"database" => RenderReference(text.Mention.Database.Id, true),
			"date" => Render(text.Mention.Date), // maybe a link to calendar here?
			"link_preview" => Render(text.PlainText),   // TODO: implement
			"page" => RenderReference(text.Mention.Page.Id, true),
			"template_mention" => Render(text.PlainText), // TODO: implement
			"user" => Render(text.Mention.User),
			_ => throw new InvalidOperationException($"Unrecognized mention type '{text.Mention.Type}'")
		};

	protected override string Render(RichTextText text) {
		var isUrl =  text.Text?.Link?.Url is not null;
		var urlInfo = isUrl ? (Url: text.Text.Link.Url, Icon: string.Empty, Indicator: string.Empty ) : default;

		return RenderText(text.Text?.Content ?? text.PlainText ?? string.Empty, isUrl, text.Annotations.IsBold, text.Annotations.IsItalic, text.Annotations.IsStrikeThrough, text.Annotations.IsUnderline, text.Annotations.IsCode, text.Annotations.Color.Value, urlInfo);
	}

	protected override string RenderBadge(string text, Color color)
		=> Render(text) + Environment.NewLine;

	#endregion

	#region Page

	protected override string Render(Page page) 
		=> RenderingContext.Resource.Title + Environment.NewLine +
		   RenderingContext.Resource.Name + Environment.NewLine +
		   RenderChildPageItems() + Environment.NewLine;

	protected override string Render(TableBlock block)
		=> block.HasChildren ? RenderChildPageItems() : string.Empty + Environment.NewLine;

	protected override string Render(TableRowBlock block) {
		var tableNode = RenderingContext.GetParentRenderingNode(2);
		var tableObj = (TableBlock)RenderingContext.GetParentRenderingObject(2);
		var rowIX = tableNode.Children.EnumeratedIndexOf(RenderingContext.CurrentRenderingNode);
		var hasRowHeader = tableObj.Table.HasRowHeader;
		var hasColHeader = tableObj.Table.HasColumnHeader;
		return Merge(block.TableRow.Cells.Select((cell, colIX) => Render(cell))) + Environment.NewLine;
	}

	protected override string Render(AudioBlock block)
		=> $"Audio ({block.Audio})" + Environment.NewLine;

	protected override string Render(BookmarkBlock block)
		=> RenderUnsupported(block) + Environment.NewLine;

	protected override string Render(BreadcrumbBlock block) 
		=> string.Empty;

	protected override string Render(BreadcrumbBlock block, BreadCrumb breadcrumb)
		=> Merge(breadcrumb.Trail.Select(item => $"{item.Text} ({item.Url})")) + Environment.NewLine;

	protected override string RenderBulletedList(IEnumerable<NotionObjectGraph> bullets)
		=> Merge(bullets.Select((bullet, index) => Render(bullet, index + 1))) + Environment.NewLine;

	protected override string RenderBulletedItem(int number, BulletedListItemBlock block)
		=> Render(block.BulletedListItem.RichText) + Environment.NewLine + (block.HasChildren ? RenderChildPageItems() : string.Empty);

	protected override string Render(CalloutBlock block)
		=> Render(block.Callout.RichText) + Environment.NewLine + (block.HasChildren ? RenderChildPageItems() : string.Empty);

	protected override string Render(ChildDatabaseBlock block)
		=> RenderUnsupported(block);

	protected override string Render(ChildPageBlock block) => RenderReference(block.Id, false, true);

	protected override string Render(CodeBlock block)
		=> Render(block.Code.RichText) + Environment.NewLine;

	protected override string Render(ColumnBlock block)
		=> block.HasChildren ? RenderChildPageItems() : string.Empty;

	protected override string Render(ColumnListBlock block)
		=> Merge(RenderingContext.CurrentRenderingNode.Children.Select(x => Render(x))) + Environment.NewLine;

	protected override string Render(DividerBlock block)
		=> "================================================" + Environment.NewLine;

	protected override string Render(EquationBlock block)
		=> block.Equation.Expression + Environment.NewLine;

	protected override string Render(FileBlock block)
		=> Render(block.File.Caption) + Environment.NewLine;

	protected override string Render(HeadingOneBlock block)
		=> Render(block.Heading_1.RichText) + Environment.NewLine + (block.HasChildren ? RenderChildPageItems() : string.Empty);

	protected override string Render(HeadingTwoBlock block)
		=> Render(block.Heading_2.RichText) + Environment.NewLine + (block.HasChildren ? RenderChildPageItems() : string.Empty);

	protected override string Render(HeadingThreeBlock block)
		=> Render(block.Heading_3.RichText) + Environment.NewLine + (block.HasChildren ? RenderChildPageItems() : string.Empty);

	protected override string Render(ImageBlock block)
		=> $"Image ({block.Image}) {block.Image.Caption}" + Environment.NewLine;

	protected override string Render(LinkToPageBlock block)
		=> RenderReference(block.LinkToPage.GetId(), false);

	protected override string RenderNumberedItem(int number, NumberedListItemBlock block)
		=> Render(block.NumberedListItem.RichText) + Environment.NewLine + (block.HasChildren ? RenderChildPageItems() : string.Empty);

	protected override string RenderNumberedList(IEnumerable<NotionObjectGraph> numberedListItems)
		=> Merge(numberedListItems.Select((item, index) => Render(item, index + 1)));   // never call RenderNumberedItem directly to avoid infinite loop due to rendering stack

	protected override string Render(PDFBlock block) => Render(block.PDF.Caption) + $"PDF: {block.PDF}";

	protected override string Render(ParagraphBlock block)
		=> Render(block.Paragraph.RichText) + Environment.NewLine + (block.HasChildren ? RenderChildPageItems() : string.Empty);

	protected override string Render(QuoteBlock block) => $"\"{Render(block.Quote.RichText)}\"" + Environment.NewLine;

	protected override string Render(SyncedBlockBlock block)
		=> RenderUnsupported(block);

	protected override string Render(TableOfContentsBlock block) => "Table of Contents" + Environment.NewLine;

	protected override string Render(TemplateBlock block)
		=> RenderUnsupported(block);

	protected override string Render(ToDoBlock block)
		=> Render(block.ToDo.RichText) + Environment.NewLine + (block.HasChildren ? RenderChildPageItems() : string.Empty);

	protected override string Render(ToggleBlock block)
		=> Render(block.Toggle.RichText) + Environment.NewLine + (block.HasChildren ? RenderChildPageItems() : string.Empty);

	protected override string RenderText(string content, bool isUrl, bool isBold, bool isItalic, bool isStrikeThrough, bool isUnderline, bool isCode, Color color, (string Url, string Icon, string Indicator) urlInfo = default)
		=> (isUrl ? urlInfo.Url : content) ?? string.Empty + Environment.NewLine + Environment.NewLine;

	protected override string RenderReference(string objectID, bool isInline, bool omitIndicator = false) => string.Empty + Environment.NewLine;

	protected override string Render(IPageIcon pageIcon) => string.Empty;

	protected override string Render(VideoBlock block) {
		switch (block.Video) {
			case ExternalFile externalFile: {
				if (Tools.Url.IsVideoSharingUrl(externalFile.External.Url, out var platform, out var videoID)) {
					switch(platform) {
						case VideoSharingPlatform.YouTube:
							return $"[YouTube]({videoID}) {block.Video.Caption} " + Environment.NewLine;

						case VideoSharingPlatform.Rumble:
							return $"[Rumble]({videoID}) {block.Video.Caption} " + Environment.NewLine;

						case VideoSharingPlatform.BitChute:
							return $"[BitChute]({videoID}) {block.Video.Caption} " + Environment.NewLine;
						case VideoSharingPlatform.Vimeo:
							return $"[Vimeo]({videoID}) {block.Video.Caption} " + Environment.NewLine;
						default:
							throw new NotSupportedException(platform.ToString());
					}
				}

				return $"[Video]({externalFile.External.Url}) {block.Video.Caption} " + Environment.NewLine;

			}
			case UploadedFile uploadedFile: {
				return $"[Video]({uploadedFile.File.Url}) {block.Video.Caption} " + Environment.NewLine;
			}
			default:
				throw new ArgumentOutOfRangeException(nameof(block), block, null);
		}
	}
	protected override string Render(EmbedBlock block) {
		var isXCom = 
			block.Embed.Url.Contains("twitter", StringComparison.InvariantCultureIgnoreCase) ||
			block.Embed.Url.Contains("x.com", StringComparison.InvariantCultureIgnoreCase);

		if (isXCom) {
			return block.Embed.Url + Environment.NewLine;
		}
		return RenderUnsupported(block);
	}

	protected override string RenderUnsupported(object @object) => string.Empty + Environment.NewLine;

	#endregion

	#region Aux

	protected virtual string ToString(Annotations annotations) {
		var sb = new FastStringBuilder();
		if (annotations.IsBold)
			sb.Append("B");
		if (annotations.IsCode)
			sb.Append("C");
		if (annotations.IsItalic)
			sb.Append("I");
		if (annotations.IsStrikeThrough)
			sb.Append("S");
		if (annotations.IsUnderline)
			sb.Append("U");
		if (sb.Length > 0) {
			sb.Prepend("[");
			sb.Append("]");
		}
		return sb.ToString();
	}

	protected virtual string ToString(ParentObject.ParentType blockType)
		=> $"[{blockType.ToString()}]";

	protected virtual string ToString(BlockType blockType)
		=> $"{blockType.GetAttribute<EnumMemberAttribute>().Value}";

	protected virtual string ToString(RichTextType richTextType)
		=> $"{richTextType.GetAttribute<EnumMemberAttribute>().Value}";

	protected virtual string ToString(PropertyValueType propertyValueType)
		=> $"{propertyValueType.GetAttribute<EnumMemberAttribute>().Value}";

	protected string ToColorString(Color color)
		=> color.GetAttribute<EnumMemberAttribute>()?.Value.Replace("_", "-") ?? throw new InvalidOperationException($"Color '{color}' did not have {nameof(EnumMemberAttribute)} defined");

	public static string ToPrismLanguage(string language)
			=> language switch {
				"abap" => "abap",
				"agda" => "agda",
				"ardunio" => "arduino",
				"assembly" => "armasm",
				"bash" => "bash",
				"basic" => "basic",
				"bnf" => "bnf",
				"c" => "c",
				"c#" => "csharp",
				"c++" => "cpp",
				"clojure" => "clojure",
				"coffeescript" => "coffeescript",
				"coq" => "coq",
				"css" => "css",
				"dart" => "dart",
				"dhall" => "dhall",
				"diff" => "diff",
				"docker" => "docker",
				"ebnf" => "ebnf",
				"elixer" => "elixir",
				"elm" => "elm",
				"erlang" => "erlang",
				"f#" => "fsharp",
				"flow" => "flow",
				"fortran" => "fortran",
				"gherkin" => "gherkin",
				"glsl" => "glsl",
				"go" => "go",
				"graphql" => "graphql",
				"groovy" => "groovy",
				"haskell" => "haskell",
				"html" => "html",
				"idris" => "idris",
				"java" => "java",
				"javascript" => "javascript",
				"json" => "json",
				"julia" => "julia",
				"kotlin" => "kotlin",
				"latex" => "latex",
				"less" => "less",
				"lisp" => "lisp",
				"livescript" => "typescript",
				"llvm" => "llvm",
				"llvmir" => "llvm",
				"lua" => "lua",
				"makefile" => "makefile",
				"markdown" => "markdown",
				"markup" => "markup-templating",
				"matlab" => "matlab",
				"mermaid" => "mermaid",
				"nix" => "nix",
				"objective-c" => "objc",
				"ocaml" => "ocaml",
				"pascal" => "pascal",
				"perl" => "perl",
				"php" => "php",
				"plain text" => "text",
				"powershell" => "powershell",
				"prolog" => "prolog",
				"protobuf" => "protobuf",
				"python" => "python",
				"r" => "r",
				"racket" => "racket",
				"reason" => "reason",
				"ruby" => "ruby",
				"rust" => "rust",
				"sass" => "sass",
				"scala" => "scala",
				"scheme" => "scheme",
				"scss" => "scss",
				"shell" => "shell-session",
				"solidity" => "solidity",
				"sql" => "sql",
				"swift" => "swift",
				"toml" => "toml",
				"typescript" => "typescript",
				"vb.net" => "vbnet",
				"verilog" => "verilog",
				"vhdl" => "vhdl",
				"visual basic" => "vb",
				"webassembly" => "wasm",
				"xml" => "xml",
				"yaml" => "yaml",
				_ => "text"
			};

	#endregion

}