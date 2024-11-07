using Hydrogen;
using Hydrogen.Data;
using Notion.Client;
using System.Runtime.Serialization;
using System.Text;
using System.Text.RegularExpressions;
using AngleSharp.Html.Parser;
using Tools;
using AngleSharp.Dom;

namespace LocalNotion.Core;

public class HtmlRenderer : RecursiveRendererBase<string> {
	private int _toggleCount = 0;
	private DictionaryChain<string, object> _tokens;

	public HtmlRenderer(
		RenderMode renderMode,
		ILocalNotionRepository repository,
		HtmlThemeManager themeManager,
		ILinkGenerator resolver,
		IBreadCrumbGenerator breadCrumbGenerator,
		ILogger logger
	) : base(renderMode, logger) {
		Guard.ArgumentNotNull(repository, nameof(repository));
		Guard.ArgumentNotNull(themeManager, nameof(themeManager));
		Guard.ArgumentNotNull(resolver, nameof(resolver));
		Guard.ArgumentNotNull(breadCrumbGenerator, nameof(breadCrumbGenerator));
		Repository = repository;
		ThemeManager = themeManager;
		Resolver = resolver;
		BreadCrumbGenerator = breadCrumbGenerator;
	}


	protected bool SuppressFormatting { get; private set; }

	protected ILocalNotionRepository Repository { get; }

	protected HtmlThemeManager ThemeManager { get; }

	protected ILinkGenerator Resolver { get; }

	protected IBreadCrumbGenerator BreadCrumbGenerator { get; }

	public static string Format(string html) {
		var parser = new HtmlParser();
		var document = parser.ParseDocument(html);
		var formatter = new CleanHtmlFormatter();
		var stringBuilder = new StringBuilder();	
		using var writer =new StringWriter(stringBuilder);
		document.ToHtml(writer, formatter);
		return stringBuilder.ToString();
	}

	protected override void OnRenderingContextCreated() {
		Guard.Ensure(RenderingContext is not null, "Rendering context was not defined");
		Guard.Ensure(RenderingContext.RenderOutputPath is not null, "Rendering context did not specify render output path");

		if (RenderingContext.Themes is null || RenderingContext.Themes.Length == 0) 
			RenderingContext.Themes = Repository.DefaultThemes;
		
		var themes = ThemeManager.FilterAvailableThemes(RenderingContext.Themes).Distinct().ToArray();
		
		var loadedThemes = themes.Select(ThemeManager.LoadTheme).ToArray();

		Guard.Ensure(loadedThemes.Length > 0, "No valid themes available for rendering (at least 1 is required)");
		Guard.Ensure(loadedThemes.All(x => x is HtmlThemeInfo), $"Must all be instances of '{nameof(HtmlThemeInfo)}'");
		
		_tokens = ThemeManager.LoadThemeTokens(
			loadedThemes.Cast<HtmlThemeInfo>().ToArray(), 
			RenderingContext.RenderOutputPath, 
			Repository.Paths.Mode, Mode, 
			out var suppressFormatting
		);

		if (RenderingContext.AmbientTokens.Count > 0)
			_tokens = new DictionaryChain<string, object>(RenderingContext.AmbientTokens, _tokens);

		SuppressFormatting = suppressFormatting;
	}
	protected override string Merge(IEnumerable<string> outputs)
		=> outputs.ToDelimittedString(string.Empty);

	#region Values

	protected override string Render(EmbedBlock block) {
		var isXCom = 
			block.Embed.Url.Contains("twitter", StringComparison.InvariantCultureIgnoreCase) ||
			block.Embed.Url.Contains("x.com", StringComparison.InvariantCultureIgnoreCase);

		if (isXCom) {
			return RenderTemplate(
				"embed_x",
				new RenderTokens(block) {
					["url"] = SanitizeUrl(block.Embed.Url),
					["caption"] = Render(block.Embed.Caption)
				}
			);
		}

		if (Tools.Url.IsVideoSharingUrl(block.Embed.Url, out var platform, out var videoID)) 
			return RenderSocialMediaVideo(block, platform, videoID, Render(block.Embed.Caption));
		

		return RenderUnsupported(block);
	}

	protected override string Render(EmojiObject emojiObject)
		 => emojiObject.Emoji;

	protected override string Render(Link link) =>
		!string.IsNullOrWhiteSpace(link.Url) ?
		RenderTemplate(
				"text_link",
				new RenderTokens {
					["url"] = SanitizeUrl(link.Url),
					["text"] = link.Url,
				}
			) :
		string.Empty;

	protected override string Render(Date date) {
		if (date == null)
			return string.Empty;

		var start = Render(date.Start);
		var end = Render(date.End);
		return date.End == null ? start : $"{start} - {end}";
	}

	protected override string Render(DateTime? date)
		=> date != null ? date.ToString("yyyy-MM-dd HH:mm") : "Empty";

	protected override string Render(bool? val)
		=> !val.HasValue ? string.Empty : val.Value ? "[X]" : "[ ]";

	protected override string Render(double? val)
		=> !val.HasValue ? string.Empty : $"{val.Value:G}";

	protected override string Render(string val) 
		=>  !string.IsNullOrEmpty(val) ? Encode(val, true, false, true) : string.Empty; 

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

	protected virtual string RenderEmailLink(string linkTitle, string email) {
		linkTitle ??= string.Empty;
		email ??= string.Empty;
		var mailto = $"mailto:{email}";
		return RenderTemplate(
			"user",
			new RenderTokens {
				["name"] = linkTitle,
				["mailto"] = mailto,
				["base64_mailto_string_exp"] = mailto.ToBase64().ToCharArray().Select(x => $"'{x}'").ToDelimittedString(" + ")
			}
		);
	}

	protected override string Render(Person user)
		=> RenderUnsupported(user);

	protected override string Render(Bot user)
		=> this.Render(user.Owner);

	#endregion

	#region Database

	protected override string Render(Database database, bool inline) {
		var graph = Repository.GetEditableResourceGraph(database.Id);
		var rows = graph.Children.Select(x => Repository.GetObject(x.ObjectID)).Cast<Page>();

		return inline switch {
			false => RenderTemplate(
					"page",
					new RenderTokens(database) {
						["id"] = RenderingContext.Resource.ID,
						["style"] = "wide",
						["title"] = database.Title.ToPlainText(),   // html title
						["description"] = database.Description.ToPlainText(),
						["keywords"] = RenderingContext.Resource.Keywords.ToDelimittedString(", "),
						["author"] = "Local Notion",
						["page_content"] = RenderTemplate(
							"page_content", 
							new RenderTokens(database) {
								["id"] = RenderingContext.Resource.ID,
								["title"] = database.Title.ToPlainText(),   // html title
								["page_name"] = RenderingContext.Resource.Name,
								["page_title"] = RenderTemplate("page_title", new() { ["text"] = RenderingContext.Resource.Title }),   // title on the page 
								["page_subtitle"] = RenderTemplate("page_subtitle", new() { ["subtitle"]= Render(database.Description) }),
								["page_cover"] = RenderingContext.Resource.Cover switch {
									null => string.Empty,
									_ => RenderTemplate(
										"page_cover",
										new RenderTokens {
											["cover_url"] = SanitizeUrl(RenderingContext.Resource.Cover) ?? string.Empty,
										}
									)
								},
								["thumbnail"] = RenderingContext.Resource.Thumbnail.Type switch {
									ThumbnailType.None => string.Empty,
									ThumbnailType.Emoji => RenderTemplate(
										RenderingContext.Resource.Cover != null ? "thumbnail_emoji_on_cover" : "thumbnail_emoji",
										new RenderTokens {
											["thumbnail_emoji"] = RenderingContext.Resource.Thumbnail.Data,
										}
									),
									ThumbnailType.Image => RenderTemplate(
										RenderingContext.Resource.Cover != null ? "thumbnail_image_on_cover" : "thumbnail_image",
										new RenderTokens {
											["thumbnail_url"] = SanitizeUrl(RenderingContext.Resource.Thumbnail.Data)
										}
									),
								},
								["created_time"] = RenderingContext.Resource.CreatedOn,
								["last_updated_time"] = RenderingContext.Resource.LastEditedOn,
								["children"] = Render(database, true)
							}
						)
					}
				),

			true => RenderTemplate(
				"database",
				new RenderTokens(database) {
					["header"] = Merge(database.Properties.Select(x => RenderTemplate("database_header_cell", new RenderTokens(x) { ["contents"] =  Render(x.Key, x.Value) }))),
					["contents"] = Merge(
						rows.Select(page =>
							RenderTemplate(
								"database_row",
								new RenderTokens(database) {
									["contents"] = Merge(
										database.Properties.Select(x => RenderTemplate("database_row_cell", new RenderTokens(x) { ["contents"] = Render(page, page.Properties[x.Key]) }))
									)
								}
							)
						)
					)
				}
			),
		};
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
		=> RenderTemplate(
			"property", 
			new RenderTokens(property) { ["text"] = Render(text) }
		);

	#endregion

	#region Property Values

	protected override string Render(CheckboxPropertyValue propertyValue)
		=> RenderTemplate(
			"property_value",
			new RenderTokens(propertyValue) {
				["contents"] = RenderTemplate (
					"to_do",
					new RenderTokens(propertyValue) {
						["text"] = string.Empty,
						["checked"] = propertyValue.Checkbox ? "checked" : string.Empty,
						["color"] = "default",
						["children"] = string.Empty,
					}
				)
			} 
		);

	protected override string Render(CreatedByPropertyValue propertyValue) 
		=> RenderTemplate(
			"property_value",
			new RenderTokens(propertyValue) {
				["contents"] = Render(propertyValue.CreatedBy)
			} 
		);

	protected override string Render(CreatedTimePropertyValue propertyValue)
		=> RenderTemplate(
			"property_value",
			new RenderTokens(propertyValue) {
				["contents"] = Render(propertyValue.CreatedTime.ChompEnd(":00"))
			} 
		);

	protected override string Render(DatePropertyValue propertyValue)
		=> RenderTemplate(
			"property_value",
			new RenderTokens(propertyValue) {
				["contents"] = Render(propertyValue.Date)
			} 
		);

	protected override string Render(EmailPropertyValue propertyValue)
		=> RenderTemplate(
			"property_value",
			new RenderTokens(propertyValue) {
				["contents"] = RenderEmailLink(propertyValue.Email, propertyValue.Email)
			} 
		);

	protected override string Render(FilesPropertyValue propertyValue)
		=> RenderTemplate(
			"property_value",
			new RenderTokens(propertyValue) {
				["contents"] = Merge (
					propertyValue.Files.Select( 
						x => {
							var url = GetFileUrl(x, out var filename);
							return RenderTemplate(
								"file",
								new RenderTokens() {
									["filename"] = filename,
									["caption"] = string.Empty,
									["url"] = SanitizeUrl(url),
									["size"] = string.Empty,
								}
							);
						}
					)
				)
			} 
		);

	protected override string Render(FormulaPropertyValue propertyValue)
		=> RenderTemplate(
			"property_value",
			new RenderTokens(propertyValue) {
				["contents"] = propertyValue.Formula.Type switch {
					"string" => Render(propertyValue.Formula.String),
					"number" => Render(propertyValue.Formula.Number),
					"date" => Render(propertyValue.Formula.Date),
					"array" => Render(propertyValue.Formula.String),
					_ => throw new NotSupportedException(propertyValue.Formula.Type.ToString())
				}
			} 
		);

	protected override string Render(LastEditedByPropertyValue propertyValue)
		=> RenderTemplate(
			"property_value",
			new RenderTokens(propertyValue) {
				["contents"] = Render(propertyValue.LastEditedBy)
			} 
		);

	protected override string Render(LastEditedTimePropertyValue propertyValue)
		=> RenderTemplate(
			"property_value",
			new RenderTokens(propertyValue) {
				["contents"] = Render(propertyValue.LastEditedTime.ChompEnd(":00"))
			} 
		);

	protected override string Render(MultiSelectPropertyValue propertyValue) 
		=> RenderTemplate(
			"property_value",
			new RenderTokens(propertyValue) {
				["contents"] = Merge( 
					propertyValue.MultiSelect.Select(
						x => RenderTemplate(
							 "text_badge",
				 			 new RenderTokens(propertyValue) {
								["text"] = Render(x.Name),
								["color"] = x.Color
							} 
						)
					)
				)
			} 
		);

	protected override string Render(NumberPropertyValue propertyValue)
		=> RenderTemplate(
			"property_value",
			new RenderTokens(propertyValue) {
				["contents"] = Render(propertyValue.Number)
			} 
		);

	protected override string Render(PeoplePropertyValue propertyValue)
		=> RenderTemplate(
			"property_value",
			new RenderTokens(propertyValue) {
				["contents"] = Merge( propertyValue.People.Select(Render) )
			} 
		);

	protected override string Render(PhoneNumberPropertyValue propertyValue)
		=> RenderTemplate(
			"property_value",
			new RenderTokens(propertyValue) {
				["contents"] = Render(propertyValue.PhoneNumber)
			} 
		);

	protected override string Render(RelationPropertyValue propertyValue)
		=> RenderTemplate(
			"property_value",
			new RenderTokens(propertyValue) {
				["contents"] = Render(propertyValue.ToPlainText())
			} 
		);

	protected override string Render(RichTextPropertyValue propertyValue)
		=> RenderTemplate(
			"property_value",
			new RenderTokens(propertyValue) {
				["contents"] = Render(propertyValue.RichText)
			} 
		);

	protected override string Render(Page page, RollupPropertyValue propertyValue) 
		=> RenderTemplate(
			"property_value",
			new RenderTokens(propertyValue) {
				["contents"] = propertyValue.Rollup.Type switch {
					"number" => Render(propertyValue.Rollup.Number),
					"date" => Render(propertyValue.Rollup.Date),
					"array" => Merge(propertyValue.Rollup.Array.Select(x => Render(page, x))),
					_ => throw new NotSupportedException(propertyValue.Rollup.Type.ToString())
				}
			} 
		);

	protected override string Render(SelectPropertyValue propertyValue)
		=>
		propertyValue.Select != null ?
		RenderTemplate(
			"property_value",
			new RenderTokens(propertyValue) {
				["contents"] = RenderTemplate(
					"text_badge",
					new RenderTokens(propertyValue) {
						["text"] = Render(propertyValue.Select.Name),
						["color"] = propertyValue.Select.Color
					} 
				)
			} 
		) :
		string.Empty;

	protected override string Render(StatusPropertyValue propertyValue)
		=> RenderTemplate(
			"property_value",
			new RenderTokens(propertyValue) {
				["contents"] = RenderTemplate(
					"text_badge",
					new RenderTokens(propertyValue) {
						["text"] = Render(propertyValue.Status.Name),
						["color"] = propertyValue.Status.Color
					} 
				)
			} 
		);

	protected override string Render(TitlePropertyValue propertyValue, string pageID)
		=> RenderTemplate(
			"property_value",
			new RenderTokens(propertyValue) {
				["contents"] = RenderReference(pageID, false, true)
			} 
		);

	protected override string Render(UrlPropertyValue propertyValue)
		=> RenderTemplate(
			"property_value",
			new RenderTokens(propertyValue) {
				["contents"] = RenderTemplate(
					"text_link",
					new RenderTokens {
						["url"] = !string.IsNullOrWhiteSpace(propertyValue.Url) ? SanitizeUrl(propertyValue.Url) : string.Empty,
						["text"] = !string.IsNullOrWhiteSpace(propertyValue.Url) ? Render(propertyValue.Url) : string.Empty
					}
				)
			} 
		);

	#endregion

	#endregion

	#region Text

	protected override string Render(IEnumerable<RichTextBase> text)
		=> text.Select(Render).ToDelimittedString(string.Empty);

	protected override string Render(RichTextEquation text)
		=> RenderTemplate(
				"equation_inline",
				new RenderTokens {
					["expression"] = Encode(text.Equation.Expression, true, true, false),
				}
			);

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
		=> RenderTemplate(
			"text_badge",
			new RenderTokens {
				["color"] = color.GetAttribute<EnumMemberAttribute>().Value.Replace("_", "-"),
				["text"] = Render(text)
			}
		);
	
	#endregion

	#region Page

	protected override string Render(Page page)
		=> RenderPageInternal(
			RenderingContext.Resource.Title,
			RenderingContext.Resource.Keywords,
			RenderingContext.Resource.CMSProperties?.Summary ?? string.Empty,
			RenderPageContent(page),
			page.CreatedTime,
			page.LastEditedTime,
			page.Object.ToString().ToLowerInvariant().Replace("_", "-"),
			page.Id
		);

	protected string RenderPageInternal(string title, string[] keyWords, string description, string contents, DateTime createdTime, DateTime updatedTime, string objectType, string objectID) {
		var framingTokens = new RenderTokens() {
				["object-id"] = objectID,
				["object-type"] = objectType,
				["created_time"] = createdTime,
				["last_updated_time"] = updatedTime,
				["type"] = "page",
				["id"] = objectID,
				["style"] = "wide",
				["title"] = title  ?? string.Empty,   
				["description"] = description ?? string.Empty,
				["keywords"] = (keyWords ?? []).ToDelimittedString(", "),
				["author"] = "Local Notion"
		};
		return RenderTemplate(
			"page",
			new RenderTokens(framingTokens) {
				["page_content"] = contents,
			}
		);
	}


	protected virtual string RenderPageContent(Page page)
		=> RenderTemplate(
			"page_content",
			new RenderTokens(page) {
				["id"] = RenderingContext.Resource.ID,
				["title"] = RenderingContext.Resource.Title,   
				["page_name"] = RenderingContext.Resource.Name,
				["page_title"] =  RenderTemplate("page_title", new() { ["text"] = RenderingContext.Resource.Title }),   // title on the page 
				["page_subtitle"] = string.Empty,
				["page_cover"] = RenderingContext.Resource.Cover switch {
					null => string.Empty,
					_ => RenderTemplate(
						"page_cover",
						new RenderTokens {
							["cover_url"] = SanitizeUrl(RenderingContext.Resource.Cover) ?? string.Empty,
						}
					)
				},
				["thumbnail"] = RenderingContext.Resource.Thumbnail.Type switch {
					ThumbnailType.None => string.Empty,
					ThumbnailType.Emoji => RenderTemplate(
						page.Cover != null ? "thumbnail_emoji_on_cover" : "thumbnail_emoji",
						new RenderTokens {
							["thumbnail_emoji"] = RenderingContext.Resource.Thumbnail.Data,
						}
					),
					ThumbnailType.Image => RenderTemplate(
						page.Cover != null ? "thumbnail_image_on_cover" : "thumbnail_image",
						new RenderTokens {
							["thumbnail_url"] = SanitizeUrl(RenderingContext.Resource.Thumbnail.Data)
						}
					),
				},
				["children"] = RenderChildPageItems(),
				["created_time"] = RenderingContext.Resource.CreatedOn,
				["last_updated_time"] = RenderingContext.Resource.LastEditedOn,

			}
		);

	protected override string Render(BreadcrumbBlock block)
		=> Render(block, BreadCrumbGenerator.CalculateBreadcrumb(RenderingContext.Resource));

	protected override string Render(TableBlock block)
		=> RenderTemplate(
			"table",
			new RenderTokens(block) {
				["column_count"] = block.Table.TableWidth,
				["table_rows"] = block.HasChildren ? RenderChildPageItems() : string.Empty,
			}
		);

	protected override string Render(TableRowBlock block) {
		var tableNode = RenderingContext.GetParentRenderingNode(2);
		var tableObj = (TableBlock)RenderingContext.GetParentRenderingObject(2);
		var rowIX = tableNode.Children.EnumeratedIndexOf(RenderingContext.CurrentRenderingNode);
		var hasRowHeader = tableObj.Table.HasRowHeader;
		var hasColHeader = tableObj.Table.HasColumnHeader;
		return RenderTemplate(
		  hasRowHeader && rowIX == 0 ? "table_header_row" : "table_row",
		  new RenderTokens(block) {
			  ["row_index"] = rowIX.ToString(),
			  ["table_row_cells"] = Merge(
				  block.TableRow.Cells.Select(
					  (cell, colIX) => RenderTemplate(
						  hasRowHeader && rowIX == 0 || hasColHeader && colIX == 0 ? "table_header_cell" : "table_cell",
						  new RenderTokens(cell) {
							  ["row_id"] = block.Id,
							  ["row_index"] = rowIX.ToString(),
							  ["col_index"] = colIX.ToString(),
							  ["contents"] = Render(cell)
						  }
					  )
				  )
			  )
		  }
	  );
	}

	protected override string Render(AudioBlock block)
		=> RenderTemplate(
			"audio",
			new RenderTokens(block) {
				["caption"] = Render(block.Audio.Caption),
				["url"] = Render(block.Audio)
			}
		);

	protected override string Render(BookmarkBlock block)
		=> RenderUnsupported(block);

	protected override string Render(BreadcrumbBlock block, BreadCrumb breadcrumb) {
		return RenderTemplate(
		   "breadcrumb",
		   new RenderTokens(block) {
			   ["breadcrumb_items"] = Merge(
				  breadcrumb.Trail.Select(
					  item => RenderTemplate(
						  !item.Traits.HasFlag(BreadCrumbItemTraits.HasUrl) || item.Traits.HasFlag(BreadCrumbItemTraits.IsCurrentPage) ? "breadcrumb_item_disabled" : "breadcrumb_item",
						  new RenderTokens(item) {
							  ["type"] = item.Type.GetAttribute<EnumMemberAttribute>().Value,
							  ["data"] = item.Data,
							  ["text"] = item.Text,
							  ["url"] = SanitizeUrl(item.Url),
							  ["icon"] = item.Traits.HasFlag(BreadCrumbItemTraits.HasEmojiIcon) ?
								   RenderTemplate(
									   "icon_emoji",
										new RenderTokens(block) {
											["emoji"] = item.Data
										}
								   ) :
								   item.Traits.HasFlag(BreadCrumbItemTraits.HasImageIcon) ?
									   RenderTemplate(
										   "icon_image",
											new RenderTokens(block) {
												["url"] = item.Data
											}
									   ) :
									   string.Empty
						  }
					   )
				   )
			   )
		   }
	   );
	}

	protected override string RenderBulletedList(IEnumerable<NotionObjectGraph> bullets)
		=> RenderTemplate(
				"bulleted_list",
				new RenderTokens {
					["contents"] = Merge(bullets.Select((bullet, index) => Render(bullet, index + 1))), // never call RenderBulletedItem directly to avoid infinite loop due to rendering stack

				}
			);

	protected override string RenderBulletedItem(int number, BulletedListItemBlock block) {
		try {

			return RenderTemplate(
				"bulleted_list_item",
				new RenderTokens(block) {
					["number"] = number,
					["contents"] = Render(block.BulletedListItem.RichText),
					["color"] = ToColorString(block.BulletedListItem.Color.Value),
					["children"] = block.HasChildren ? RenderChildPageItems() : string.Empty,
				}
			);
		} finally {
		}
	}

	protected override string Render(CalloutBlock block)
		=> RenderTemplate(
				"callout",
				new RenderTokens(block) {
					["icon"] = block.Callout.Icon switch {
						EmojiObject emojiObject => RenderTemplate(
													"icon_emoji",
													 new RenderTokens(block) {
														 ["emoji"] = Render(emojiObject)
													 }
												),
						FileObject fileObject => RenderTemplate(
													"icon_image",
													 new RenderTokens(block) {
														 ["url"] = Render(fileObject)
													 }
												),
						null => string.Empty,
						_ => throw new ArgumentOutOfRangeException()
					},
					["text"] = Render(block.Callout.RichText),
					["color"] = ToColorString(block.Callout.Color.Value),
					["children"] = block.HasChildren ? RenderChildPageItems() : string.Empty,
				}
			);

	protected override string Render(ChildDatabaseBlock block)
		=> RenderUnsupported(block);

	protected override string Render(ChildPageBlock block) => RenderReference(block.Id, false, true);

	protected override string Render(CodeBlock block) {
		var rawCode = block.Code.RichText.Select(x => x.PlainText).ToDelimittedString(string.Empty);
		return RenderTemplate(
			"code",
			new RenderTokens(block) {
				["language"] = ToPrismLanguage(block.Code.Language),
				["code"] = Encode(rawCode, true, true, false),
				["raw_code"] = rawCode
			}
		);

		string ToPrismLanguage(string language)
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
	}

	protected override string Render(ColumnBlock block)
		=> block.HasChildren ? RenderChildPageItems() : string.Empty;

	protected override string Render(ColumnListBlock block)
		=> RenderingContext.CurrentRenderingNode.Children.Length switch {
			0 => RenderTemplate(
					"column_list_1",
					new RenderTokens(block) {
						["column_1"] = string.Empty,
					}
				),
			var x and > 0 and <= 12 => RenderTemplate(
					$"column_list_{x}",
					new RenderTokens(
						RenderTokens
							.ExtractTokens(block)
							.Concat(
								Enumerable.Range(0, x).Select(
										i => new KeyValuePair<string, object>($"column_{i + 1}", (object)Render(RenderingContext.CurrentRenderingNode.Children[i]))
								)
						)
					)
				),
			var x => throw new InvalidOperationException($"Unable to render pages with {x} or more columns")
		};

	protected override string Render(DividerBlock block)
		=> RenderTemplate("divider");

	protected override string Render(EquationBlock block)
		=> RenderTemplate(
				"equation_block",
				new RenderTokens(block) {
					["expression"] = Encode(block.Equation.Expression, true, true, false),
				}
			);

	protected override string Render(FileBlock block) {
		var url = GetFileUrl(block.File, out var filename);
		return RenderTemplate(
			"file",
			new RenderTokens(block) {
				["filename"] = filename,
				["caption"] = Render(block.File.Caption),
				["url"] = SanitizeUrl(url),
				["size"] = string.Empty,
			}
		);
	}

	protected override string Render(HeadingOneBlock block)
		=> block.Heading_1.IsToggleable switch {

			true => RenderTemplate(
				"toggle_closed",
				new RenderTokens(block) {
					["toggle_id"] = $"toggle_{++_toggleCount}",
					["title"] = RenderTemplate(
						"heading_1",
						new RenderTokens(block) {
							["text"] = Render(block.Heading_1.RichText),
							["color"] = ToColorString(block.Heading_1.Color.Value)
						}
					),
					["color"] = ToColorString(block.Heading_1.Color.Value),
					["children"] = block.HasChildren ? RenderChildPageItems() : string.Empty,
				}
			),

			false => RenderTemplate(
					"heading_1",
					new RenderTokens(block) {
						["text"] = Render(block.Heading_1.RichText),
						["color"] = ToColorString(block.Heading_1.Color.Value)
					}
				)
		};

	protected override string Render(HeadingTwoBlock block)
		=> block.Heading_2.IsToggleable switch {
			true => RenderTemplate(
				"toggle_closed",
				new RenderTokens(block) {
					["toggle_id"] = $"toggle_{++_toggleCount}",
					["title"] = RenderTemplate(
						"heading_2",
						new RenderTokens(block) {
							["text"] = Render(block.Heading_2.RichText),
							["color"] = ToColorString(block.Heading_2.Color.Value)
						}
					),
					["color"] = ToColorString(block.Heading_2.Color.Value),
					["children"] = block.HasChildren ? RenderChildPageItems() : string.Empty,
				}
			),
			false => RenderTemplate(
				"heading_2",
				new RenderTokens(block) {
					["text"] = Render(block.Heading_2.RichText),
					["color"] = ToColorString(block.Heading_2.Color.Value)
				}
			)
		};

	protected override string Render(HeadingThreeBlock block)
		=> block.Heading_3.IsToggleable switch {
			true => RenderTemplate(
				"toggle_closed",
				new RenderTokens(block) {
					["toggle_id"] = $"toggle_{++_toggleCount}",
					["title"] = RenderTemplate(
						"heading_3",
						new RenderTokens(block) {
							["text"] = Render(block.Heading_3.RichText),
							["color"] = ToColorString(block.Heading_3.Color.Value)
						}
					),
					["color"] = ToColorString(block.Heading_3.Color.Value),
					["children"] = block.HasChildren || block.Heading_3.IsToggleable ? RenderChildPageItems() : string.Empty,
				}
			),
			false => RenderTemplate(
				"heading_3",
				new RenderTokens(block) {
					["text"] = Render(block.Heading_3.RichText),
					["color"] = ToColorString(block.Heading_3.Color.Value)
				}
			)
		};

	protected override string Render(ImageBlock block)
		=> RenderTemplate(
				"image",
				new RenderTokens(block) {
					["url"] = Render(block.Image),
					["caption"] = Render(block.Image.Caption)
				}
			);

	protected override string Render(LinkToPageBlock block) => RenderReference(block.LinkToPage.GetId(), false);

	protected override string RenderNumberedItem(int number, NumberedListItemBlock block)
		=> RenderTemplate(
			"numbered_list_item",
			new RenderTokens(block) {
				["number"] = number,
				["contents"] = Render(block.NumberedListItem.RichText),
				["color"] = ToColorString(block.NumberedListItem.Color.Value),
				["children"] = block.HasChildren ? RenderChildPageItems() : string.Empty,
			}
		);

	protected override string RenderNumberedList(IEnumerable<NotionObjectGraph> numberedListItems)
		=> RenderTemplate(
				"numbered_list",
				new RenderTokens {
					["contents"] = Merge(numberedListItems.Select((item, index) => Render(item, index + 1)))   // never call RenderNumberedItem directly to avoid infinite loop due to rendering stack
				}
			);

	protected override string Render(PDFBlock block)
		=> RenderTemplate(
			"pdf",
			new RenderTokens(block) {
				["caption"] = Render(block.PDF.Caption),
				["url"] = Render(block.PDF)
			}
		);

	protected override string Render(ParagraphBlock block) {
		//var paragraphItems = block.Paragraph?.RichText?.ToArray();

		// A paragraph containing a single mention, we render it as a Notion-style mention.
		// Otherwise a mention within a paragraph is rendered as a link.
		//if (paragraphItems.Trim().ToArray() is [RichTextMention mention]) 
		//	return RenderReference(mention.Mention.GetObjectID(), false);

		return RenderTemplate(
				"paragraph",
				new RenderTokens(block) {
					["contents"] = Render(block.Paragraph.RichText),
					["color"] = ToColorString(block.Paragraph.Color.Value),
					["children"] = block.HasChildren ? RenderChildPageItems() : string.Empty,
				}
			);
	}

	protected override string Render(QuoteBlock block)
		=> RenderTemplate(
			"quote",
			new RenderTokens(block) {
				["text"] = Render(block.Quote.RichText),
				["color"] = ToColorString(block.Quote.Color.Value),
				["children"] = block.HasChildren ? RenderChildPageItems() : string.Empty,
			}
		);

	protected override string Render(SyncedBlockBlock block)
		=> RenderUnsupported(block);

	protected override string Render(TableOfContentsBlock block)
		=> RenderTemplate(
			"table_of_contents",
			new RenderTokens(block) {
				["color"] = ToColorString(block.TableOfContents.Color.Value)
			}
		);

	protected override string Render(TemplateBlock block)
		=> RenderUnsupported(block);

	protected override string Render(ToDoBlock block)
		=> RenderTemplate(
			"to_do",
			new RenderTokens(block) {
				["text"] = Render(block.ToDo.RichText),
				["checked"] = block.ToDo.IsChecked ? "checked" : string.Empty,
				["color"] = ToColorString(block.ToDo.Color.Value),
				["children"] = block.HasChildren ? RenderChildPageItems() : string.Empty,
			}
		);

	protected override string Render(ToggleBlock block)
		=> RenderTemplate(
			"toggle_closed",
			new RenderTokens(block) {
				["toggle_id"] = $"toggle_{++_toggleCount}",
				["title"] = Render(block.Toggle.RichText),
				["color"] = ToColorString(block.Toggle.Color.Value),
				["children"] = block.HasChildren ? RenderChildPageItems() : string.Empty,
			}
		);

	protected override string RenderText(string content, bool isUrl, bool isBold, bool isItalic, bool isStrikeThrough, bool isUnderline, bool isCode, Color color, (string Url, string Icon, string Indicator) urlInfo = default) {
		if (isUrl) {
			Guard.ArgumentNotNull(urlInfo, nameof(urlInfo));

			return RenderTemplate(
				"text_link",
				new RenderTokens {
					["url"] = SanitizeUrl(urlInfo.Url ?? string.Empty),
					["text"] = RenderText(content, false, isBold, isItalic, isStrikeThrough, isUnderline, isCode, color),
					["icon"] = urlInfo.Icon,
					["indicator"] = urlInfo.Indicator
				}
			);

		}

		if (isBold) {
			return RenderTemplate(
				"text_bold",
				new RenderTokens {
					["text"] = RenderText(content, false, false, isItalic, isStrikeThrough, isUnderline, isCode, color)
				}
			);
		}

		if (isItalic) {
			return RenderTemplate(
				"text_italic",
				new RenderTokens {
					["text"] = RenderText(content, false, false, false, isStrikeThrough, isUnderline, isCode, color)
				}
			);
		}

		if (isStrikeThrough) {
			return RenderTemplate(
				"text_strikethrough",
				new RenderTokens {
					["text"] = RenderText(content, false, false, false, false, isUnderline, isCode, color)
				}
			);
		}


		if (isUnderline) {
			return RenderTemplate(
				"text_underline",
				new RenderTokens {
					["text"] = RenderText(content, false, false, false, false, false, isCode, color)
				}
			);
		}

		if (isCode) {
			return RenderTemplate(
				"text_code",
				new RenderTokens {
					["text"] = Render(content),
				}
			);
		}

		if (color != Color.Default) {
			return RenderTemplate(
				"text_colored",
				new RenderTokens {
					["color"] = color.GetAttribute<EnumMemberAttribute>().Value.Replace("_", "-"),
					["text"] = RenderText(content, false, false, false, false, false, false, Color.Default)
				}
			);
		}

		return RenderTemplate(
			"text",
			new RenderTokens {
				["text"] = Render(content), 
			}
		);
	}

	protected override string RenderReference(string objectID, bool isInline, bool omitIndicator = false) {

		if (!Resolver.TryGenerate(RenderingContext.Resource, objectID, RenderType.HTML, out var childResourceUrl, out var resource))
			return $"Unresolved child resource {objectID}";

		var icon = resource switch {
			LocalNotionEditableResource { Thumbnail: not null, Thumbnail.Type: ThumbnailType.Emoji } localNotionPage => RenderTemplate(
				"icon_emoji",
				new RenderTokens(resource) {
					["emoji"] = localNotionPage.Thumbnail.Data
				}
			),
			LocalNotionEditableResource { Thumbnail: not null, Thumbnail.Type: ThumbnailType.Image } localNotionPage => RenderTemplate(
				"icon_image",
				new RenderTokens(resource) {
					["url"] = SanitizeUrl(localNotionPage.Thumbnail.Data)
				}
			),
			_ => string.Empty
		};

		omitIndicator |= string.IsNullOrWhiteSpace(icon);

		var indicator = !omitIndicator ? RenderTemplate("indicator_link") : string.Empty;



		if (isInline) {
			// TODO: add inline page_link template that matches Notion
			//return RenderText(resource.Title, true, false, false, false, false, false, Color.Default, (Url: SanitizeUrl(childResourceUrl), Icon: icon, Indicator: indicator));
			return RenderText(resource.Title, true, false, false, false, false, false, Color.Default, (Url: SanitizeUrl(childResourceUrl), Icon: icon, Indicator: indicator));
		}


		return RenderTemplate(
			"paragraph",
			new RenderTokens(objectID) {
				["contents"] = RenderText(resource.Title, true, false, false, false, false, false, Color.Default, (Url: SanitizeUrl(childResourceUrl), Icon: icon, Indicator: indicator)),
				["color"] = Color.Default,
				["children"] = string.Empty,
			}
		);
	}

	protected override string Render(IPageIcon pageIcon)
		=> pageIcon switch {
			EmojiObject emojiObject => Render(emojiObject),
			ExternalFile externalFile => (string)(object)GetFileUrl(externalFile, out _),   // WARNING: ugly cast hack here
			UploadedFile uploadedFile => (string)(object)GetFileUrl(uploadedFile, out _),   // WARNING: ugly cast hack here
			_ => throw new NotSupportedException()
		};

	protected override string Render(VideoBlock block) {
		switch (block.Video) {
			case ExternalFile externalFile: {
				if (Tools.Url.IsVideoSharingUrl(externalFile.External.Url, out var platform, out var videoID)) 
					return RenderSocialMediaVideo(block, platform, videoID, Render(block.Video.Caption));

				return  RenderTemplate(
					"embed_video",
					new RenderTokens(block) {
						["caption"] = Render(block.Video.Caption),
						["url"] = Render(externalFile.External.Url)
					}
				);
			}
			case UploadedFile uploadedFile: {
				var url = Resolver.GenerateUploadedFileLink(RenderingContext.Resource, uploadedFile, out _);
				return RenderTemplate(
					"embed_video",
					new RenderTokens(block) {
						["caption"] = Render(block.Video.Caption),
						["url"] = Render(url)
					}
				);
			}
			default:
				throw new ArgumentOutOfRangeException(nameof(block), block, null);
		}
	}
	
	protected virtual string RenderSocialMediaVideo(Block block, VideoSharingPlatform platform, string videoID, string caption) {
		return platform switch {
			VideoSharingPlatform.YouTube => RenderTemplate(
				"embed_youtube",
				new RenderTokens(block) {
					["caption"] = Render(caption),
					["video_id"] = videoID,
				}),
			VideoSharingPlatform.Rumble => RenderTemplate(
				"embed_rumble",
				new RenderTokens(block) {
					["caption"] = Render(caption),
					["video_id"] = videoID,
				}),
			VideoSharingPlatform.BitChute => RenderTemplate(
				"embed_bitchute",
				new RenderTokens(block) {
					["caption"] = Render(caption),
					["video_id"] = videoID,
				}),
			VideoSharingPlatform.Vimeo => RenderTemplate(
				"embed_vimeo",
				new RenderTokens(block) {
					["caption"] = Render(caption),
					["video_id"] = videoID,
				}),
			_ => throw new NotSupportedException(platform.ToString())
		};
	}

	protected override string RenderUnsupported(object @object)
		=> RenderTemplate(
			"unsupported",
			new RenderTokens(@object) {
				["json"] = Tools.Json.WriteToString(@object ?? "NULL"),
			}
		);

	#endregion

	#region Aux

	protected virtual string GetFileUrl(FileObject fileObject, out string filename) {
		string url;
		switch (fileObject) {
			case ExternalFile externalFile:
				url = externalFile.External.Url;
				break;
			case UploadedFile uploadedFile:
				url = Resolver.GenerateUploadedFileLink(RenderingContext.Resource, uploadedFile, out _);
				break;
			default:
				throw new NotSupportedException(fileObject.GetType().Name);
		}
		filename = Path.GetFileName(Tools.Url.TryParse(url, out _, out _, out _, out var path, out _) ? path : url) ?? Constants.DefaultResourceTitle;
		return url;
	}

	protected virtual string GetFileUrl(FileObjectWithName fileObject, out string filename) {
		string url;
		switch (fileObject) {
			case ExternalFileWithName externalFileWithName:
				url = externalFileWithName.External.Url;
				break;
			case UploadedFileWithName uploadedFileWithName:
				url = Resolver.GenerateUploadedFileLink(RenderingContext.Resource, uploadedFileWithName, out _);
				break;
			default:
				throw new NotSupportedException(fileObject.GetType().Name);
		}
		filename = fileObject.Name;
		return url;
	}

	protected virtual string RenderTemplate(string widgetType)
		=> RenderTemplate(widgetType, new RenderTokens());

	protected virtual string RenderTemplate(string widget, RenderTokens tokens) {
		_tokens = _tokens.AttachHead(tokens);
		try {
			return FetchTemplate(widget, ".html").FormatWithDictionary(_tokens, true);
		} finally {
			_tokens = _tokens.DetachHead();
		}
	}

	protected virtual string FetchTemplate(string widgetname, string fileExt) {
		var localNotionMode = Repository.Paths.Mode;
		var renderModePrefix = Mode switch { RenderMode.Editable => "editable", RenderMode.ReadOnly => "readonly", _ => throw new NotSupportedException(Mode.ToString()) };
		var sanitizedWidgetName = Tools.FileSystem.ToValidFolderOrFilename(widgetname);
		var sanitizedWidgetNameWithMode = $"{sanitizedWidgetName}.{localNotionMode switch { LocalNotionMode.Offline => "offline", LocalNotionMode.Online => "online", _ => throw new NotSupportedException(localNotionMode.ToString()) }}";
		fileExt = fileExt.TrimStart(".");
		var rootedWidgetTemplateName = $"include://{renderModePrefix}/{sanitizedWidgetName}.{fileExt}";
		var rootedWidgetWithModeTemplate = $"include://{renderModePrefix}/{sanitizedWidgetNameWithMode}.{fileExt}";
		var widgetTemplateName = $"include://{sanitizedWidgetName}.{fileExt}";
		var widgetWithModeTemplate = $"include://{sanitizedWidgetNameWithMode}.{fileExt}";

		if (!_tokens.TryGetValue(rootedWidgetWithModeTemplate, out var widgetValue))
			if (!_tokens.TryGetValue(rootedWidgetTemplateName, out widgetValue))
				if (!_tokens.TryGetValue(widgetTemplateName, out widgetValue))
					if (!_tokens.TryGetValue(widgetWithModeTemplate, out widgetValue))
						throw new InvalidOperationException($"Widget `{widgetname}` not found in theme(s).");

		return Regex.Replace(widgetValue.ToString(), "^<!--.*?-->", string.Empty, RegexOptions.Singleline);
	}

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

	protected virtual string ToString(ParentType blockType)
		=> $"[{blockType.GetAttribute<EnumMemberAttribute>().Value}]";

	protected virtual string ToString(BlockType blockType)
		=> $"{blockType.GetAttribute<EnumMemberAttribute>().Value}";

	protected virtual string ToString(RichTextType richTextType)
		=> $"{richTextType.GetAttribute<EnumMemberAttribute>().Value}";

	protected virtual string ToString(PropertyValueType propertyValueType)
		=> $"{propertyValueType.GetAttribute<EnumMemberAttribute>().Value}";

	protected string ToColorString(Color color)
		=> color.GetAttribute<EnumMemberAttribute>()?.Value.Replace("_", "-") ?? throw new InvalidOperationException($"Color '{color}' did not have {nameof(EnumMemberAttribute)} defined");

	protected string SanitizeUrl(string url) {
		// Resource link?
		if (LocalNotionRenderLink.TryParse(url, out var link))  
			return $"/{Resolver.Generate(RenderingContext.Resource, link.ResourceID, link.RenderType, out _)}";

		// Page link?
		if (url.StartsWith("/")) {
			var destObject = new string(url.Substring(1).TakeUntil(c => c == '#').ToArray());
			var anchor = new string (url.Substring(1).Skip(destObject.Length).ToArray()).TrimStart('#');
			if (Guid.TryParse(destObject, out var destGuid)) 
				destObject = LocalNotionHelper.ObjectGuidToId(destGuid);

			if (LocalNotionHelper.IsValidObjectID(destObject) && Resolver.TryGenerate(RenderingContext.Resource, destObject, RenderType.HTML, out var parentUrl, out _)) {
				url = parentUrl;
				if (!string.IsNullOrEmpty(anchor))
					url += $"#{anchor}";
			}
		}

		return url;
	}

	protected string Encode(string text, bool htmlEncode, bool escapeBraces, bool convertNewlinesToBreaks) {

		if (htmlEncode)
			text = System.Net.WebUtility.HtmlEncode(text);

		if (escapeBraces)
			text = text.Replace("{", "{{").Replace("}", "}}");

		if (convertNewlinesToBreaks) {
			text = text.Replace("\n\r", "<br />");
			text = text.Replace("\n", "<br />");
		}
		return text;
	}

	#endregion

	#region Inner Classes

	protected class RenderTokens : DictionaryDecorator<string, object> {

		public RenderTokens()
			: this(Enumerable.Empty<KeyValuePair<string, object>>()) {
		}

		public RenderTokens(object @object)
			: this(@object is not null ? ExtractTokens(@object) : Enumerable.Empty<KeyValuePair<string, object>>()) {
		}

		public RenderTokens(IEnumerable<KeyValuePair<string, object>> values) : base(new Dictionary<string, object>()) {
			Guard.ArgumentNotNull(values, nameof(values));
			this.AddRange(values, CollectionConflictPolicy.Throw);
		}

		private void HydrateObject(IObject notionObject) {
			this["object_id"] = notionObject.Id.Replace("-", string.Empty);
			this["object_type"] = notionObject.Object.ToString().ToLowerInvariant().Replace("_", "-");
			this["type"] = notionObject switch {
				IBlock block => block.Type.GetAttribute<EnumMemberAttribute>().Value?.Replace("_", "-"),
				_ => notionObject.GetType().Name
			} ?? string.Empty;
		}

		public static IEnumerable<KeyValuePair<string, object>> ExtractTokens(object @object) {
			if (@object == null)
				yield break;
			//if (@object is string str && Guid.TryParse(str, out var id)) {
			//	yield return new KeyValuePair<string, object>("object_id", str.Replace("-", string.Empty));
			//}
			if (@object is RenderTokens rt) {
				foreach (var item in rt)
					yield return item;
			} else if (@object is LocalNotionResource lnr) {
				yield return new KeyValuePair<string, object>("object_id", lnr.ID.Replace("-", string.Empty));

			} else if (@object is IObject notionObject) {
				yield return new KeyValuePair<string, object>("object_id", notionObject.Id.Replace("-", string.Empty));
				yield return new KeyValuePair<string, object>("object_type", notionObject.Object.ToString().ToLowerInvariant().Replace("_", "-"));
				yield return new KeyValuePair<string, object>(
					"type",
					notionObject switch {
						IBlock block => block.Type.GetAttribute<EnumMemberAttribute>().Value?.Replace("_", "-"),
						_ => notionObject.GetType().Name
					} ?? string.Empty);

			} else {
				yield return new KeyValuePair<string, object>("object_id", string.Empty);
				yield return new KeyValuePair<string, object>("object_type", "misc");
				yield return new KeyValuePair<string, object>("type", @object.GetType().Name.ToLowerInvariant().Replace("_", "-"));
			}
		}

	}


	#endregion
}