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

	public static string ToPlainText(this PropertyValue propertyValue) 
		=> propertyValue switch {
			CheckboxPropertyValue pv=> $"{pv.Checkbox.ToPlainText()}",
			CreatedByPropertyValue pv => $"{pv.CreatedBy?.ToPlainText()})",
			CreatedTimePropertyValue pv => $"{pv.CreatedTime}",
			DatePropertyValue pv => $"{pv.Date?.ToPlainText()}",
			EmailPropertyValue pv => $"{pv.Email}",
			FilesPropertyValue pv => $"{pv.Files?.Select(f => f.Name).ToDelimittedString(", ")}",
			FormulaPropertyValue pv => $"{pv.Formula?.ToPlainText()}",
			LastEditedByPropertyValue pv => $"{pv.LastEditedBy?.ToPlainText()}",
			LastEditedTimePropertyValue pv => $"{pv.LastEditedTime}",
			MultiSelectPropertyValue pv => $"{pv.ToPlainTextValues().ToDelimittedString(", ")}",
			NumberPropertyValue pv => $"{pv.Number?.ToPlainText()}",
			PeoplePropertyValue pv => $"{pv.People?.Select(p => p.ToPlainText()).ToDelimittedString(", ")}",
			PhoneNumberPropertyValue pv => $"{pv.PhoneNumber}",
			RelationPropertyValue pv => $"{pv.Relation?.Select(o => o.Id).ToDelimittedString(", ")}",
			RichTextPropertyValue pv => $"{pv.RichText?.ToPlainText()}",
			RollupPropertyValue pv => $"{pv.Rollup?.ToPlainText()}",
			SelectPropertyValue pv => $"{pv.Select?.ToPlainText()}",
			StatusPropertyValue pv => $"{pv.Status?.ToPlainText()}",
			TitlePropertyValue pv => $"{pv.Title?.ToPlainText()}",
			UrlPropertyValue pv => $"{pv.Url}",
		};

	public static string ToPlainText(this User user) 
		=> user.Type switch {
			"person" => user.Person.ToPlainText(),
			"bot" => user.Bot.ToPlainText(),
			_ => throw new NotSupportedException(user.Type)
		};
	public static string ToPlainText(this Person person) => $"{person.Email}";

	public static string ToPlainText(this Bot bot) => $"{bot.Owner.ToPlainText()}";

	public static string ToPlainText(this IBotOwner botOwner) 
		=> botOwner switch {
			UserOwner bo=> $"Bot owned by user {bo.User}",
			WorkspaceIntegrationOwner bo => $"Bot owned by workspace",
			_ => throw new NotSupportedException(botOwner.Type)
		};

	public static string ToPlainText(this SelectOption selectOption) => $"{selectOption.Name}";

	public static string ToPlainText(this RollupValue selectOption) 
		=> selectOption.Type switch {
			"number" => $"{selectOption.Number.ToPlainText()}",
			"date" => $"{selectOption.Date.ToPlainText()}",
			"array" => $"{selectOption.Array.Select(pv => pv.ToPlainText()).ToDelimittedString(", ")}",
			_ => throw new NotSupportedException(selectOption.Type)
		};

	public static string ToPlainText(this StatusPropertyValue.Data data) => $"{data.Name}";


	public static IEnumerable<string> ToPlainTextValues(this MultiSelectPropertyValue propertyValue) 
		=>  propertyValue.MultiSelect?.Select(o => o.ToPlainText());
	
}
