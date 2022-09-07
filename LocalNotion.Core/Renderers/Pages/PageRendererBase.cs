using Hydrogen;
using LocalNotion.Core;
using Notion.Client;

namespace LocalNotion.Core;

public abstract class PageRendererBase<TOutput> : IPageRenderer {

	protected PageRendererBase(LocalNotionPage page, NotionObjectGraph pageGraph, PageProperties pageProperties, IDictionary<string, IObject> pageObjects, IUrlResolver resolver,  IBreadCrumbGenerator breadCrumbGenerator, Action<string, TOutput> fileSerializer) {
		Guard.ArgumentNotNull(page, nameof(page));
		Guard.ArgumentNotNull(pageGraph, nameof(pageGraph));
		Guard.ArgumentNotNull(pageProperties, nameof(pageProperties));
		Guard.ArgumentNotNull(pageObjects, nameof(pageObjects));
		Guard.ArgumentNotNull(resolver, nameof(resolver));
		Guard.ArgumentNotNull(breadCrumbGenerator, nameof(breadCrumbGenerator));
		Guard.ArgumentNotNull(fileSerializer, nameof(fileSerializer));
		Page = page;
		PageGraph = pageGraph;
		PageProperties = pageProperties;
		PageObjects = pageObjects;
		Resolver = resolver;
		BreadCrumbGenerator = breadCrumbGenerator;
		Serializer = fileSerializer;
		RenderingStack = new StackList<NotionObjectGraph>();
	}

	protected LocalNotionPage Page { get; }

	protected NotionObjectGraph PageGraph { get; }

	protected PageProperties PageProperties { get; set; }

	protected IDictionary<string, IObject> PageObjects { get; set; }

	protected IUrlResolver Resolver { get; }

	protected IBreadCrumbGenerator BreadCrumbGenerator { get; }

	protected Action<string, TOutput> Serializer { get; }
	
	protected StackList<NotionObjectGraph> RenderingStack { get; }

	protected NotionObjectGraph CurrentRenderingNode => GetParentRenderingNode(1);

	protected IObject CurrentRenderingObject => GetParentRenderingObject(1);

	protected NotionObjectGraph GetParentRenderingNode(int level) => RenderingStack.TryPeek(out var value, level) ? value : null;

	protected IObject GetParentRenderingObject(int level) => RenderingStack.TryPeek(out var value, level) ? PageObjects.TryGetValue(value.ObjectID, out var obj) ? obj : null : null;

	public void Render(string destinationFile) {
		Guard.ArgumentNotNull(destinationFile, nameof(destinationFile));
		Guard.Against(File.Exists(destinationFile), $"File '{destinationFile}' already exists");
		var output = Render(PageGraph);
		Serializer(destinationFile, output);
	}

	protected virtual TOutput Render(NotionObjectGraph objectGraph) {
		RenderingStack.Push(objectGraph);
		try {
			return CurrentRenderingObject switch {
				TableBlock x => Render(x),
				TableRowBlock x => Render(x),
				Database x => Render(x),
				Page x => RenderingStack.Count == 1 ? Render(x) : Render(x.AsChildPageBlock(PageProperties)),   // Nested pages are stored as "Page" objects in LocalNotion
				User x => Render(x),
				AudioBlock x => Render(x),
				BookmarkBlock x => Render(x),
				BreadcrumbBlock x => Render(x),
				BulletedListItemBlock x => throw new InvalidOperationException($"{nameof(BulletedListItemBlock)} are rendered collectively"),
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
				HeadingThreeeBlock x => Render(x),
				HeadingTwoBlock x => Render(x),
				ImageBlock x => Render(x),
				LinkToPageBlock x => Render(x),
				NumberedListItemBlock x => throw new InvalidOperationException($"{nameof(NumberedListItemBlock)} are rendered collectively"),
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
				_ => throw new ArgumentOutOfRangeException()
			};
		} finally {
			RenderingStack.Pop();
		}
	}

	protected virtual TOutput RenderChildItems() {
		return Merge(
			CurrentRenderingNode
				.Children
				.GroupAdjacentBy(x => PageObjects[x.ObjectID].GetType())
				.SelectMany(adjacentObjects => adjacentObjects.Key switch {
					Type bulletListType when bulletListType == typeof(BulletedListItemBlock) => new[] { Render(adjacentObjects.Select(x => PageObjects[x.ObjectID]).Cast<BulletedListItemBlock>() ) },
					Type numberedListType when numberedListType == typeof(NumberedListItemBlock) => new[] { Render(adjacentObjects.Select(x => PageObjects[x.ObjectID]).Cast<NumberedListItemBlock>()) },
					_ => adjacentObjects.Select(Render)
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

	protected virtual TOutput Render(FileObject fileObject)
		=> fileObject switch {
			ExternalFile x => Render(x),
			UploadedFile x => Render(x),
			_ => throw new ArgumentOutOfRangeException(nameof(fileObject), fileObject, null)
		};

	protected virtual TOutput Render(IBotOwner botOwner)
		=> botOwner switch {
			UserOwner x => Render(x),
			WorkspaceIntegrationOwner x => Render(x),
			_ => throw new ArgumentOutOfRangeException(nameof(botOwner), botOwner, null)
		};

	protected virtual TOutput Render(IPageIcon pageIcon)
		=> pageIcon switch {
			EmojiObject emojiObject => Render(emojiObject),
			ExternalFile externalFile => Render(externalFile),
			UploadedFile uploadedFile => Render(uploadedFile),
			_ => throw new NotSupportedException()
		};

	protected virtual TOutput Render(PropertyValue propertyValue)
		=> propertyValue switch {
			CheckboxPropertyValue x => Render(x),
			CreatedByPropertyValue x => Render(x),
			CreatedTimePropertyValue x => Render(x),
			DatePropertyValue x => Render(x),
			EmailPropertyValue x => Render(x),
			FilesPropertyValue x => Render(x),
			FormulaPropertyValue x => Render(x),
			LastEditedByPropertyValue x => Render(x),
			LastEditedTimePropertyValue x => Render(x),
			MultiSelectPropertyValue x => Render(x),
			NumberPropertyValue x => Render(x),
			PeoplePropertyValue x => Render(x),
			PhoneNumberPropertyValue x => Render(x),
			RelationPropertyValue x => Render(x),
			RichTextPropertyValue x => Render(x),
			RollupPropertyValue x => Render(x),
			SelectPropertyValue x => Render(x),
			TitlePropertyValue x => Render(x),
			UrlPropertyValue x => Render(x),
			_ => throw new ArgumentOutOfRangeException(nameof(propertyValue), propertyValue, null)
		};

	protected virtual TOutput Render(VideoBlock block) {
		switch (block.Video) {
			case ExternalFile externalFile: {
				var youTubeVideoID = Tools.Url.ExtractVideoIdFromYouTubeUrl(externalFile.External.Url);
				if (!string.IsNullOrWhiteSpace(youTubeVideoID))
					return RenderYouTubeEmbed(block, youTubeVideoID);
				return RenderVideoEmbed(block, externalFile.External.Url);
			}
			case UploadedFile uploadedFile: {
				var url = Resolver.ResolveUploadedFileUrl(LocalNotionResourceType.Page, Page.ID, uploadedFile, out _);
				return RenderVideoEmbed(block, url);
			}
			default:
				throw new ArgumentOutOfRangeException(nameof(block), block, null);
		}
	}

	protected virtual TOutput Render(EmbedBlock block) {
		if (block.Embed.Url.Contains("twitter", StringComparison.InvariantCultureIgnoreCase)) {
			return RenderTwitterEmbed(block, block.Embed.Url);
		}
		return RenderUnsupported(block);
	}

	#endregion

	#region Values

	protected abstract TOutput Render(EmojiObject emojiObject);

	protected abstract TOutput Render(ExternalFile externalFile);

	protected abstract TOutput Render(UploadedFile uploadedFile);

	protected abstract TOutput Render(Link link);

	protected abstract string Render(Date date);

	protected abstract string Render(DateTime? date);

	protected abstract string Render(bool? val);

	protected abstract string Render(double? val);

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

	protected abstract TOutput Render(Database database);

	#endregion

	#region Rich Text

	protected abstract TOutput Render(IEnumerable<RichTextBase> text);

	protected abstract TOutput Render(RichTextEquation text);

	protected abstract TOutput Render(RichTextMention text);

	protected abstract TOutput Render(RichTextText text);

	#endregion

	#region Propery Values

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

	protected abstract TOutput Render(RollupPropertyValue propertyValue);

	protected abstract TOutput Render(SelectPropertyValue propertyValue);

	protected abstract TOutput Render(TitlePropertyValue propertyValue);

	protected abstract TOutput Render(UrlPropertyValue propertyValue);

	#endregion

	#region Page & Blocks

	protected abstract TOutput Render(Page page);

	protected abstract TOutput Render(TableBlock block);

	protected abstract TOutput Render(TableRowBlock block);

	protected abstract TOutput Render(AudioBlock block);

	protected abstract TOutput Render(BookmarkBlock block);

	protected virtual TOutput Render(BreadcrumbBlock block) 
		=> Render(block, BreadCrumbGenerator.CalculateBreadcrumb(Page.ID));

	protected abstract TOutput Render(BreadcrumbBlock block, BreadCrumb breadcrumb);
	protected virtual TOutput Render(IEnumerable<BulletedListItemBlock> bullets) 
		=> Merge(bullets.Select((b, i) => Render(i+1, b)));

	protected abstract TOutput Render(int number, BulletedListItemBlock block);

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

	protected abstract TOutput Render(HeadingThreeeBlock block);

	protected abstract TOutput Render(HeadingTwoBlock block);

	protected abstract TOutput Render(ImageBlock block);

	protected abstract TOutput Render(LinkToPageBlock block);

	protected virtual TOutput Render(IEnumerable<NumberedListItemBlock> numberedItems) 
		=> Merge(numberedItems.Select((b, i) => Render(i+1, b)));

	protected abstract TOutput Render(int number, NumberedListItemBlock block);

	protected abstract TOutput Render(PDFBlock block);

	protected abstract TOutput Render(ParagraphBlock block);

	protected abstract TOutput Render(QuoteBlock block);

	protected abstract TOutput Render(SyncedBlockBlock block);

	protected abstract TOutput Render(TableOfContentsBlock block);

	protected abstract TOutput Render(TemplateBlock block);

	protected abstract TOutput Render(ToDoBlock block);

	protected abstract TOutput Render(ToggleBlock block);

	protected abstract TOutput RenderYouTubeEmbed(VideoBlock videoBlock, string youTubeVideoID);

	protected abstract TOutput RenderVideoEmbed(VideoBlock videoBlock, string url);

	protected abstract TOutput RenderTwitterEmbed(EmbedBlock embedBlock, string url);

	protected abstract TOutput RenderUnsupported(object @object);
	
	#endregion

}
