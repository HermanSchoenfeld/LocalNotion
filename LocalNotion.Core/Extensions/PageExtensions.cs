using Hydrogen;
using Notion.Client;

namespace LocalNotion.Core;


public static class PageExtensions {

	public static string? GetTitle(this Page page) {
		Guard.ArgumentNotNull(page, nameof(page));
		Guard.Ensure(page.FetchedProperties != null, "Properties have not been fetched");
		return (page.FetchedProperties.FirstOrDefault(x => x.Key == "title").Value as ListPropertyItem)?
			.Results
			.Cast<TitlePropertyItem>()
			.Select(x => x.Title)
			.ToPlainText();
	}

	public static string? GetPropertyPlainTextValue(this Page page, string propertyName) 
		=> page.GetPropertyObject(propertyName).ToPlainText();

	public static DateTime? GetPropertyDateValue(this Page page, string propertyName) {
		var dateProp = page.GetPropertyObject<DatePropertyItem>(propertyName);
		return dateProp.Date.Start ?? dateProp.Date.End;
	}

	public static TProperty GetPropertyObject<TProperty>(this Page page, string propertyName) where TProperty : class, IPropertyItemObject
		=> page.GetPropertyObject(propertyName) as TProperty ?? throw new InvalidOperationException($"Property '{propertyName}' not a '{typeof(TProperty).Name}' property");

	public static IPropertyItemObject GetPropertyObject(this Page page, string propertyName) {
		Guard.ArgumentNotNull(page, nameof(page));
		Guard.Ensure(page.FetchedProperties != null, "Properties have not been fetched");
		if (!page.Properties.TryGetValue(propertyName, out var propId))
			throw new ArgumentException($"Property '{propertyName}' not found", nameof(propertyName));

		if (!page.FetchedProperties.TryGetValue(propId.Id, out var propObj))
			throw new InvalidOperationException($"Property object '{propId.Id}' not found");

		return propObj;
	}

	public static void ValidatePropertiesExist(this Page page, params string[] propertyNames)
		=> page.ValidatePropertiesExist((IEnumerable<string>)propertyNames);

	public static void ValidatePropertiesExist(this Page page, IEnumerable<string> propertyNames)
		=> propertyNames.ForEach(x => ValidatePropertyExist(page, x));

	public static void ValidatePropertyExist(this Page page, string propertyName)
		=> Guard.Ensure(page.Properties.ContainsKey(propertyName), $"Missing property '{propertyName}'");

	public static ChildPageBlock AsChildPageBlock(this Page page)
		=> new() {
			Id = page.Id,
			CreatedTime = page.CreatedTime,
			LastEditedTime = page.LastEditedTime,
			HasChildren = true,
			ChildPage = new ChildPageBlock.Info {
				Title = page.GetTitle()
			}
		};
}