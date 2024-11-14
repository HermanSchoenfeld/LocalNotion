using System.Xml.Schema;
using Hydrogen;
using Notion.Client;

namespace LocalNotion.Core;

public static class PageExtensions {

	public static string GetTitle(this Page page) 
		=> (page.Properties.Values.FirstOrDefault(x => x is TitlePropertyValue) as TitlePropertyValue)?.Title.ToPlainText() ?? string.Empty;

	public static string GetPropertyDisplayValue(this Page page, string propertyName) {
		if (!page.Properties.TryGetValue(propertyName, out var propertyValue))
			throw new InvalidOperationException($"Property '{propertyName}' not found");
		return propertyValue.ToPlainText();
	}

	public static DateTimeOffset? GetPropertyDate(this Page page, string propertyName) {
		if (!page.Properties.TryGetValue(propertyName, out var propertyValue))
			throw new InvalidOperationException($"Property '{propertyName}' not found");
		var datePropertyValue = propertyValue as DatePropertyValue;
		Guard.Ensure(datePropertyValue != null, $"Property '{propertyName}' was not a date");
		return datePropertyValue.Date?.Start;
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