using System.Runtime.CompilerServices;
using Hydrogen;
using Notion.Client;

namespace LocalNotion.Core;
internal class NotionCMSHelper {


	public static bool IsCMSPage(Page page)
		=> page.Properties.ContainsKey(Constants.TitlePropertyName) &&
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

	public static CMSProperties ParseCMSProperties(string pageName, Page page, HtmlThemeManager htmlThemeManager) {
		Guard.ArgumentNotNull(page, nameof(page));
		var result = new CMSProperties();
		ParseCMSProperties(pageName, page, htmlThemeManager, result);
		return result;
	}

	public static CMSProperties ParseCMSPropertiesAsChildPage(string childPageName, Page childPage, LocalNotionPage parentPage) { 
		Guard.ArgumentNotNull(childPage, nameof(childPage));
		Guard.ArgumentNotNull(parentPage, nameof(parentPage));
		var result = new CMSProperties();
		ParseCMSPropertiesAsChildPage(childPageName, childPage, parentPage, result);
		return result;
	}

	public static CMSProperties ParseCMSProperties(string pageName, Page page, HtmlThemeManager htmlThemeManager, CMSProperties result) {
		Guard.ArgumentNotNull(page, nameof(page));

		page.ValidatePropertiesExist(
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

		result.PublishOn = page.GetPropertyDate(Constants.PublishOnPropertyName);
		result.Status = Tools.Parser.SafeParse(page.GetPropertyDisplayValue(Constants.StatusPropertyName), CMSItemStatus.Hidden);
		result.Themes = ((MultiSelectPropertyValue)page.Properties[Constants.ThemesPropertyName]).ToPlainTextValues().ToArray();
		result.CustomSlug = page.GetPropertyDisplayValue(Constants.SlugPropertyName).ToNullWhenWhitespace();
		result.Sequence = (int?)((NumberPropertyValue)page.Properties[Constants.SequencePropertyName]).Number;
		result.Root = page.GetPropertyDisplayValue(Constants.RootCategoryPropertyName).ToNullWhenWhitespace();
		result.Category1 = page.GetPropertyDisplayValue(Constants.Category1PropertyName).ToNullWhenWhitespace();
		result.Category2 = page.GetPropertyDisplayValue(Constants.Category2PropertyName).ToNullWhenWhitespace();
		result.Category3 = page.GetPropertyDisplayValue(Constants.Category3PropertyName).ToNullWhenWhitespace();
		result.Category4 = page.GetPropertyDisplayValue(Constants.Category4PropertyName).ToNullWhenWhitespace();
		result.Category5 = page.GetPropertyDisplayValue(Constants.Category5PropertyName).ToNullWhenWhitespace();
		result.Summary = page.GetPropertyDisplayValue(Constants.SummaryPropertyName).ToNullWhenWhitespace();
		NormalizeCategories(result);
		var pageTitle = page.GetTitle().ToValueWhenNullOrEmpty(Constants.DefaultResourceTitle);

		// TODO: in future, determine if a page is a section or not based on some column or something else
		// HACK: CMSProperty IsPartial is determined based on theme traits. In future, this should be
		// specified as part of the page definition (but this requires new LocalNotion CMS structure). 
		// This should be done in subsequent version.
		if (result.Themes != null) {
			foreach (var theme in result.Themes) {
				if (htmlThemeManager.TryLoadTheme(theme, out var themeInfo) &&
				    themeInfo is HtmlThemeInfo htmlThemeInfo &&
				    htmlThemeInfo.Traits.HasFlag(HtmlThemeTraits.PartialPage)) {
					result.IsPartial = true;
					break;
				}
			}
		}

		if (string.IsNullOrWhiteSpace(result.CustomSlug))
			result.CustomSlug = CalculateCMSSlug(pageTitle, result);
		
		// Process slug tokens if any
		if (result.CustomSlug != null)
			result.CustomSlug = ProcessSlugTokens(result.CustomSlug, page.Id, pageName, result);

		return result;
	}

	public static CMSProperties ParseCMSPropertiesAsChildPage(string childPageName,  Page childPage, LocalNotionPage parentPage, CMSProperties result) {
		Guard.ArgumentNotNull(childPage, nameof(childPage));
		Guard.ArgumentNotNull(parentPage, nameof(parentPage));
		Guard.Argument(parentPage.CMSProperties != null, nameof(parentPage), "No CMS properties were defined on parent page");

		var parentCMSProps = parentPage.CMSProperties;
		var pageTitle = childPage.GetTitle().ToValueWhenNullOrEmpty(Constants.DefaultResourceTitle);
		result.PublishOn = parentCMSProps.PublishOn;
		result.Status = parentCMSProps.Status;
		//result.Themes = parentCMSProps.Themes; // NOTE: child pages from CMS pages should render using default template, since they are stand-alone pages (not sections, etc)
		result.CustomSlug = CalculateCMSChildPageSlug(parentCMSProps.CustomSlug, pageTitle);
		result.Root = parentCMSProps.Root;
		result.Category1 = parentCMSProps.Category1;
		result.Category2 = parentCMSProps.Category2;
		result.Category3 = parentCMSProps.Category3;
		result.Category4 = parentCMSProps.Category4;
		result.Category5 = parentCMSProps.Category5;
		result.Summary = null;

		// Process slug tokens if any
		result.CustomSlug = ProcessSlugTokens(result.CustomSlug, childPage.Id, LocalNotionHelper.CalculatePageName(childPage.Id,  childPage.GetTitle()), result);
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


	public static string CalculateCMSSlug(string pageTitle, CMSProperties cmsProperties)  {
		var slug = !string.IsNullOrWhiteSpace(cmsProperties.CustomSlug) ?
			LocalNotionHelper.SanitizeSlug(cmsProperties.CustomSlug) :
			CreatePageSlug(pageTitle, cmsProperties.Root, cmsProperties.Category1, cmsProperties.Category2, cmsProperties.Category3, cmsProperties.Category4, cmsProperties.Category5);

		// Partial pages have an anchor to their name
		if (cmsProperties.IsPartial) {
			// replace the last part of the url "/{page_title}" with "/{page_name}"
			var ix = slug.LastIndexOf('/');
			if (ix != -1) {
				slug = slug.Substring(0, ix);
			}
			slug += "#{page_name}"; // tokens are resolved by caller
		}

		return slug;
	}

	public static string CalculateCMSChildPageSlug(string parentPageSlug, string childPageTitle) 
		// if parent has anchor tag we treat it is an implicit "category" by simply replacing '#' with '/'
		// example: parent slug = /services#development   child slug = /services/development/mobile
		=> $"{parentPageSlug.Replace("###", "#").Replace("##", "#").Replace('#', '/')}/{Tools.Url.ToUrlSlug(childPageTitle)}";
	

	public static string CreatePageSlug(string title, string root, string category1, string category2, string category3, string category4, string category5)
		=> CreateCategorySlug(root, category1, category2, category3, category4, category5) + "/" + LocalNotionHelper.SanitizeSlug($"{Tools.Url.ToUrlSlug(title)}");

	public static string CreateCategorySlug(string root, string category1, string category2, string category3, string category4, string category5)
		=> CreateCategorySlug(root, new[] { category1, category2, category3, category4, category5 });

	public static string CreateCategorySlug(string root, string[] categories)
		=> LocalNotionHelper.SanitizeSlug(
		  	 new[] { root }.Concat(categories)
			 .TakeWhile(x => !string.IsNullOrEmpty(x))
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

