using System.Runtime.CompilerServices;
using Hydrogen;
using Notion.Client;

namespace LocalNotion.Core;
internal class NotionCMSHelper {


	public static bool IsCMSPage(Page page)
		=> page.Properties.ContainsKey(Constants.TitlePropertyName) &&
		   page.Properties.ContainsKey(Constants.PublishOnPropertyName) &&
		   page.Properties.ContainsKey(Constants.StatusPropertyName) &&
		   page.Properties.ContainsKey(Constants.ThemePropertyName) &&
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

	public static CMSProperties ParseCMSProperties(Page page) {
		Guard.ArgumentNotNull(page, nameof(page));
		var result = new CMSProperties();
		ParseCMSProperties(page, result);
		return result;
	}

	public static CMSProperties ParseCMSPropertiesAsChildPage(Page childPage, LocalNotionPage parentPage) { 
		Guard.ArgumentNotNull(childPage, nameof(childPage));
		Guard.ArgumentNotNull(parentPage, nameof(parentPage));
		var result = new CMSProperties();
		ParseCMSPropertiesAsChildPage(childPage, parentPage, result);
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
		result.Status = Tools.Parser.SafeParse(page.GetPropertyDisplayValue(Constants.StatusPropertyName), CMSItemStatus.Hidden);
		result.Theme = page.GetPropertyDisplayValue(Constants.ThemePropertyName).ToNullWhenWhitespace();
		result.CustomSlug = page.GetPropertyDisplayValue(Constants.SlugPropertyName).ToNullWhenWhitespace();
		result.Root = page.GetPropertyDisplayValue(Constants.RootCategoryPropertyName).ToNullWhenWhitespace();
		result.Category1 = page.GetPropertyDisplayValue(Constants.Category1PropertyName).ToNullWhenWhitespace();
		result.Category2 = page.GetPropertyDisplayValue(Constants.Category2PropertyName).ToNullWhenWhitespace();
		result.Category3 = page.GetPropertyDisplayValue(Constants.Category3PropertyName).ToNullWhenWhitespace();
		result.Category4 = page.GetPropertyDisplayValue(Constants.Category4PropertyName).ToNullWhenWhitespace();
		result.Category5 = page.GetPropertyDisplayValue(Constants.Category5PropertyName).ToNullWhenWhitespace();
		result.Summary = page.GetPropertyDisplayValue(Constants.SummaryPropertyName).ToNullWhenWhitespace();
		NormalizeCategories(result);
		var pageTitle = page.GetTitle().ToValueWhenNullOrEmpty(Constants.DefaultResourceTitle);
		result.CustomSlug = CalculateCMSSlug(pageTitle, result);
		return result;
	}

	public static CMSProperties ParseCMSPropertiesAsChildPage(Page childPage, LocalNotionPage parentPage, CMSProperties result) {
		Guard.ArgumentNotNull(childPage, nameof(childPage));
		Guard.ArgumentNotNull(parentPage, nameof(parentPage));
		Guard.Argument(parentPage.CMSProperties != null, nameof(parentPage), "No CMS properties were defined on parent page");

		var parentCMSProps = parentPage.CMSProperties;
		var pageTitle = childPage.GetTitle().ToValueWhenNullOrEmpty(Constants.DefaultResourceTitle);
		result.PublishOn = parentCMSProps.PublishOn;
		result.Status = parentCMSProps.Status;
		result.Theme = parentCMSProps.Theme;
		result.CustomSlug = $"{parentCMSProps.CustomSlug.TrimEnd("/")}/{Tools.Url.ToUrlSlug(pageTitle)}";
		result.Root = parentCMSProps.Root;
		result.Category1 = parentCMSProps.Category1;
		result.Category2 = parentCMSProps.Category2;
		result.Category3 = parentCMSProps.Category3;
		result.Category4 = parentCMSProps.Category4;
		result.Category5 = parentCMSProps.Category5;
		result.Summary = null;
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


	public static string CalculateCMSSlug(string pageTitle, CMSProperties cmsProperties) 
		=> !string.IsNullOrWhiteSpace(cmsProperties.CustomSlug) ?
			LocalNotionHelper.SanitizeSlug(cmsProperties.CustomSlug) :
			CreatePageSlug(pageTitle, cmsProperties.Root, cmsProperties.Category1, cmsProperties.Category2, cmsProperties.Category3, cmsProperties.Category4, cmsProperties.Category5);

	public static string CreatePageSlug(string title, string root, string category1, string category2, string category3, string category4, string category5)
		=> CreateCategorySlug(root, category1, category2, category3, category4, category5) + "/" + LocalNotionHelper.SanitizeSlug($"{Tools.Url.ToUrlSlug(title)}");

	public static string CreateCategorySlug(string root, string category1, string category2, string category3, string category4, string category5)
		=> CreateCategorySlug(root, new[] { category1, category2, category3, category4, category5 });

	public static string CreateCategorySlug(string root, string[] categories)
		=> LocalNotionHelper.SanitizeSlug(
		  	 new[] { root }.Concat(categories)
			 .TakeWhile(x => !string.IsNullOrWhiteSpace(x))
			 .Select(Tools.Url.ToUrlSlug)
			 .ToDelimittedString("/")
		);


}

