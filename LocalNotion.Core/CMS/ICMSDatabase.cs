namespace LocalNotion.Core;

public interface ICMSDatabase {

	IEnumerable<CMSContentNode> Content { get; }

	bool ContainsSlug(string slug);

	bool TryGetContent(string slug, out CMSContentNode contentNode, out CMSContentType contentType);

	bool TryGetComponentPage(string slug, CMSPageType pageType, out LocalNotionPage page);

	bool FindComponentPage(string slug, CMSPageType pageType, out LocalNotionPage page);

	//bool TryGetFooter(string slug, out LocalNotionPage footerPage);
}
	