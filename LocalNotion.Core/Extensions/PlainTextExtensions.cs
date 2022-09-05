using Hydrogen;
using Notion.Client;

namespace LocalNotion.Core;

public static class PlainTextExtensions {
	public static string ToPlainText(this IEnumerable<RichTextBase> formattedText)
		=> formattedText.Select(x => x.PlainText).ToDelimittedString(" ");

	public static string ToPlainText(this Date date) {
		var start = date.Start.ToPlainText();
		var end = date.End.ToPlainText();
		return date.End == null ? start : $"{start} - {end}";
	}

	public static string ToPlainText(this DateTime? dateTime) 
		=> $"{dateTime:yyyy-MM-dd HH:mm:ss.fff}";

	public static string ToPlainText(this DateTime dateTime) 
		=> $"{dateTime:yyyy-MM-dd HH:mm:ss.fff}";

	public static string ToPlainText(this double? number) 
		=> $"{number}";

	public static string ToPlainText(this double number) 
		=> $"{number}";

	public static string ToPlainText(this bool? b) 
		=> $"{b}";

	public static string ToPlainText(this bool b) 
		=> $"{b}";

	public static string ToPlainText(this FormulaValue formulaValue) 
		=> formulaValue.Type switch{
			"string" => formulaValue.String,
			"number" => formulaValue.Number.ToPlainText(),
			"boolean" => formulaValue.Boolean.ToPlainText(),
			"date" => formulaValue.Date.ToPlainText(),
			_ => throw new NotSupportedException(formulaValue.Type.ToString())
		};
		

	public static string ToPlainText(this IPropertyItemObject propertyItemObject) 
		=> propertyItemObject switch {
			ListPropertyItem lpi => lpi.Results.Select(ToPlainText).ToDelimittedString(string.Empty),
			NumberPropertyItem pi => pi.Number.ToPlainText(),
			UrlPropertyItem pi => $"{pi.Url}",
			SelectPropertyItem pi => $"{pi.Select?.Name}",
			MultiSelectPropertyItem pi => $"{pi.MultiSelect?.Select(x => x.Name).ToDelimittedString(", ")}",
			DatePropertyItem pi => $"{pi.Date?.ToPlainText()}",
			EmailPropertyItem pi => $"{pi.Email}",
			PhoneNumberPropertyItem pi => $"{pi.PhoneNumber}",
			CheckboxPropertyItem pi => pi.Checkbox.ToPlainText(),
			FilesPropertyItem pi => $"{pi.Files?.Select(x => x.Name).ToDelimittedString(", ")}",
			CreatedByPropertyItem pi => $"{pi.CreatedBy?.Name}",
			CreatedTimePropertyItem pi => pi.CreatedTime.ToPlainText(),
			LastEditedByPropertyItem pi => $"{pi.LastEditedBy?.Name}",
			LastEditedTimePropertyItem pi => $"{pi.LastEditedTime.ToPlainText()}",
			FormulaPropertyItem pi => $"{pi.Formula?.ToPlainText()}",
			TitlePropertyItem pi => $"{pi.Title?.PlainText}",
			RichTextPropertyItem pi => $"{pi.RichText?.PlainText}",
			PeoplePropertyItem pi => $"{pi.People?.Name}",
			RelationPropertyItem pi => $"{pi.Relation?.Id}",
		};

}
