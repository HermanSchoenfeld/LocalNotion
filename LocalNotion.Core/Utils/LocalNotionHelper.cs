using System.Runtime.CompilerServices;
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

		if (!host.Contains("amazonaws"))
			return false;

		var splits = path.Split('/', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
		if (splits.Length != 3)
			return false;

		if (!Guid.TryParse(splits[1], out _))
			return false;

		resourceID = splits[1];
		filename = Uri.UnescapeDataString(splits[2]);
		return true;
	}

	public static LocalNotionFile ParseFile( string resourceID, string filename, string slugPrefix = "files")
		=> new() {
			ID = resourceID,
			MimeType = Tools.Network.GetMimeType(filename),
			Title = filename,
			DefaultSlug = SanitizeSlug($"{slugPrefix}/{resourceID}") + $"/{filename}"
		};

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

	public static LocalNotionPage ParsePage(Page page, string slugPrefix = "pages") {
		var result = new LocalNotionPage();
		ParsePage(page, result, slugPrefix);
		return result;
	}

	public static void ParsePage(Page page, LocalNotionPage dest, string slugPrefix = "pages") {
		dest.ID = page.Id;
		dest.Parent = page.Parent.GetParentId();
		dest.LastEditedTime = page.LastEditedTime;
		dest.Title = page.GetTitle().ToValueWhenNullOrEmpty(Constants.DefaultResourceTitle);
		dest.DefaultSlug = SanitizeSlug($"{slugPrefix}/{page.Id}/{dest.Title}");
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

		if (IsCMSPage(page))
			dest.CMSProperties = ParseCMSProperties(page);
	
	}

	public static CMSProperties ParseCMSProperties(Page page) {
		Guard.ArgumentNotNull(page, nameof(page));
		var result = new CMSProperties();
		ParseCMSProperties(page, result);
		return result;
	}

	public static CMSProperties ParseCMSProperties(Page page, CMSProperties result) {
		Guard.ArgumentNotNull(page, nameof(page));

		page.ValidatePropertiesExist(
			Constants.RootCategoryPropertyName,
			Constants.Category1PropertyName,
			Constants.Category2PropertyName,
			Constants.Category3PropertyName,
			Constants.Category4PropertyName,
			Constants.Category5PropertyName,
			Constants.TagsPropertyName,
			Constants.SummaryPropertyName
		);
		

		result.PublishOn = page.GetPropertyDate(Constants.PublishOnPropertyName);
		result.Status = Tools.Parser.SafeParse(page.GetPropertyTitle(Constants.StatusPropertyName), CMSItemStatus.Hidden);
		result.Location = page.GetPropertyTitle(Constants.LocationPropertyName).ToNullWhenWhitespace();
		result.Slug = page.GetPropertyTitle(Constants.SlugPropertyName).ToNullWhenWhitespace();
		result.Root = page.GetPropertyTitle(Constants.RootCategoryPropertyName).ToNullWhenWhitespace();
		result.Category1 = page.GetPropertyTitle(Constants.Category1PropertyName).ToNullWhenWhitespace();
		result.Category2 = page.GetPropertyTitle(Constants.Category2PropertyName).ToNullWhenWhitespace();
		result.Category3 = page.GetPropertyTitle(Constants.Category3PropertyName).ToNullWhenWhitespace();
		result.Category4 = page.GetPropertyTitle(Constants.Category4PropertyName).ToNullWhenWhitespace();
		result.Category5 = page.GetPropertyTitle(Constants.Category5PropertyName).ToNullWhenWhitespace();
		result.Summary = page.GetPropertyTitle(Constants.SummaryPropertyName).ToNullWhenWhitespace();
		NormalizeCategories(result);
		var pageTitle = page.GetTitle().ToValueWhenNullOrEmpty(Constants.DefaultResourceTitle);
		result.Slug = CalculateCMSSlug(pageTitle, result);
		return result;
	}

	public static void NormalizeCategories(CMSProperties cmsProperties) {
		const int HierarchyLevels = 6;

		// Remove dangling categories
		for (var i = 0; i < HierarchyLevels; i++) {
			var categoryName = i switch {
				0 => cmsProperties.Root,
				1 => cmsProperties.Category1,
				2 => cmsProperties.Category2,
				3 => cmsProperties.Category3,
				4 => cmsProperties.Category4,
				5 => cmsProperties.Category5,
			};
			if (string.IsNullOrWhiteSpace(categoryName)) {
				for (var j = i; j < HierarchyLevels; j++) {
					switch (j) {
						case 0:
							cmsProperties.Root = null;
							break;
						case 1:
							cmsProperties.Category1 = null;
							break;
						case 2:
							cmsProperties.Category2 = null;
							break;
						case 3:
							cmsProperties.Category3 = null;
							break;
						case 4:
							cmsProperties.Category4 = null;
							break;
						case 5:
							cmsProperties.Category5 = null;
							break;
					}
				}
			}
		}
		// trim whitespace
		cmsProperties.Root = cmsProperties.Root?.Trim();
		cmsProperties.Category1 = cmsProperties.Category1?.Trim();
		cmsProperties.Category2 = cmsProperties.Category2?.Trim();
		cmsProperties.Category3 = cmsProperties.Category3?.Trim();
		cmsProperties.Category4 = cmsProperties.Category4?.Trim();
		cmsProperties.Category5 = cmsProperties.Category5?.Trim();
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

	public static string CalculateCMSSlug(string pageTitle, CMSProperties cmsProperties) 
		=> !string.IsNullOrWhiteSpace(cmsProperties.Slug) ?
			SanitizeSlug(cmsProperties.Slug) :
			CreatePageSlug(pageTitle, cmsProperties.Root, cmsProperties.Category1, cmsProperties.Category2, cmsProperties.Category3, cmsProperties.Category4, cmsProperties.Category5);

	public static string CreatePageSlug(string title, string root, string category1, string category2, string category3, string category4, string category5)
		=> CreateCategorySlug(root, category1, category2, category3, category4, category5) + "/" + SanitizeSlug($"{Tools.Url.ToUrlSlug(title)}");

	public static string CreateCategorySlug(string root, string category1, string category2, string category3, string category4, string category5)
		=> CreateCategorySlug(root, new[] { category1, category2, category3, category4, category5 });

	public static string CreateCategorySlug(string root, string[] categories)
		=> SanitizeSlug(
		  	 new[] { root }.Concat(categories)
			 .TakeWhile(x => !string.IsNullOrWhiteSpace(x))
			 .Select(Tools.Url.ToUrlSlug)
			 .ToDelimittedString("/")
		);

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

	private static string SanitizeSlug(string slug) {
		return
			slug
				.Trim()
				.TrimStart('/')
				.Split('/', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
				.Select(Tools.Url.ToUrlSlug)
				.ToDelimittedString("/");
	}


}

