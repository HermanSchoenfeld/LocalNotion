using Sphere10.Framework;
using Notion.Client;

namespace LocalNotion.Core;

public static class ObjectIdExtensions {
	public static string GetObjectID(this Mention mention)
		=> mention.Type switch {
			"database" => mention.Database.Id,
			"date" => null, // Date mentions don't have an Id
			"link_preview" => null,
			"page" => mention.Page.Id,
			"template_mention" => null,
			"user" => mention.User.Id,
			_ => throw new InvalidOperationException($"Unrecognized mention type '{mention.Type}'")
		};
}
