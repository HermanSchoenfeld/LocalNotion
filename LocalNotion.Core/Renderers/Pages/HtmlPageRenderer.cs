using Hydrogen;
using Notion.Client;
using System.Runtime.Serialization;
using System.Text.RegularExpressions;
using AngleSharp.Html.Parser;

namespace LocalNotion.Core;

public class HtmlPageRenderer : PageRendererBase<string> {
	private int _toggleCount = 0;
	private DictionaryChain<string, object> _tokens;
	public HtmlPageRenderer(RenderMode renderMode, LocalNotionMode mode, LocalNotionPage page, NotionObjectGraph pageGraph, IDictionary<string, IObject> pageObjects, IPathResolver pathResolver, ILinkGenerator resolver, IBreadCrumbGenerator breadCrumbGenerator, HtmlThemeInfo[] themes)
		: base(page, pageGraph, pageObjects, resolver, breadCrumbGenerator, File.WriteAllText) {
		Guard.ArgumentNotNull(themes, nameof(themes));
		Guard.ArgumentGT(themes.Length, 0, nameof(themes), "At least 1 theme must be provided to the renderer");
		Mode = mode;
		BreadCrumbGenerator = breadCrumbGenerator;
		RenderMode = renderMode;

		SuppressFormatting = themes.Any(t => t.SuppressFormatting);

		// Generate all the theme tokens
		foreach(var theme in themes)
			_tokens = _tokens == null ?
				GetModeCorrectedTokens(theme) 
				: _tokens.AttachHead( GetModeCorrectedTokens(theme) );

		// Attach rendering-specific tokens
		_tokens = _tokens.AttachHead(new Dictionary<string, object> { ["render_mode"] = renderMode.GetAttribute<EnumMemberAttribute>().Value });

		DictionaryChain<string, object> GetModeCorrectedTokens(HtmlThemeInfo theme) {
			// This method provides a "chain of responsibility" style dictionary of all the theme tokens
			// Also it resolves a theme:// tokens which are aliases for links to a theme resource.
			var dictionary = new DictionaryChain<string, object>(
				theme.Tokens.ToDictionary(x => x.Key, x => mode switch { LocalNotionMode.Offline => ToLocalPathIfApplicable(x.Key, x.Value.Local), LocalNotionMode.Online => x.Value.Remote, _ => throw new NotSupportedException(mode.ToString()) }),
				theme.BaseTheme != null ? GetModeCorrectedTokens(theme.BaseTheme) : null
			);

			object ToLocalPathIfApplicable(string key, object value) {
				if (key.StartsWith("theme://")) {
					Guard.Ensure(value != null, $"Unexpected null value for key '{key}'");
					var thisRendersExpectedParentFolder = pathResolver.GetResourceFolderPath(LocalNotionResourceType.Page, Page.ID, FileSystemPathType.Absolute);
					return Path.GetRelativePath(thisRendersExpectedParentFolder, value.ToString()).ToUnixPath();
				}
				return value;
			}
			return dictionary;
		}
	}

	protected LocalNotionMode Mode { get; }

	protected IBreadCrumbGenerator BreadCrumbGenerator { get; }

	protected RenderMode RenderMode { get; }

	protected bool SuppressFormatting { get; }

	protected override string Merge(IEnumerable<string> outputs)
		=> outputs.ToDelimittedString(string.Empty);

	#region Values

	protected override string Render(EmojiObject emojiObject)
		 => emojiObject.Emoji;

	protected override string Render(ExternalFile externalFile)
		=> SanitizeUrl(externalFile.External.Url);

	protected override string Render(UploadedFile uploadedFile)
		 => Resolver.GenerateUploadedFileLink(Page, uploadedFile, out _);

	protected override string Render(Link link)
		=> RenderTemplate(
				"text_link",
				new NotionObjectTokens {
					["url"] = SanitizeUrl(link.Url),
					["text"] = link.Url,
				}
			);

	protected override string Render(Date date) {
		var start = Render(date.Start);
		var end = Render(date.End);
		return date.End == null ? start : $"{start} - {end}";
	}

	protected override string Render(DateTime? date)
		=> date != null ? date.ToString("yyyy-MM-dd HH:mm:ss.fff") : "Empty";

	protected override string Render(bool? val)
		=> !val.HasValue ? string.Empty : val.Value ? "[X]" : "[ ]";

	protected override string Render(double? val)
		=> !val.HasValue ? string.Empty : $"{val.Value:G}";

	#endregion

	#region Owners

	protected override string Render(UserOwner owner)
		=> Render(owner.User);

	protected override string Render(WorkspaceIntegrationOwner owner)
		=> RenderUnsupported(owner);

	#endregion

	#region Users

	protected override string Render(User user) {
		var mailto = $"mailto:{user.Person?.Email ?? string.Empty}";
		return RenderTemplate(
			"user",
			new NotionObjectTokens(user) {
				["name"] = user.Name,
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

	protected override string Render(Database database)
		=> RenderUnsupported(database);

	#endregion

	#region Text

	protected override string Render(IEnumerable<RichTextBase> text)
		=> text.Select(Render).ToDelimittedString(string.Empty);

	protected override string Render(RichTextEquation text)
		=> RenderTemplate(
				"equation_inline",
				new NotionObjectTokens {
					["expression"] = System.Net.WebUtility.HtmlEncode(text.Equation.Expression),
				}
			);

	protected override string Render(RichTextMention text)
		=> text.Mention.Type switch {
			"user" => Render(text.Mention.User),
			"page" => RenderTemplate(
				"text_link",   // should be page_link (use svg's)
				new NotionObjectTokens {
					["url"] = Resolver.TryGenerate(Page, text.Mention.Page.Id, null, out var url, out _) ? url : $"Unresolved link to '{text.Mention.Page.Id}'",
					["text"] = Resolver.TryGenerate(Page, text.Mention.Page.Id, null, out _, out var resource) ? resource.Title : $"Unresolved name for page '{text.Mention.Page.Id}'",
				}
			),
			"database" => $"[{text.Mention.Type}]{text.Mention.Database.Id}",
			"date" => Render(text.Mention.Date), // maybe a link to calendar here?
			_ => throw new InvalidOperationException($"Unrecognized mention type '{text.Mention.Type}'")
		};

	protected override string Render(RichTextText text) {
		return RenderInternal(text.Text?.Link?.Url ?? text.Href, text.Annotations.IsBold, text.Annotations.IsItalic, text.Annotations.IsStrikeThrough, text.Annotations.IsUnderline, text.Annotations.IsCode, text.Annotations.Color.Value, text.Text?.Content ?? text.PlainText ?? string.Empty);

		string RenderInternal(string link, bool isBold, bool isItalic, bool isStrikeThrough, bool isUnderline, bool isCode, Color color, string content) {

			if (!string.IsNullOrWhiteSpace(link)) {
				return RenderTemplate(
					"text_link",
					new NotionObjectTokens {
						["url"] = SanitizeUrl(link),
						["text"] = RenderInternal(null, isBold, isItalic, isStrikeThrough, isUnderline, isCode, color, content)
					}
				);

			}

			if (isBold) {
				return RenderTemplate(
					"text_bold",
					new NotionObjectTokens {
						["text"] = RenderInternal(null, false, isItalic, isStrikeThrough, isUnderline, isCode, color, content)
					}
				);
			}

			if (isItalic) {
				return RenderTemplate(
					"text_italic",
					new NotionObjectTokens {
						["text"] = RenderInternal(null, false, false, isStrikeThrough, isUnderline, isCode, color, content)
					}
				);
			}

			if (isStrikeThrough) {
				return RenderTemplate(
					"text_strikethrough",
					new NotionObjectTokens {
						["text"] = RenderInternal(null, false, false, false, isUnderline, isCode, color, content)
					}
				);
			}


			if (isUnderline) {
				return RenderTemplate(
					"text_underline",
					new NotionObjectTokens {
						["text"] = RenderInternal(null, false, false, false, false, isCode, color, content)
					}
				);
			}

			if (isCode) {
				return RenderTemplate(
					"text_code",
					new NotionObjectTokens {
						["text"] = RenderInternal(null, false, false, false, false, false, color, content)
					}
				);
			}

			if (color != Color.Default) {
				return RenderTemplate(
					"text_colored",
					new NotionObjectTokens {
						["color"] = color.GetAttribute<EnumMemberAttribute>().Value.Replace("_", "-"),
						["text"] = RenderInternal(null, false, false, false, false, false, Color.Default, content)
					}
				);
			}

			return RenderTemplate(
				"text",
				new NotionObjectTokens {
					["text"] = System.Net.WebUtility.HtmlEncode(content),
				}
			);

		}

	}

	#endregion

	#region Property Values

	protected override string Render(CheckboxPropertyValue propertyValue)
		=> RenderUnsupported(propertyValue);

	protected override string Render(CreatedByPropertyValue propertyValue)
		=> RenderUnsupported(propertyValue);

	protected override string Render(CreatedTimePropertyValue propertyValue)
		=> RenderUnsupported(propertyValue);

	protected override string Render(DatePropertyValue propertyValue)
		=> RenderUnsupported(propertyValue);

	protected override string Render(EmailPropertyValue propertyValue)
		=> RenderUnsupported(propertyValue);

	protected override string Render(FilesPropertyValue propertyValue)
		=> RenderUnsupported(propertyValue);

	protected override string Render(FormulaPropertyValue propertyValue)
		=> propertyValue.Formula.Type switch {
			"string" => RenderUnsupported(propertyValue),
			"number" => RenderUnsupported(propertyValue),
			"date" => RenderUnsupported(propertyValue),
			"array" => RenderUnsupported(propertyValue),
			_ => throw new NotSupportedException(propertyValue.Formula.Type.ToString())
		};

	protected override string Render(LastEditedByPropertyValue propertyValue)
		=> RenderUnsupported(propertyValue);

	protected override string Render(LastEditedTimePropertyValue propertyValue)
		=> RenderUnsupported(propertyValue);

	protected override string Render(MultiSelectPropertyValue propertyValue)
		=> RenderUnsupported(propertyValue);

	protected override string Render(NumberPropertyValue propertyValue)
		=> RenderUnsupported(propertyValue);

	protected override string Render(PeoplePropertyValue propertyValue)
		=> RenderUnsupported(propertyValue);

	protected override string Render(PhoneNumberPropertyValue propertyValue)
		=> RenderUnsupported(propertyValue);

	protected override string Render(RelationPropertyValue propertyValue)
		=> RenderUnsupported(propertyValue);

	protected override string Render(RichTextPropertyValue propertyValue)
		=> RenderUnsupported(propertyValue);

	protected override string Render(RollupPropertyValue propertyValue) {
		return propertyValue.Rollup.Type switch {
			"number" => $"{ToString(propertyValue.Type)}:{propertyValue.Rollup.Type} [{Render(propertyValue.Rollup.Number)}]",
			"date" => $"{ToString(propertyValue.Type)}:{propertyValue.Rollup.Type} [{Render(propertyValue.Rollup.Date)}]",
			"array" => $"{ToString(propertyValue.Type)}:{propertyValue.Rollup.Type} [{propertyValue.Rollup.Array.Select(Render).ToDelimittedString(", ")}]",
			_ => throw new InvalidOperationException($"Unrecognized rollup type '{propertyValue.Rollup.Type}'")
		};
	}

	protected override string Render(SelectPropertyValue propertyValue)
		=> RenderUnsupported(propertyValue);

	protected override string Render(TitlePropertyValue propertyValue)
		=> RenderUnsupported(propertyValue);

	protected override string Render(UrlPropertyValue propertyValue)
		=> RenderUnsupported(propertyValue);

	#endregion

	#region Page

	protected override string Render(Page page)
		=> CleanUpHtml(
			RenderTemplate(
				"page",
				new NotionObjectTokens(page) {
					["title"] = this.Page.Title,   // html title
					["page_name"] = Tools.Url.ToHtmlDOMObjectID(page.Id, Constants.PageNameDomObjectPrefix),
					["page_title"] = RenderTemplate("page_title", new() { ["text"] = this.Page.Title }),   // title on the page 
					["style"] = "wide",
					["cover"] = this.Page.Cover switch {
						null => string.Empty,
						_ => RenderTemplate(
								"cover",
								new NotionObjectTokens {
									["cover_url"] = SanitizeUrl(this.Page.Cover) ?? string.Empty,
								}
							)
					},
					["thumbnail"] = this.Page.Thumbnail.Type switch {
						ThumbnailType.None => string.Empty,
						ThumbnailType.Emoji => RenderTemplate(
							this.Page.Cover != null ? "thumbnail_emoji_on_cover" : "thumbnail_emoji",
							new NotionObjectTokens {
								["thumbnail_emoji"] = this.Page.Thumbnail.Data,
							}
						),
						ThumbnailType.Image => RenderTemplate(
							this.Page.Cover != null ? "thumbnail_image_on_cover" : "thumbnail_image",
							new NotionObjectTokens {
								["thumbnail_url"] = SanitizeUrl(this.Page.Thumbnail.Data)
							}
						),
					},
					["id"] = page.Id,
					["created_time"] = page.CreatedTime,
					["last_updated_time"] = page.LastEditedTime,
					["children"] = RenderChildItems()
				}
			)
		);

	protected override string Render(TableBlock block)
		=> RenderTemplate(
			"table",
			new NotionObjectTokens(block) {
				["column_count"] = block.Table.TableWidth,
				["table_rows"] = RenderChildItems()
			}
		);

	protected override string Render(TableRowBlock block) {
		var tableNode = GetParentRenderingNode(2);
		var tableObj = (TableBlock)GetParentRenderingObject(2);
		var rowIX = tableNode.Children.EnumeratedIndexOf(CurrentRenderingNode);
		var hasRowHeader = tableObj.Table.HasRowHeader;
		var hasColHeader = tableObj.Table.HasColumnHeader;
		return RenderTemplate(
		  hasRowHeader && rowIX == 0 ? "table_header_row" : "table_row",
		  new NotionObjectTokens(block) {
			  ["row_index"] = rowIX.ToString(),
			  ["table_row_cells"] = Merge(
				  block.TableRow.Cells.Select(
					  (cell, colIX) => RenderTemplate(
						  hasRowHeader && rowIX == 0 || hasColHeader && colIX == 0 ? "table_header_cell" : "table_cell",
						  new NotionObjectTokens(cell) {
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
			new NotionObjectTokens(block) {
				["caption"] = Render(block.Audio.Caption),
				["url"] = Render(block.Audio)
			}
		);

	protected override string Render(BookmarkBlock block)
		=> RenderUnsupported(block);

	protected override string Render(BreadcrumbBlock block, BreadCrumb breadcrumb) {
		return RenderTemplate(
		   "breadcrumb",
		   new NotionObjectTokens(block) {
			   ["breadcrumb_items"] = Merge(
				  breadcrumb.Trail.Select(
					  item => RenderTemplate(
						  !item.Traits.HasFlag(BreadCrumbItemTraits.HasUrl) || item.Traits.HasFlag(BreadCrumbItemTraits.IsCurrentPage) ? "breadcrumb_item_disabled" : "breadcrumb_item",
						  new NotionObjectTokens(item) {
							  ["type"] = item.Type.GetAttribute<EnumMemberAttribute>().Value,
							  ["data"] = item.Data,
							  ["text"] = item.Text,
							  ["url"] = SanitizeUrl(item.Url),
							  ["icon"] = item.Traits.HasFlag(BreadCrumbItemTraits.HasEmojiIcon) ?
								   RenderTemplate(
									   "icon_emoji",
										new NotionObjectTokens(block) {
											["emoji"] = item.Data
										}
								   ) :
								   item.Traits.HasFlag(BreadCrumbItemTraits.HasImageIcon) ?
									   RenderTemplate(
										   "icon_image",
											new NotionObjectTokens(block) {
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

	protected override string Render(IEnumerable<BulletedListItemBlock> bullets)
		=> RenderTemplate(
				"bulleted_list",
				new NotionObjectTokens {
					["contents"] = Merge(bullets.Select((bullet, index) => Render(index + 1, bullet)))
				}
			);

	protected override string Render(int number, BulletedListItemBlock block)
		=> RenderTemplate(
			"bulleted_list_item",
			new NotionObjectTokens(block) {
				["number"] = number,
				["contents"] = Render(block.BulletedListItem.RichText),
				["color"] = ToColorString(block.BulletedListItem.Color.Value)
			}
		);

	protected override string Render(CalloutBlock block)
		=> RenderTemplate(
				"callout",
				new NotionObjectTokens(block) {
					["icon"] = block.Callout.Icon switch {
						EmojiObject emojiObject => RenderTemplate(
													"icon_emoji",
													 new NotionObjectTokens(block) {
														 ["emoji"] = Render(emojiObject)
													 }
												),
						FileObject fileObject => RenderTemplate(
													"icon_image",
													 new NotionObjectTokens(block) {
														 ["url"] = Render(fileObject)
													 }
												),
						_ => throw new ArgumentOutOfRangeException()
					},
					["text"] = Render(block.Callout.RichText),
					["color"] = ToColorString(block.Callout.Color.Value),
					["children"] = block.HasChildren ? RenderChildItems() : string.Empty,
				}
			);

	protected override string Render(ChildDatabaseBlock block)
		=> RenderUnsupported(block);

	protected override string Render(ChildPageBlock block) {
		if (!Resolver.TryGenerate(Page, block.Id, RenderType.HTML, out var childPageUrl, out var resource))
			return $"Unresolved child page {block.Id}";

		return RenderTemplate(
				"page_link",
				new NotionObjectTokens(block) {
					["icon"] = resource switch {
						LocalNotionPage { Thumbnail: not null, Thumbnail.Type: ThumbnailType.Emoji } localNotionPage => RenderTemplate(
													"icon_emoji",
													 new NotionObjectTokens(block) {
														 ["emoji"] = localNotionPage.Thumbnail.Data
													 }
												),
						LocalNotionPage { Thumbnail: not null, Thumbnail.Type: ThumbnailType.Image } localNotionPage => RenderTemplate(
													"icon_image",
													 new NotionObjectTokens(block) {
														 ["url"] = SanitizeUrl(localNotionPage.Thumbnail.Data)
													 }
												),
						_ => string.Empty
					},
					["url"] = SanitizeUrl(childPageUrl),
					["title"] = block.ChildPage.Title,
					["indicator"] = string.Empty,
				}
			);
	}

	protected override string Render(CodeBlock block) {
		return RenderTemplate(
			"code",
			new NotionObjectTokens(block) {
				["language"] = ToPrismLanguage(block.Code.Language),
				["code"] = Render(block.Code.RichText)
			}
		);

		string ToPrismLanguage(string language)
			=> language switch {
				"abap" => "abap",
				"ardunio" => "arduino",
				"bash" => "bash",
				"basic" => "basic",
				"c" => "c",
				"clojure" => "clojure",
				"coffeescript" => "coffeescript",
				"c++" => "cpp",
				"c#" => "cs",
				"css" => "css",
				"dart" => "dart",
				"diff" => "diff",
				"docker" => "docker",
				"elixer" => "elixir",
				"elm" => "elm",
				"erlang" => "erlang",
				"flow" => "flow",
				"fortran" => "fortran",
				"f#" => "fsharp",
				"gherkin" => "gherkin",
				"glsl" => "glsl",
				"go" => "go",
				"graphql" => "graphql",
				"groovy" => "groovy",
				"haskell" => "haskell",
				"html" => "html",
				"java" => "java",
				"javascript" => "javascript",
				"json" => "json",
				"julia" => "julia",
				"kotlin" => "kotlin",
				"latex" => "latex",
				"powershell" => "powershell",
				"prolog" => "prolog",
				"protobuf" => "protobuf",
				"python" => "python",
				"r" => "r",
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
				"typescript" => "typescript",
				"less" => "less",
				"lisp" => "lisp",
				"livescript" => "typescript",
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
		=> RenderChildItems();

	protected override string Render(ColumnListBlock block)
		=> this.CurrentRenderingNode.Children.Length switch {
			0 => RenderTemplate(
					"column_list_1",
					new NotionObjectTokens(block) {
						["column_1"] = string.Empty,
					}
				),
			var x and > 0 and <= 12 => RenderTemplate(
					$"column_list_{x}",
					new NotionObjectTokens(
						NotionObjectTokens
							.ExtractTokens(block)
							.Concat(
								Enumerable.Range(0, x).Select(
										i => new KeyValuePair<string, object>($"column_{i + 1}", (object)Render(CurrentRenderingNode.Children[i]))
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
				new NotionObjectTokens(block) {
					["expression"] = System.Net.WebUtility.HtmlEncode(block.Equation.Expression),
				}
			);

	protected override string Render(FileBlock block) {
		var url = Render(block.File);
		var filename = Path.GetFileName(Tools.Url.TryParse(url, out _, out _, out _, out var path, out _) ? path : url) ?? Constants.DefaultResourceTitle;

		return RenderTemplate(
			"file",
			new NotionObjectTokens(block) {
				["filename"] = filename,
				["caption"] = Render(block.File.Caption),
				["url"] = SanitizeUrl(url),
				["size"] = string.Empty,
			}
		);
	}

	protected override string Render(HeadingOneBlock block)
		=> RenderTemplate(
				"heading_1",
				new NotionObjectTokens(block) {
					["text"] = Render(block.Heading_1.RichText),
					["color"] = ToColorString(block.Heading_1.Color.Value)
				}
			);

	protected override string Render(HeadingTwoBlock block)
		=> RenderTemplate(
				"heading_2",
				new NotionObjectTokens(block) {
					["text"] = Render(block.Heading_2.RichText),
					["color"] = ToColorString(block.Heading_2.Color.Value)
				}
			);

	protected override string Render(HeadingThreeeBlock block)
		=> RenderTemplate(
				"heading_3",
				new NotionObjectTokens(block) {
					["text"] = Render(block.Heading_3.RichText),
					["color"] = ToColorString(block.Heading_3.Color.Value)
				}
			);

	protected override string Render(ImageBlock block)
		=> RenderTemplate(
				"image",
				new NotionObjectTokens(block) {
					["url"] = Render(block.Image),
					["caption"] = Render(block.Image.Caption)
				}
			);

	protected override string Render(LinkToPageBlock block) {
		if (!Resolver.TryGenerate(Page, block.LinkToPage.GetId(), RenderType.HTML, out var childPageUrl, out var resource))
			return $"Unresolved page {block.LinkToPage.GetId()}";

		return RenderTemplate(
				"page_link",
				new NotionObjectTokens(block) {
					["icon"] = resource switch {
						LocalNotionPage { Thumbnail: not null, Thumbnail.Type: ThumbnailType.Emoji } localNotionPage => RenderTemplate(
													"icon_emoji",
													 new NotionObjectTokens(block) {
														 ["emoji"] = localNotionPage.Thumbnail.Data
													 }
												),
						LocalNotionPage { Thumbnail: not null, Thumbnail.Type: ThumbnailType.Image } localNotionPage => RenderTemplate(
													"icon_image",
													 new NotionObjectTokens(block) {
														 ["url"] = localNotionPage.Thumbnail.Data
													 }
												),
						_ => string.Empty
					},
					["url"] = SanitizeUrl(childPageUrl),
					["title"] = resource.Title,
					["indicator"] = RenderTemplate("indicator_link")
				}
			);
	}

	protected override string Render(int number, NumberedListItemBlock block)
		=> RenderTemplate(
			"numbered_list_item",
			new NotionObjectTokens(block) {
				["number"] = number,
				["contents"] = Render(block.NumberedListItem.RichText),
				["color"] = ToColorString(block.NumberedListItem.Color.Value)
			}
		);

	protected override string Render(IEnumerable<NumberedListItemBlock> numberedListItems)
		=> RenderTemplate(
				"numbered_list",
				new NotionObjectTokens {
					["contents"] = Merge(numberedListItems.Select((item, index) => Render(index + 1, item)))
				}
			);

	protected override string Render(PDFBlock block)
		=> RenderTemplate(
			"pdf",
			new NotionObjectTokens(block) {
				["caption"] = Render(block.PDF.Caption),
				["url"] = Render(block.PDF)
			}
		);

	protected override string Render(ParagraphBlock block)
		=> RenderTemplate(
				"paragraph",
				new NotionObjectTokens(block) {
					["contents"] = Render(block.Paragraph.RichText),
					["color"] = ToColorString(block.Paragraph.Color.Value)
				}
			);

	protected override string Render(QuoteBlock block)
		=> RenderTemplate(
			"quote",
			new NotionObjectTokens(block) {
				["text"] = Render(block.Quote.RichText),
				["color"] = ToColorString(block.Quote.Color.Value)
			}
		);

	protected override string Render(SyncedBlockBlock block)
		=> RenderUnsupported(block);

	protected override string Render(TableOfContentsBlock block)
		=> RenderTemplate("table_of_contents",
			new NotionObjectTokens(block) {
				["color"] = ToColorString(block.TableOfContents.Color.Value)
			}
		);

	protected override string Render(TemplateBlock block)
		=> RenderUnsupported(block);

	protected override string Render(ToDoBlock block)
		=> RenderTemplate(
			"to_do",
			new NotionObjectTokens(block) {
				["text"] = Render(block.ToDo.RichText),
				["checked"] = block.ToDo.IsChecked ? "checked" : string.Empty,
				["color"] = ToColorString(block.ToDo.Color.Value)
			}
		);

	protected override string Render(ToggleBlock block)
		=> RenderTemplate(
			"toggle_closed",
			new NotionObjectTokens(block) {
				["toggle_id"] = $"toggle_{++_toggleCount}",
				["title"] = Render(block.Toggle.RichText),
				["color"] = ToColorString(block.Toggle.Color.Value),
				["contents"] = RenderChildItems(),
			}
		);

	protected override string RenderYouTubeEmbed(VideoBlock videoBlock, string youTubeVideoID)
		=> RenderTemplate(
			"embed_youtube",
			new NotionObjectTokens(videoBlock) {
				["caption"] = Render(videoBlock.Video.Caption),
				["video_id"] = youTubeVideoID,
			}
		);

	protected override string RenderVideoEmbed(VideoBlock videoBlock, string url)
		=> RenderTemplate(
			"embed_video",
			new NotionObjectTokens(videoBlock) {
				["caption"] = Render(videoBlock.Video.Caption),
				["url"] = Render(videoBlock.Video)
			}
		);

	protected override string RenderTwitterEmbed(EmbedBlock embedBlock, string url)
		=> RenderTemplate(
			"embed_twitter",
			new NotionObjectTokens(embedBlock) {
				["url"] = SanitizeUrl(embedBlock.Embed.Url),
				["caption"] = Render(embedBlock.Embed.Caption)
			}
		);

	protected override string RenderUnsupported(object @object)
		=> RenderTemplate(
			"unsupported",
			new NotionObjectTokens(@object) {
				["json"] = Tools.Json.WriteToString(@object ?? "NULL"),
			}
		);

	#endregion

	#region Aux

	protected virtual string RenderTemplate(string widgetType)
		=> RenderTemplate(widgetType, new NotionObjectTokens());

	protected virtual string RenderTemplate(string widget, NotionObjectTokens tokens) {
		_tokens = _tokens.AttachHead(tokens);
		try {
			return FetchTemplate(widget, ".html").FormatWithDictionary(_tokens, true);
		} finally {
			_tokens = _tokens.DetachHead();
		}
	}

	protected virtual string FetchTemplate(string widgetname, string fileExt) {
		var renderModePrefix = RenderMode switch { RenderMode.Editable => "editable", RenderMode.ReadOnly => "readonly", _ => throw new NotSupportedException(RenderMode.ToString()) };
		var sanitizedWidgetName = Tools.FileSystem.ToValidFolderOrFilename(widgetname);
		var sanitizedWidgetNameWithMode = $"{sanitizedWidgetName}.{Mode switch { LocalNotionMode.Offline => "offline", LocalNotionMode.Online => "online", _ => throw new NotSupportedException(Mode.ToString()) }}";
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

	protected virtual string CleanUpHtml(string html) {
		if (SuppressFormatting)
			return html;

		var options = new HtmlParserOptions {
			IsEmbedded = true,
			IsScripting = true
		};
		var parser = new HtmlParser(options);
		var document = parser.ParseDocument(html);
		var sw = new StringWriter();
		var formatter = new CleanHtmlFormatter();
		document.ToHtml(sw, formatter);
		var indentedHtml = sw.ToString();
		return indentedHtml;
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

	#endregion

	#region Inner Classes

	protected class NotionObjectTokens : DictionaryDecorator<string, object> {

		public NotionObjectTokens()
			: this(Enumerable.Empty<KeyValuePair<string, object>>()) {
		}

		public NotionObjectTokens(object @object)
			: this(@object is not null ? ExtractTokens(@object) : Enumerable.Empty<KeyValuePair<string, object>>()) {
		}

		public NotionObjectTokens(IEnumerable<KeyValuePair<string, object>> values) : base(new Dictionary<string, object>()) {
			Guard.ArgumentNotNull(values, nameof(values));
			this.AddRange(values, CollectionConflictPolicy.Throw);
		}

		private void HydrateObject(IObject notionObject) {
			this["object_id"] = notionObject.Id;
			this["object_type"] = notionObject.Object.ToString().ToLowerInvariant().Replace("_", "-");
			this["type"] = notionObject switch {
				IBlock block => block.Type.GetAttribute<EnumMemberAttribute>().Value?.Replace("_", "-"),
				_ => notionObject.GetType().Name
			} ?? string.Empty;
		}

		public static IEnumerable<KeyValuePair<string, object>> ExtractTokens(object @object) {
			if (@object == null)
				yield break;

			if (@object is IObject notionObject) {
				yield return new KeyValuePair<string, object>("object_id", notionObject.Id);
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