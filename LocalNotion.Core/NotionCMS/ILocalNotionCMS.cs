namespace LocalNotion.Core;

public interface ILocalNotionCMS {
	bool ContainsSlug(string slug);

	bool TryGetContent(string slug, out CMSContentNode contentNode, out CMSContentType contentType);
}
	