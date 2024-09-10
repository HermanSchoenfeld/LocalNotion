using Hydrogen;
using Notion.Client;

namespace LocalNotion.Core;

public abstract class RecursiveRendererBase<TOutput> : IRenderer<TOutput> {

	protected RecursiveRendererBase(RenderMode renderMode, ILogger logger) {
		Mode = renderMode;
		Logger = logger;
	}

	protected RenderMode Mode { get; }

	protected ILogger Logger { get; }

	protected PageRenderingContext RenderingContext { get; set; }

	public virtual TOutput Render(LocalNotionEditableResource page, NotionObjectGraph pageGraph, IDictionary<string, IObject> notionObjects, string renderOutputPath) {
		using (EnterRenderingContext(new PageRenderingContext {
			Resource = page,
			RenderOutputPath = renderOutputPath,
			PageGraph = pageGraph,
			PageObjects = notionObjects,
		})) {
			return Render(RenderingContext.PageGraph);
		}
	}

	protected virtual void OnRenderingContextCreated() {
	}

	protected IDisposable EnterRenderingContext(PageRenderingContext context) {
		Guard.ArgumentNotNull(context, nameof(context));
		Guard.Ensure(RenderingContext is null, "Rendering context is already entered");
		RenderingContext = context;
		OnRenderingContextCreated();
		return new ActionDisposable(() => RenderingContext = null);
	}

	protected virtual TOutput Render(NotionObjectGraph objectGraph, int? index = null) {
		RenderingContext.RenderingStack.Push(objectGraph);
		try {
			return RenderingContext.CurrentRenderingObject switch {
				TableBlock x => Render(x),
				TableRowBlock x => Render(x),
				Database x => Render(x, RenderingContext.RenderingStack.Count > 1),
				Page x => RenderingContext.RenderingStack.Count == 1 ? Render(x) : Render(x.AsChildPageBlock()), // Nested pages are stored as "Page" objects in LocalNotion // Render(x),
				User x => Render(x),
				AudioBlock x => Render(x),
				BookmarkBlock x => Render(x),
				BreadcrumbBlock x => Render(x),
				BulletedListItemBlock x => RenderBulletedItem(index.Value, x),
				CalloutBlock x => Render(x),
				ChildDatabaseBlock x => Render(x),
				ChildPageBlock x => Render(x),
				CodeBlock x => Render(x),
				ColumnBlock x => Render(x),
				ColumnListBlock x => Render(x),
				DividerBlock x => Render(x),
				EmbedBlock x => Render(x),
				EquationBlock x => Render(x),
				FileBlock x => Render(x),
				HeadingOneBlock x => Render(x),
				HeadingTwoBlock x => Render(x),
				HeadingThreeBlock x => Render(x),
				ImageBlock x => Render(x),
				LinkToPageBlock x => Render(x),
				NumberedListItemBlock x => RenderNumberedItem(index.Value, x),
				PDFBlock x => Render(x),
				ParagraphBlock x => Render(x),
				QuoteBlock x => Render(x),
				SyncedBlockBlock x => Render(x),
				TableOfContentsBlock x => Render(x),
				TemplateBlock x => Render(x),
				ToDoBlock x => Render(x),
				ToggleBlock x => Render(x),
				UnsupportedBlock x => RenderUnsupported(x),
				VideoBlock x => Render(x),
				LinkPreviewBlock x => RenderUnsupported(x),
				_ => throw new ArgumentOutOfRangeException()
			};
		} finally {
			RenderingContext.RenderingStack.Pop();
		}
	}

	protected virtual TOutput RenderChildPageItems() {
		return Merge(
			RenderingContext.CurrentRenderingNode
				.Children
				.GroupAdjacentBy(x => RenderingContext.PageObjects[x.ObjectID].GetType())
				.SelectMany(adjacentObjects => adjacentObjects.Key switch {
					Type bulletListType when bulletListType == typeof(BulletedListItemBlock) => new[] { RenderBulletedList(adjacentObjects) },
					Type numberedListType when numberedListType == typeof(NumberedListItemBlock) => new[] { RenderNumberedList(adjacentObjects) },
					_ => adjacentObjects.Select(x => Render(x))
				})
				.ToArray()
		);
	}

	protected abstract TOutput Merge(IEnumerable<TOutput> outputs);


	#region Base renderers

	protected virtual TOutput Render(RichTextBase richText)
		=> richText switch {
			RichTextEquation x => Render(x),
			RichTextMention x => Render(x),
			RichTextText x => Render(x),
			_ => throw new ArgumentOutOfRangeException(nameof(richText), richText, null)
		};

	protected virtual TOutput Render(IBotOwner botOwner)
		=> botOwner switch {
			UserOwner x => Render(x),
			WorkspaceIntegrationOwner x => Render(x),
			_ => throw new ArgumentOutOfRangeException(nameof(botOwner), botOwner, null)
		};

	protected abstract TOutput Render(IPageIcon pageIcon);

	protected abstract TOutput Render(VideoBlock block);

	protected virtual TOutput Render(EmbedBlock block) {
		var isXCom = 
			block.Embed.Url.Contains("twitter", StringComparison.InvariantCultureIgnoreCase) ||
			block.Embed.Url.Contains("x.com", StringComparison.InvariantCultureIgnoreCase);

		if (isXCom) {
			return RenderTwitterEmbed(block, block.Embed.Url);
		}
		return RenderUnsupported(block);
	}

	#endregion

	#region Values

	protected abstract TOutput Render(EmojiObject emojiObject);

	protected abstract TOutput Render(Link link);

	protected abstract string Render(Date date);

	protected abstract string Render(DateTime? date);

	protected abstract string Render(bool? val);

	protected abstract string Render(double? val);

	protected abstract string Render(string val);

	#endregion

	#region Owners

	protected abstract TOutput Render(UserOwner owner);

	protected abstract TOutput Render(WorkspaceIntegrationOwner owner);

	#endregion

	#region Users

	protected abstract TOutput Render(User user);

	protected abstract TOutput Render(Person person);

	protected abstract TOutput Render(Bot user);

	#endregion

	#region Database 

	protected abstract TOutput Render(Database database, bool inline);

	#region Properties

	protected virtual TOutput Render(string key, Property property)
		=> property switch {
			CheckboxProperty checkboxProperty => Render(checkboxProperty),
			CreatedByProperty createdByProperty => Render(createdByProperty),
			CreatedTimeProperty createdTimeProperty => Render(createdTimeProperty),
			DateProperty dateProperty => Render(dateProperty),
			EmailProperty emailProperty => Render(emailProperty),
			FilesProperty filesProperty => Render(filesProperty),
			FormulaProperty formulaProperty => Render(formulaProperty),
			LastEditedByProperty lastEditedByProperty => Render(lastEditedByProperty),
			LastEditedTimeProperty lastEditedTimeProperty => Render(lastEditedTimeProperty),
			MultiSelectProperty multiSelectProperty => Render(multiSelectProperty),
			NumberProperty numberProperty => Render(numberProperty),
			PeopleProperty peopleProperty => Render(peopleProperty),
			PhoneNumberProperty phoneNumberProperty => Render(phoneNumberProperty),
			RelationProperty relationProperty => Render(relationProperty),
			RichTextProperty richTextProperty => Render(richTextProperty),
			RollupProperty rollupProperty => Render(rollupProperty),
			SelectProperty selectProperty => Render(selectProperty),
			StatusProperty statusProperty => Render(statusProperty),
			TitleProperty titleProperty => Render(titleProperty),
			UrlProperty urlProperty => Render(urlProperty),
			_ => RenderUnsupported(property)
		};

	protected abstract TOutput Render(CheckboxProperty property);

	protected abstract TOutput Render(CreatedByProperty property);

	protected abstract TOutput Render(CreatedTimeProperty property);

	protected abstract TOutput Render(DateProperty property);

	protected abstract TOutput Render(EmailProperty property);

	protected abstract TOutput Render(FilesProperty property);

	protected abstract TOutput Render(FormulaProperty property);

	protected abstract TOutput Render(LastEditedByProperty property);

	protected abstract TOutput Render(LastEditedTimeProperty property);

	protected abstract TOutput Render(MultiSelectProperty property);

	protected abstract TOutput Render(NumberProperty property);

	protected abstract TOutput Render(PeopleProperty property);

	protected abstract TOutput Render(PhoneNumberProperty property);

	protected abstract TOutput Render(RelationProperty property);

	protected abstract TOutput Render(RichTextProperty property);

	protected abstract TOutput Render(RollupProperty property);

	protected abstract TOutput Render(SelectProperty property);

	protected abstract TOutput Render(StatusProperty property);

	protected abstract TOutput Render(TitleProperty property);

	protected abstract TOutput Render(UrlProperty property);

	#endregion

	#region Property Values

	protected virtual TOutput Render(Page page, PropertyValue propertyValue)
		=> propertyValue switch {
			CheckboxPropertyValue checkboxPropertyValue => Render(checkboxPropertyValue),
			CreatedByPropertyValue createdByPropertyValue => Render(createdByPropertyValue),
			CreatedTimePropertyValue createdTimePropertyValue => Render(createdTimePropertyValue),
			DatePropertyValue datePropertyValue => Render(datePropertyValue),
			EmailPropertyValue emailPropertyValue => Render(emailPropertyValue),
			FilesPropertyValue filesPropertyValue => Render(filesPropertyValue),
			FormulaPropertyValue formulaPropertyValue => Render(formulaPropertyValue),
			LastEditedByPropertyValue lastEditedByPropertyValue => Render(lastEditedByPropertyValue),
			LastEditedTimePropertyValue lastEditedTimePropertyValue => Render(lastEditedTimePropertyValue),
			MultiSelectPropertyValue multiSelectPropertyValue => Render(multiSelectPropertyValue),
			NumberPropertyValue numberPropertyValue => Render(numberPropertyValue),
			PeoplePropertyValue peoplePropertyValue => Render(peoplePropertyValue),
			PhoneNumberPropertyValue phoneNumberPropertyValue => Render(phoneNumberPropertyValue),
			RelationPropertyValue relationPropertyValue => Render(relationPropertyValue),
			RichTextPropertyValue richTextPropertyValue => Render(richTextPropertyValue),
			RollupPropertyValue rollupPropertyValue => Render(page, rollupPropertyValue),
			SelectPropertyValue selectPropertyValue => Render(selectPropertyValue),
			StatusPropertyValue statusPropertyValue => Render(statusPropertyValue),
			TitlePropertyValue titlePropertyValue => Render(titlePropertyValue, page.Id),
			UrlPropertyValue urlPropertyValue => Render(urlPropertyValue),
			_ => RenderUnsupported(propertyValue)
		};

	protected abstract TOutput Render(CheckboxPropertyValue propertyValue);

	protected abstract TOutput Render(CreatedByPropertyValue propertyValue);

	protected abstract TOutput Render(CreatedTimePropertyValue propertyValue);

	protected abstract TOutput Render(DatePropertyValue propertyValue);

	protected abstract TOutput Render(EmailPropertyValue propertyValue);

	protected abstract TOutput Render(FilesPropertyValue propertyValue);

	protected abstract TOutput Render(FormulaPropertyValue propertyValue);

	protected abstract TOutput Render(LastEditedByPropertyValue propertyValue);

	protected abstract TOutput Render(LastEditedTimePropertyValue propertyValue);

	protected abstract TOutput Render(MultiSelectPropertyValue propertyValue);

	protected abstract TOutput Render(NumberPropertyValue propertyValue);

	protected abstract TOutput Render(PeoplePropertyValue propertyValue);

	protected abstract TOutput Render(PhoneNumberPropertyValue propertyValue);

	protected abstract TOutput Render(RelationPropertyValue propertyValue);

	protected abstract TOutput Render(RichTextPropertyValue propertyValue);

	protected abstract TOutput Render(Page page, RollupPropertyValue propertyValue);

	protected abstract TOutput Render(SelectPropertyValue propertyValue);

	protected abstract TOutput Render(StatusPropertyValue propertyValue);

	protected abstract TOutput Render(TitlePropertyValue propertyValue, string pageID);

	protected abstract TOutput Render(UrlPropertyValue propertyValue);

	#endregion

	#endregion

	#region Rich Text

	protected abstract TOutput Render(IEnumerable<RichTextBase> text);

	protected abstract TOutput Render(RichTextEquation text);

	protected abstract TOutput Render(RichTextMention text);

	protected abstract TOutput Render(RichTextText text);

	protected abstract string RenderBadge(string text, Color color);

	#endregion

	#region Page & Blocks

	protected abstract TOutput Render(Page page);

	protected abstract TOutput Render(TableBlock block);

	protected abstract TOutput Render(TableRowBlock block);

	protected abstract TOutput Render(AudioBlock block);

	protected abstract TOutput Render(BookmarkBlock block);

	protected abstract TOutput Render(BreadcrumbBlock block);

	protected abstract TOutput Render(BreadcrumbBlock block, BreadCrumb breadcrumb);

	protected virtual TOutput RenderBulletedList(IEnumerable<NotionObjectGraph> bullets)
		=> Merge(bullets.Select((b, i) => Render(b, i + 1)));

	protected abstract TOutput RenderBulletedItem(int number, BulletedListItemBlock block);

	protected abstract TOutput Render(CalloutBlock block);

	protected abstract TOutput Render(ChildDatabaseBlock block);

	protected abstract TOutput Render(ChildPageBlock block);

	protected abstract TOutput Render(CodeBlock block);

	protected abstract TOutput Render(ColumnBlock block);

	protected abstract TOutput Render(ColumnListBlock block);

	protected abstract TOutput Render(DividerBlock block);

	protected abstract TOutput Render(EquationBlock block);

	protected abstract TOutput Render(FileBlock block);

	protected abstract TOutput Render(HeadingOneBlock block);

	protected abstract TOutput Render(HeadingTwoBlock block);

	protected abstract TOutput Render(HeadingThreeBlock block);

	protected abstract TOutput Render(ImageBlock block);

	protected abstract TOutput Render(LinkToPageBlock block);

	protected virtual TOutput RenderNumberedList(IEnumerable<NotionObjectGraph> numberedItems)
		=> Merge(numberedItems.Select((b, i) => Render(b, i + 1)));

	protected abstract TOutput RenderNumberedItem(int number, NumberedListItemBlock block);

	protected abstract TOutput Render(PDFBlock block);

	protected abstract TOutput Render(ParagraphBlock block);

	protected abstract TOutput Render(QuoteBlock block);

	protected abstract TOutput Render(SyncedBlockBlock block);

	protected abstract TOutput Render(TableOfContentsBlock block);

	protected abstract TOutput Render(TemplateBlock block);

	protected abstract TOutput Render(ToDoBlock block);

	protected abstract TOutput Render(ToggleBlock block);

	protected abstract TOutput RenderText(string content, bool isUrl, bool isBold, bool isItalic, bool isStrikeThrough, bool isUnderline, bool isCode, Color color, (string Url, TOutput Icon, TOutput Indicator) urlInfo = default);

	protected abstract TOutput RenderReference(string objectID, bool isInline, bool omitIndicator = false);

	protected abstract TOutput RenderYouTubeEmbed(VideoBlock videoBlock, string videoID);

	protected abstract TOutput RenderVimeoEmbed(VideoBlock videoBlock, string videoID);

	protected abstract TOutput RenderVideoEmbed(VideoBlock videoBlock, string url);

	protected abstract TOutput RenderTwitterEmbed(EmbedBlock embedBlock, string url);

	protected abstract TOutput RenderUnsupported(object @object);

	#endregion


	#region Inner classes

	protected class PageRenderingContext {
		public LocalNotionEditableResource Resource { get; set; }

		public string [] Themes { get; set; } = [];

		public IDictionary<string, object> AmbientTokens { get; set; } = new Dictionary<string, object>();

		public string RenderOutputPath { get; set; }

		public NotionObjectGraph PageGraph { get; set; }

		public IDictionary<string, IObject> PageObjects { get; set; }

		public StackList<NotionObjectGraph> RenderingStack { get; } = new();

		public NotionObjectGraph CurrentRenderingNode => GetParentRenderingNode(1);

		public IObject CurrentRenderingObject => GetParentRenderingObject(1);

		public NotionObjectGraph GetParentRenderingNode(int level) => RenderingStack.TryPeek(out var value, level) ? value : null;

		public IObject GetParentRenderingObject(int level) => RenderingStack.TryPeek(out var value, level) ? PageObjects.TryGetValue(value.ObjectID, out var obj) ? obj : null : null;

	}

	#endregion

}
