using System.Runtime.CompilerServices;
using System.Text;
using Hydrogen;
using Notion.Client;

namespace LocalNotion.Core;
internal class LocalNotionHelper {

	public static bool TryCovertObjectIdToGuid(string objectID, out Guid guid)
		=> Guid.TryParse(objectID, out guid);


	public static Guid ObjectIdToGuid(string objectID)
		=> TryCovertObjectIdToGuid(objectID, out var guid) ? guid : throw new ArgumentException($"Not a validly formatted object ID '{objectID}'", nameof(objectID));

	public static string ObjectGuidToId(Guid guid)
		=> guid.ToString().Trim("{}".ToCharArray());


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

	public static bool IsCMSPage(Page page)
		=> page.Properties.ContainsKey(Constants.TitlePropertyName) &&
		   page.Properties.ContainsKey(Constants.PublishOnPropertyName) &&
		   page.Properties.ContainsKey(Constants.StatusPropertyName) &&
		   page.Properties.ContainsKey(Constants.LocationPropertyName) &&
		   page.Properties.ContainsKey(Constants.SlugPropertyName) &&
		   page.Properties.ContainsKey(Constants.RootCategoryPropertyName) &&
		   page.Properties.ContainsKey(Constants.Category1PropertyName) &&
		   page.Properties.ContainsKey(Constants.Category2PropertyName) &&
		   page.Properties.ContainsKey(Constants.Category3PropertyName) &&
		   page.Properties.ContainsKey(Constants.Category4PropertyName) &&
		   page.Properties.ContainsKey(Constants.Category5PropertyName) &&
		   page.Properties.ContainsKey(Constants.TagsPropertyName) &&
		   page.Properties.ContainsKey(Constants.CreatedByPropertyName) &&
		   page.Properties.ContainsKey(Constants.CreatedOnPropertyName) &&
		   page.Properties.ContainsKey(Constants.EditedByPropertyName) &&
		   page.Properties.ContainsKey(Constants.EditedOnPropertyName);

	public static LocalNotionPage ParsePage(Page page) {
		var result = new LocalNotionPage();
		ParsePage(page, result);
		return result;
	}

	public static void ParsePage(Page page, LocalNotionPage dest) {
		dest.ID = page.Id;
		dest.LastEditedTime = page.LastEditedTime;
		dest.Title = page.GetTitle().ToValueWhenNullOrEmpty(Constants.DefaultResourceTitle);
		dest.Cover = page.Cover != null ? ParseFileUrl(page.Cover, out _) : null;
		if (page.Icon != null) {
			dest.Thumbnail = new() {
				Type = page.Icon switch {
					null => ThumbnailType.None,
					EmojiObject => ThumbnailType.Emoji,
					FileObject => ThumbnailType.Image,
					_ => throw new ArgumentOutOfRangeException()
				},

				Data = page.Icon switch {
					EmojiObject emojiObject => emojiObject.Emoji,
					FileObject fileObject => ParseFileUrl(fileObject, out _),
					_ => throw new ArgumentOutOfRangeException()
				}
			};
		} else dest.Thumbnail = LocalNotionThumbnail.None;

	}

	public static string ParseFileUrl(FileObject fileObject, out string name) {
		name = Constants.DefaultResourceTitle;
		return fileObject switch {
			UploadedFile uploadedFile => TryParseNotionFileUrl(uploadedFile.File.Url, out _, out name) ? uploadedFile.File.Url : uploadedFile.File.Url,
			ExternalFile externalFile => externalFile.External.Url,
			_ => throw new ArgumentOutOfRangeException(nameof(fileObject), fileObject, null)
		};
	}

	public static string ParseFileUrl(FileObjectWithName fileObject, out string name) {
		name = fileObject.Name;
		return fileObject switch {
			UploadedFileWithName uploadedFileWithName => TryParseNotionFileUrl(uploadedFileWithName.File.Url, out _, out _) ? uploadedFileWithName.File.Url : uploadedFileWithName.File.Url,
			ExternalFileWithName externalFileWithName => externalFileWithName.External.Url,
			_ => throw new ArgumentOutOfRangeException(nameof(fileObject), fileObject, null)
		};
	}

	public static string ExtractDefaultPageSummary(NotionObjectGraph pageGraph, IDictionary<string, IObject> pageObjects) {
		string summary = null;
		// simply extract first 4 sentences from first text block
		var firstParagraph =
			pageGraph
				.VisitAll()
				.Select(x => pageObjects[x.ObjectID])
				.Where(x => x is ParagraphBlock)
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

}

