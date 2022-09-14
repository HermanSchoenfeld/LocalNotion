using System.Xml.Schema;
using Hydrogen;
using Notion.Client;

namespace LocalNotion.Core;

public static class DatabaseExtensions {

	public static string GetTitle(this Database database) 
		=> database.Title.ToPlainText();
}