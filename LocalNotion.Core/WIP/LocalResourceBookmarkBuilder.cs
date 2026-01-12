// Copyright (c) Herman Schoenfeld 2018 - Present. All rights reserved. (https://sphere10.com/products/localnotion)
// Author: Herman Schoenfeld <herman@sphere10.com>
//
// Distributed under the GPLv3 software license, see the accompanying file LICENSE 
// or visit https://github.com/HermanSchoenfeld/localnotion/blob/master/LICENSE
//
// This notice must not be removed when duplicating this file or its contents, in whole or in part.

using Notion.Client;

namespace LocalNotion.Core;

public class LocalResourceBookmarkBuilder : IBookmarkBuilder {

	public LocalResourceBookmarkBuilder(ILocalNotionRepository repository, ILinkGenerator resolver) {
		Repository = repository;
		Resolver = resolver;
	}

	public ILocalNotionRepository Repository { get; }
	public ILinkGenerator Resolver { get; }

	public async Task<LocalNotionBookmark> Build(string url) {
		var pageID = url;  // url is the resource ID

		var localNotionPage = Repository.GetPage(pageID);

		var pageGraph = Repository.GetEditableResourceGraph(pageID);

		// LoadAsync the page objects
		var pageObjects = await Task.Run(() => Repository.LoadObjects(pageGraph));


		var page = pageObjects[pageGraph.ObjectID] as Page;

		var bookmark = new LocalNotionBookmark {
			Title = localNotionPage.Title,
			ImageUrl = localNotionPage.Cover
		};

		return bookmark;

	}
}
