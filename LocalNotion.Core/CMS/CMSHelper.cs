using System.Runtime.CompilerServices;
using Hydrogen;
using Notion.Client;

namespace LocalNotion.Core;

public class CMSHelper {

	public static bool IsCMSDatabase(LocalNotionDatabase database)
		=> database.Properties != null &&
		   database.Properties.ContainsKey(Constants.PageTypePropertyName) &&
		   database.Properties.ContainsKey(Constants.TitlePropertyName) &&
		   database.Properties.ContainsKey(Constants.PublishOnPropertyName) &&
		   database.Properties.ContainsKey(Constants.StatusPropertyName) &&
		   database.Properties.ContainsKey(Constants.ThemesPropertyName) &&
		   database.Properties.ContainsKey(Constants.SlugPropertyName) &&
		   database.Properties.ContainsKey(Constants.SequencePropertyName) &&
		   database.Properties.ContainsKey(Constants.RootCategoryPropertyName) &&
		   database.Properties.ContainsKey(Constants.Category1PropertyName) &&
		   database.Properties.ContainsKey(Constants.Category2PropertyName) &&
		   database.Properties.ContainsKey(Constants.Category3PropertyName) &&
		   database.Properties.ContainsKey(Constants.Category4PropertyName) &&
		   database.Properties.ContainsKey(Constants.Category5PropertyName) &&
		   database.Properties.ContainsKey(Constants.TagsPropertyName) &&
		   database.Properties.ContainsKey(Constants.SummaryPropertyName) &&
		   database.Properties.ContainsKey(Constants.CreatedByPropertyName) &&
		   database.Properties.ContainsKey(Constants.CreatedOnPropertyName) &&
		   database.Properties.ContainsKey(Constants.EditedByPropertyName) &&
		   database.Properties.ContainsKey(Constants.EditedOnPropertyName) &&
		   database.Properties[Constants.ThemesPropertyName] is MultiSelectProperty &&
		   database.Properties[Constants.SequencePropertyName] is NumberProperty;

	public static bool IsCMSDatabase(Database database)
		=> database.Properties != null &&
		   database.Properties.ContainsKey(Constants.PageTypePropertyName) &&
		   database.Properties.ContainsKey(Constants.TitlePropertyName) &&
		   database.Properties.ContainsKey(Constants.PublishOnPropertyName) &&
		   database.Properties.ContainsKey(Constants.StatusPropertyName) &&
		   database.Properties.ContainsKey(Constants.ThemesPropertyName) &&
		   database.Properties.ContainsKey(Constants.SlugPropertyName) &&
		   database.Properties.ContainsKey(Constants.SequencePropertyName) &&
		   database.Properties.ContainsKey(Constants.RootCategoryPropertyName) &&
		   database.Properties.ContainsKey(Constants.Category1PropertyName) &&
		   database.Properties.ContainsKey(Constants.Category2PropertyName) &&
		   database.Properties.ContainsKey(Constants.Category3PropertyName) &&
		   database.Properties.ContainsKey(Constants.Category4PropertyName) &&
		   database.Properties.ContainsKey(Constants.Category5PropertyName) &&
		   database.Properties.ContainsKey(Constants.TagsPropertyName) &&
		   database.Properties.ContainsKey(Constants.SummaryPropertyName) &&
		   database.Properties.ContainsKey(Constants.CreatedByPropertyName) &&
		   database.Properties.ContainsKey(Constants.CreatedOnPropertyName) &&
		   database.Properties.ContainsKey(Constants.EditedByPropertyName) &&
		   database.Properties.ContainsKey(Constants.EditedOnPropertyName) &&
		   database.Properties[Constants.ThemesPropertyName] is MultiSelectProperty &&
		   database.Properties[Constants.SequencePropertyName] is NumberProperty;

	public static bool IsCMSPage(Page page)
		=> page.Properties != null &&
		   page.Properties.ContainsKey(Constants.PageTypePropertyName) &&
		   page.Properties.ContainsKey(Constants.TitlePropertyName) &&
		   page.Properties.ContainsKey(Constants.PublishOnPropertyName) &&
		   page.Properties.ContainsKey(Constants.StatusPropertyName) &&
		   page.Properties.ContainsKey(Constants.ThemesPropertyName) &&
		   page.Properties.ContainsKey(Constants.SlugPropertyName) &&
		   page.Properties.ContainsKey(Constants.SequencePropertyName) &&
		   page.Properties.ContainsKey(Constants.RootCategoryPropertyName) &&
		   page.Properties.ContainsKey(Constants.Category1PropertyName) &&
		   page.Properties.ContainsKey(Constants.Category2PropertyName) &&
		   page.Properties.ContainsKey(Constants.Category3PropertyName) &&
		   page.Properties.ContainsKey(Constants.Category4PropertyName) &&
		   page.Properties.ContainsKey(Constants.Category5PropertyName) &&
		   page.Properties.ContainsKey(Constants.TagsPropertyName) &&
		   page.Properties.ContainsKey(Constants.SummaryPropertyName) &&
		   page.Properties.ContainsKey(Constants.CreatedByPropertyName) &&
		   page.Properties.ContainsKey(Constants.CreatedOnPropertyName) &&
		   page.Properties.ContainsKey(Constants.EditedByPropertyName) &&
		   page.Properties.ContainsKey(Constants.EditedOnPropertyName) &&
		   page.Properties[Constants.ThemesPropertyName] is MultiSelectPropertyValue &&
		   page.Properties[Constants.SequencePropertyName] is NumberPropertyValue;

	public static bool IsPublicContent(LocalNotionPage page) 
		=> page.CMSProperties == null || IsContent(page) &&  page.CMSProperties.Status == CMSPageStatus.Published && (page.CMSProperties.PublishOn == null || page.CMSProperties.PublishOn <= DateTime.UtcNow);

	public static bool IsContent(LocalNotionPage page) 
		=> page.CMSProperties == null || page.CMSProperties.PageType.IsIn(CMSPageType.Page, CMSPageType.Section, CMSPageType.Gallery);

	public static CMSProperties ParseCMSProperties(string pageName, Page page) {
		Guard.ArgumentNotNull(page, nameof(page));
		var result = new CMSProperties();
		ParseCMSProperties(pageName, page, result);
		return result;
	}

	public static CMSProperties ParseCMSPropertiesAsChildPage(string childPageName, Page childPage, LocalNotionPage parentPage) { 
		Guard.ArgumentNotNull(childPage, nameof(childPage));
		Guard.ArgumentNotNull(parentPage, nameof(parentPage));
		var result = new CMSProperties();
		ParseCMSPropertiesAsChildPage(childPageName, childPage, parentPage, result);
		return result;
	}

	public static CMSProperties ParseCMSProperties(string pageName, Page page, CMSProperties result) {
		Guard.ArgumentNotNull(page, nameof(page));

		page.ValidatePropertiesExist(
			Constants.PageTypePropertyName,
			Constants.TitlePropertyName,
			Constants.PublishOnPropertyName,
			Constants.StatusPropertyName,
			Constants.ThemesPropertyName,
			Constants.SlugPropertyName,
			Constants.SequencePropertyName,
			Constants.RootCategoryPropertyName,
			Constants.Category1PropertyName,
			Constants.Category2PropertyName,
			Constants.Category3PropertyName,
			Constants.Category4PropertyName,
			Constants.Category5PropertyName,
			Constants.TagsPropertyName,
			Constants.SummaryPropertyName,
			Constants.CreatedByPropertyName,
			Constants.CreatedOnPropertyName,
			Constants.EditedByPropertyName,
			Constants.EditedOnPropertyName
		);

		result.PageType = Tools.Enums.ParseEnum<CMSPageType>(page.GetPropertyDisplayValue(Constants.PageTypePropertyName).ToValueWhenNullOrWhitespace(Tools.Enums.GetSerializableOrientedName(CMSPageType.Page)), false);
		result.PublishOn = page.GetPropertyDate(Constants.PublishOnPropertyName);
		result.Status = Tools.Parser.SafeParse(page.GetPropertyDisplayValue(Constants.StatusPropertyName), CMSPageStatus.Hidden);
		result.Themes = ((MultiSelectPropertyValue)page.Properties[Constants.ThemesPropertyName]).ToPlainTextValues().ToArray();
		result.CustomSlug = page.GetPropertyDisplayValue(Constants.SlugPropertyName).ToNullWhenWhitespace();
		result.Sequence = (int?)((NumberPropertyValue)page.Properties[Constants.SequencePropertyName]).Number;
		result.Root = page.GetPropertyDisplayValue(Constants.RootCategoryPropertyName)?.Trim().ToNullWhenWhitespace();
		result.Category1 = page.GetPropertyDisplayValue(Constants.Category1PropertyName)?.Trim().ToNullWhenWhitespace();
		result.Category2 = page.GetPropertyDisplayValue(Constants.Category2PropertyName)?.Trim().ToNullWhenWhitespace();
		result.Category3 = page.GetPropertyDisplayValue(Constants.Category3PropertyName)?.Trim().ToNullWhenWhitespace();
		result.Category4 = page.GetPropertyDisplayValue(Constants.Category4PropertyName)?.Trim().ToNullWhenWhitespace();
		result.Category5 = page.GetPropertyDisplayValue(Constants.Category5PropertyName)?.Trim().ToNullWhenWhitespace();
		result.Summary = page.GetPropertyDisplayValue(Constants.SummaryPropertyName)?.Trim().ToNullWhenWhitespace();
		result.Tags = ((MultiSelectPropertyValue)page.Properties[Constants.TagsPropertyName]).MultiSelect.Select(x => x.Name).Select(x => x.Trim()).ToArray();
		var pageTitle =page.GetTitle().ToValueWhenNullOrEmpty(Constants.DefaultResourceTitle);
			

		result.CustomSlug = CalculatePageSlug(pageTitle, result);
		
		// Process slug tokens if any
		if (result.CustomSlug != null)
			result.CustomSlug = ProcessSlugTokens(result.CustomSlug, page.Id, pageName, result);

		return result;
	}

	public static CMSProperties ParseCMSPropertiesAsChildPage(string childPageName, Page childPage, LocalNotionPage parentPage, CMSProperties result) {
		Guard.ArgumentNotNull(childPage, nameof(childPage));
		Guard.ArgumentNotNull(parentPage, nameof(parentPage));
		Guard.Argument(parentPage.CMSProperties != null, nameof(parentPage), "No CMS properties were defined on parent page");

		var parentCMSProps = parentPage.CMSProperties;
		var pageTitle = childPage.GetTitle().ToValueWhenNullOrEmpty(Constants.DefaultResourceTitle);
		result.PageType = CMSPageType.Page;
		result.PublishOn = parentCMSProps.PublishOn;
		result.Status = parentCMSProps.Status;
		//result.Themes = parentCMSProps.Themes; // NOTE: child pages from CMS pages should render using default template, since they are stand-alone pages (not sections, etc)
		result.CustomSlug = CalculateNestedPageSlug(parentCMSProps.PageType, parentCMSProps.CustomSlug, pageTitle);
		result.Root = parentCMSProps.Root;
		result.Category1 = parentCMSProps.Category1;
		result.Category2 = parentCMSProps.Category2;
		result.Category3 = parentCMSProps.Category3;
		result.Category4 = parentCMSProps.Category4;
		result.Category5 = parentCMSProps.Category5;
		result.Summary = null;
		result.Tags = parentCMSProps.Tags; // child pages inherit parent page tags

		// Process slug tokens if any
		result.CustomSlug = ProcessSlugTokens(result.CustomSlug, childPage.Id, childPageName, result);
		return result;
	}

	public static string CalculatePageSlug(string pageTitle, CMSProperties cmsProperties)  {
		var calculatedSlug = cmsProperties.CustomSlug ?? CalculateSlug(cmsProperties.Categories);
		return cmsProperties.PageType switch {
			CMSPageType.Section => calculatedSlug + (calculatedSlug.Contains("#") == false ? "#{page_name}": string.Empty),
			CMSPageType.Footer => calculatedSlug,
			_ => cmsProperties.CustomSlug ?? CalculateSlug(cmsProperties.Categories.Concat(pageTitle))
		};
	}
	
	public static string CalculateNestedPageSlug(CMSPageType pageType, string parentPageSlug, string childPageTitle) 
		// if parent is NOT a section and has anchor tag we treat it is an implicit "category" by simply replacing '#' with '/' 
		// if parent is a section and has anchor tag we ignore anchor tag
		// example: parent slug = /services#development   child slug = /services/development/mobile
		=> pageType switch  {
			CMSPageType.Section => $"{new string(parentPageSlug.TakeUntil(c => c == '#').ToArray())}/{Tools.Url.ToUrlSlug(childPageTitle)}".TrimStart("/"),
			_ => $"{parentPageSlug.Replace("###", "#").Replace("##", "#").Replace('#', '/')}/{Tools.Url.ToUrlSlug(childPageTitle)}".TrimStart("/")
		};

	/// <summary>
	/// Calculates a slug composed of the given component parts (will skip null or empty parts).
	/// </summary>
	/// <param name="parts"></param>
	/// <returns></returns>
	public static string CalculateSlug(IEnumerable<string> parts)
		=> LocalNotionHelper.SanitizeSlug(
		  	 parts
			 .Where(x => !string.IsNullOrEmpty(x))
			 .Select(Tools.Url.ToUrlSlug)
			 .ToDelimittedString("/")
		);

	public static string ProcessSlugTokens(string slug, string pageID, string pageName, CMSProperties properties) 
		=> Tools.Text.FormatWithDictionary(
			slug,
			new Dictionary<string, object>() {
				["id"] = pageID,
				["page_name"] = pageName ?? string.Empty,
				["publish_on"] = properties.PublishOn,
				["status"] = Tools.Enums.GetSerializableOrientedName(properties.Status) ?? string.Empty,
				["custom_slug"] = properties.CustomSlug ?? string.Empty,
				["themes"] = (properties.Themes ?? Array.Empty<string>()).ToDelimittedString(", "),
				["root"] = properties.Root ?? string.Empty,
				["category1"] = properties.Category1 ?? string.Empty,
				["category2"] = properties.Category2 ?? string.Empty,
				["category3"] = properties.Category3 ?? string.Empty,
				["category4"] = properties.Category4 ?? string.Empty,
				["category5"] = properties.Category5 ?? string.Empty
			},
			true
		);

}

