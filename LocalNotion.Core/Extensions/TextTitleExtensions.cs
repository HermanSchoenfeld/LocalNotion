using System.Xml.Schema;
using Sphere10.Framework;
using Notion.Client;

namespace LocalNotion.Core;

public static class DatabaseExtensions {

	public static string GetTextTitle(this DataSource datasource) 
		=> datasource.Title.ToPlainText();

	
	public static string GetTextTitle(this Database database) 
		=> database.Title.ToPlainText();

	public static string GetTextTitle(this Block block)
		=> $"[Block Type=\"{block.Type}\" ID=\"{block.Id}\" ]";

	public static string GetTextTitle(this Comment comment)
		=> $"[Comment ID=\"{comment.Id}\" Text=\"{comment.RichText.ToPlainText().TrimToLength(10, "...")}\"]";

	public static string GetTextTitle(this FileUpload fileUpload)
		=> $"[FileUpload ID=\"{fileUpload.Id}\" Name=\"{fileUpload.FileName}\"]";
}