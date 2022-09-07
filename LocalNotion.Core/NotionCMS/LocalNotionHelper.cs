using System.Runtime.CompilerServices;
using Hydrogen;
using Notion.Client;

namespace LocalNotion.Core;
internal class NotionCMSHelper {


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

	public static CMSProperties ParseCMSProperties(Page page, PageProperties properties) {
		Guard.ArgumentNotNull(page, nameof(page));
		var result = new CMSProperties();
		ParseCMSProperties(page, properties, result);
		return result;
	}

	public static CMSProperties ParseCMSProperties(Page page, PageProperties properties, CMSProperties result) {
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

		result.PublishOn = page.GetPropertyDateValue(properties, Constants.PublishOnPropertyName);
		result.Status = Tools.Parser.SafeParse(page.GetPropertyPlainTextValue(properties, Constants.StatusPropertyName), CMSItemStatus.Hidden);
		result.Location = page.GetPropertyPlainTextValue(properties, Constants.LocationPropertyName).ToNullWhenWhitespace();
		result.CustomSlug = page.GetPropertyPlainTextValue(properties, Constants.SlugPropertyName).ToNullWhenWhitespace();
		result.Root = page.GetPropertyPlainTextValue(properties, Constants.RootCategoryPropertyName).ToNullWhenWhitespace();
		result.Category1 = page.GetPropertyPlainTextValue(properties, Constants.Category1PropertyName).ToNullWhenWhitespace();
		result.Category2 = page.GetPropertyPlainTextValue(properties, Constants.Category2PropertyName).ToNullWhenWhitespace();
		result.Category3 = page.GetPropertyPlainTextValue(properties, Constants.Category3PropertyName).ToNullWhenWhitespace();
		result.Category4 = page.GetPropertyPlainTextValue(properties, Constants.Category4PropertyName).ToNullWhenWhitespace();
		result.Category5 = page.GetPropertyPlainTextValue(properties, Constants.Category5PropertyName).ToNullWhenWhitespace();
		result.Summary = page.GetPropertyPlainTextValue(properties, Constants.SummaryPropertyName).ToNullWhenWhitespace();
		NormalizeCategories(result);
		var pageTitle = page.GetTitle(properties).ToValueWhenNullOrEmpty(Constants.DefaultResourceTitle);
		result.CustomSlug = CalculateCMSSlug(pageTitle, result);
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

