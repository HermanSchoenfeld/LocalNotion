using Notion.Client;

namespace LocalNotion.Core;

public class LocalResourceBookmarkBuilder : IBookmarkBuilder {

	public LocalResourceBookmarkBuilder(ILocalNotionRepository repository, IUrlResolver resolver) {
		Repository = repository;
		Resolver = resolver;
	}

	public ILocalNotionRepository Repository { get; }
	public IUrlResolver Resolver { get; }

	public async Task<LocalNotionBookmark> Build(string url) {
		var pageID = url;  // url is the resource ID

		var localNotionPage = Repository.GetPage(pageID);

		var pageGraph = Repository.GetPageGraph(pageID);

		// Load the page objects
		var pageObjects = await Task.Run(() => Repository.LoadObjects(pageGraph));


		var page = pageObjects[pageGraph.ObjectID] as Page;

		var bookmark = new LocalNotionBookmark {
			Title = localNotionPage.Title,
			ImageUrl = localNotionPage.Cover
		};

		return bookmark;

	}
}
