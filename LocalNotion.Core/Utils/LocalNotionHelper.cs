using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using CommandLine;
using Sphere10.Framework;
using Notion.Client;
using AngleSharp.Common;

namespace LocalNotion.Core;

public class LocalNotionHelper {
	
	public static bool TryCovertObjectIdToGuid(string objectID, out Guid guid)
		=> Guid.TryParse(objectID, out guid);

	public static bool IsValidObjectID(string objectID) => TryCovertObjectIdToGuid(objectID, out _);

	public static Guid ObjectIdToGuid(string objectID)
		=> TryCovertObjectIdToGuid(objectID, out var guid) ? guid : throw new ArgumentException($"Not a validly formatted object ID '{objectID}'", nameof(objectID));

	public static string ObjectGuidToId(Guid guid)
		=> guid.ToString().Trim("{}".ToCharArray());

	public static string SanitizeObjectID(string objectID) => ObjectGuidToId(ObjectIdToGuid(objectID));

	public static bool TryParseNotionFileUrl(string url, out string resourceID, out string filename) {
		resourceID = filename = null;
		if (!Tools.Url.TryParse(url, out var protocol, out var port, out var host, out var path, out var queryString))
			return false;

		if (!host.Contains("amazonaws") && !host.Contains("secure.notion-static.com"))
			return false;

		var splits = path.Split('/', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
		if (splits.Length != 3)
			return false;

		if (!Guid.TryParse(splits[1], out _))
			return false;

		resourceID = splits[1];
		filename = Uri.UnescapeDataString(splits[2]);
		if (!Tools.FileSystem.IsWellFormedFileName(filename))
			filename = "LN_"+Guid.Parse(resourceID).ToStrictAlphaString();
		
		return true;
	}

	public static LocalNotionPage ParsePage(Page notionPage) {
		var result = new LocalNotionPage();
		ParsePage(notionPage, result);
		return result;
	}

	public static void ParsePage(Page notionPage, LocalNotionPage localNotionPage) {
		localNotionPage.ID = notionPage.Id;
		localNotionPage.LastSyncedOn = DateTime.UtcNow;
		localNotionPage.LastEditedOn = notionPage.LastEditedTime;
		localNotionPage.CreatedOn = notionPage.CreatedTime;
		localNotionPage.Title = notionPage.GetTextTitle().ToValueWhenNullOrEmpty(Constants.DefaultResourceTitle);
		localNotionPage.Name = CalculatePageName(notionPage.Id, localNotionPage.Title); // note: this is made unique by NotionSyncOrchestrator
		localNotionPage.Cover = notionPage.Cover != null ? ParseCoverUrl(notionPage.Cover, out _) : null;
		localNotionPage.Keywords = Array.Empty<string>();
		if (notionPage.Icon != null) {
			localNotionPage.Thumbnail = new() {
				Type = notionPage.Icon switch {
					null => ThumbnailType.None,
					EmojiPageIcon => ThumbnailType.Emoji,
					FilePageIcon => ThumbnailType.Image,
					ExternalPageIcon => ThumbnailType.Image,
					CustomEmojiPageIcon => ThumbnailType.Image,
					_ => throw new ArgumentOutOfRangeException()
				},

				Data = notionPage.Icon switch {
					EmojiPageIcon emojiPageIcon => emojiPageIcon.Emoji,
					FilePageIcon filePageIcon => filePageIcon.File.Url,
					ExternalPageIcon externalPageIcon => externalPageIcon.External.Url,
					CustomEmojiPageIcon customEmojiPageIcon => customEmojiPageIcon.CustomEmoji.Url,
					_ => throw new ArgumentOutOfRangeException()
				}
			};
		} else localNotionPage.Thumbnail = LocalNotionThumbnail.None;

	}
	
	public static LocalNotionDatabase ParseDatabase(Database notionDatabase, DataSource notionPrimaryDataSource) {
		var result = new LocalNotionDatabase();
		ParseDatabase(notionDatabase, notionPrimaryDataSource, result);
		return result;
	}

	public static void ParseDatabase(Database notionDatabase, DataSource notionPrimaryDataSource, LocalNotionDatabase localNotionDatabase) {
		localNotionDatabase.ID = notionDatabase.Id;
		localNotionDatabase.LastSyncedOn = DateTime.UtcNow;
		localNotionDatabase.LastEditedOn = notionDatabase.LastEditedTime;
		localNotionDatabase.CreatedOn = notionDatabase.CreatedTime;
		localNotionDatabase.Title = notionDatabase.GetTextTitle().ToValueWhenNullOrEmpty(Constants.DefaultResourceTitle);
		localNotionDatabase.Name = CalculatePageName(notionDatabase.Id, localNotionDatabase.Title); // note: this is made unique by NotionSyncOrchestrator
		localNotionDatabase.Cover = notionDatabase.Cover != null ? ParseCoverUrl(notionDatabase.Cover, out _) : null;
		localNotionDatabase.Keywords = Array.Empty<string>();
		if (notionPrimaryDataSource.Icon != null) {
			localNotionDatabase.Thumbnail = new() {
				Type = notionDatabase.Icon switch {
					null => ThumbnailType.None,
					EmojiPageIcon => ThumbnailType.Emoji,
					FilePageIcon => ThumbnailType.Image,
					ExternalPageIcon => ThumbnailType.Image,
					CustomEmojiPageIcon => ThumbnailType.Image,
					_ => throw new ArgumentOutOfRangeException()
				},

				Data = notionPrimaryDataSource.Icon switch {
					EmojiPageIcon emojiPageIcon => emojiPageIcon.Emoji,
					FilePageIcon filePageIcon => filePageIcon.File.Url,
					ExternalPageIcon externalPageIcon => externalPageIcon.External.Url,
					CustomEmojiPageIcon customEmojiPageIcon => customEmojiPageIcon.CustomEmoji.Url,
					_ => throw new ArgumentOutOfRangeException()
				}
			};
		} else localNotionDatabase.Thumbnail = LocalNotionThumbnail.None;
		localNotionDatabase.PrimaryDataSourceID = notionDatabase.DataSources?.FirstOrDefault()?.DataSourceId;
		localNotionDatabase.Description = notionPrimaryDataSource.Description.ToPlainText();
		var properties = notionPrimaryDataSource.Properties.Reverse().ToList();
		MoveToBeginning<TitleProperty>(properties);
		MoveToEnd<LastEditedByProperty>(properties);
		MoveToEnd<LastEditedTimeProperty>(properties);
		MoveToEnd<CreatedByProperty>(properties);
		MoveToEnd<CreatedTimeProperty>(properties);
		//notionDatabase.Properties = properties.ToDictionary();
		localNotionDatabase.Properties = properties.ToDictionary();

		void MoveToBeginning<T>(List<KeyValuePair<string, DataSourcePropertyConfig>> list) where T : Property {
			var ix = list.FindIndex(x => x.Value is T);
			if (ix >= 0) {
				var item = list[ix];
				list.RemoveAt(ix);
				list.Insert(0, item);
			}
		}

		void MoveToEnd<T>(List<KeyValuePair<string, DataSourcePropertyConfig>> list) where T : Property {
			var ix = list.FindIndex(x => x.Value is T);
			if (ix >= 0) {
				var item = list[ix];
				list.RemoveAt(ix);
				list.Add(item);
			}
		}
	}

	public static string ParseCoverUrl(IPageCover cover, out string fileName) {
		fileName = Constants.DefaultResourceTitle;
		return cover switch {
			FilePageCover fileCover => TryParseNotionFileUrl(fileCover.File.Url, out _, out fileName) ? fileCover.File.Url : fileCover.File.Url,
			ExternalPageCover externalCover => externalCover.External.Url,
			_ => throw new ArgumentOutOfRangeException(nameof(cover), cover, null)
		};
	}

	public static string ParseFileUrl(FileObject notionFile, out string fileName) {
		fileName = Constants.DefaultResourceTitle;
		return notionFile switch {
			UploadedFile uploadedFile => TryParseNotionFileUrl(uploadedFile.File.Url, out _, out fileName) ? uploadedFile.File.Url : uploadedFile.File.Url,
			ExternalFile externalFile => externalFile.External.Url,
			_ => throw new ArgumentOutOfRangeException(nameof(notionFile), notionFile, null)
		};
	}

	public static string ParseFileUrl(FileObjectWithName notionFile, out string fileName) {
		fileName = notionFile.Name;
		return notionFile switch {
			UploadedFileWithName uploadedFileWithName => TryParseNotionFileUrl(uploadedFileWithName.File.Url, out _, out _) ? uploadedFileWithName.File.Url : uploadedFileWithName.File.Url,
			ExternalFileWithName externalFileWithName => externalFileWithName.External.Url,
			_ => throw new ArgumentOutOfRangeException(nameof(notionFile), notionFile, null)
		};
	}

	public static string ExtractDefaultPageSummary(NotionObjectGraph pageGraph, IDictionary<string, IObject> pageObjects) {
		string summary = null;
		// simply extract first 4 sentences from first text block`
		var firstParagraph =
			pageGraph
				.VisitAll()
				.Select(x => pageObjects[x.ObjectID])
				.Where(x => x is ParagraphBlock paragraph && paragraph.GetTextContents().Any(x => !string.IsNullOrWhiteSpace(x)))
				.Cast<ParagraphBlock>()
				.FirstOrDefault();
		if (firstParagraph != null) {
			summary = Tools.Text.EnumerateSentences(firstParagraph.Paragraph.RichText.ToPlainText()).FirstOrDefault();
			if (summary != null)
				summary = summary.Truncate(280);
		}

		return summary;
	}

	public static string SanitizeSlug(string slug) {
		return
			slug
				.Trim()
				.TrimStart('/')
				.Split('/', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
				.Select(Tools.Url.ToUrlSlug)
				.ToDelimittedString("/");
	}

	//public static string SanitizeSlug(string slug) {
	//	slug ??= string.Empty;
	//	if (!slug.StartsWith("/"))
	//		slug = "/" + slug;
	//	return slug;
	//}

	/// <summary>
	/// This calculates a UUID ID of a Page property so that it can be stored in the Local Notion Objects database. This is needed
	/// because Notion does not use UUID's for Page properties.
	/// </summary>
	/// <remarks>The algorithm used is: Guid.Parse( Blake2b_128( ToGuidBytes(PageID) ++ ToAsciiEncodedBytes(PropertyID) ) ) </remarks>
	/// <param name="pageID">The ID of the Page containing the property</param>
	/// <param name="propertyID">The Notion issued ID of the Page property (a string that is unique only to the Page)</param>
	/// <returns>A globally unique ID for the property.</returns>
	public string CalculatePagePropertyUUID(string pageID, string propertyID) {
		Guard.ArgumentNotNull(pageID, nameof(pageID));
		Guard.ArgumentNotNull(propertyID, nameof(propertyID));
		Guard.ArgumentParse<Guid>(pageID, nameof(pageID), out var pageGuid);
		var pageIDBytes = pageGuid.ToByteArray();
		var propIDBytes = Encoding.ASCII.GetBytes(propertyID);
		var result = LocalNotionHelper.ObjectGuidToId(new Guid(Hashers.JoinHash(CHF.Blake2b_128, pageIDBytes, propIDBytes)));
		return result;
	}

	public static string CalculatePageName(string id, string title) 
		=> Tools.Url.ToHtml4DOMObjectID($"{Tools.Text.ToCasing(TextCasing.KebabCase, title)}", String.Empty);

	public static string CalculateFeatureImageID(LocalNotionPage localPage, Page page, IDictionary<string, IObject> pageObjects, NotionObjectGraph objectGraph) {
		var firstImage = FindFirstImage(objectGraph);
		return firstImage?.Id;
		
		ImageBlock FindFirstImage(NotionObjectGraph node) {
			var block = pageObjects[node.ObjectID];
			if (block is ImageBlock imgBlock)
				return imgBlock;

			foreach(var childNode in node.Children) {
				var result = FindFirstImage(childNode);
				if (result is not null)
					return result;
			}
			
			return null;
		}
	}

	public static string CalculateFeatureImageID(LocalNotionDatabase localDatabase, Database database, NotionObjectGraph objectGraph) 
		=> null;

	public static bool IsNotionFileObject(object x)
		=> x is FileObject || x is FileObjectWithName || x is CustomEmojiPageIcon || x is FilePageIcon;
	
	public static IEnumerable<LocalNotionEditableResource> FilterRenderableResources(IEnumerable<LocalNotionResource> resources)
		=> resources.Where(x => x is LocalNotionEditableResource).Cast<LocalNotionEditableResource>().Distinct();

	public static IEnumerable<(string Slug, string RelPath, string MimeType)> EnumerateWebHostableResources(ILocalNotionRepository repo) {

		// All CMS items
		foreach(var item in repo.CMSItems.Where(x => !string.IsNullOrWhiteSpace(x.RenderPath))) {
			yield return (item.Slug, item.RenderPath, "text/html");
		}

		// All normally rendered resources if online
		if (repo.Paths.Mode == LocalNotionMode.Online) {
			foreach (var resource in repo.Resources) {
				var slug = string.Empty;
				RenderEntry render;
				string mimeType;
				switch (resource) {
					case LocalNotionDatabase db:
						if (!db.TryGetRender(RenderType.HTML, out render))
							continue;
						slug = render.Slug;
						mimeType = "text/html";
						break;
					case LocalNotionPage page:
						if (!page.TryGetRender(RenderType.HTML, out render))
							continue;
						slug = render.Slug;
						mimeType = "text/html";
						break;
					case LocalNotionFile file:
						if (!file.TryGetRender(RenderType.File, out render))
							continue;
						slug = render.Slug;
						mimeType = file.MimeType;
						break;
					default:
						throw new InternalErrorException("387FA12E-6E67-4C06-8ED2-DAFC83CCB161");
				}
				var repoPath = repo.Paths.GetRepositoryPath(FileSystemPathType.Absolute);
				var fullPath = Path.GetFullPath(render.LocalPath, repo.Paths.GetRepositoryPath(FileSystemPathType.Absolute));
				yield return (slug, Path.GetRelativePath(repoPath, fullPath), mimeType);

			}
		}
	}

	public static IEnumerable<string> CombineMultiPageKeyWords(IEnumerable<IEnumerable<string>> pageKeyWords)
		=> pageKeyWords.SelectMany(x => x).Distinct(StringComparer.InvariantCultureIgnoreCase).Take(50);


}

