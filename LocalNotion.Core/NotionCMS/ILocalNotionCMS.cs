namespace LocalNotion.Core;

public interface ILocalNotionCMS {

	IEnumerable<CMSContentNode> Content { get; }

	bool ContainsSlug(string slug);

	bool TryGetContent(string slug, out CMSContentNode contentNode, out CMSContentType contentType);

	bool TryGetFooter(string slug, out LocalNotionPage footerPage);
}
	