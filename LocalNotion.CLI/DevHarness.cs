//using Hydrogen;
//using Hydrogen.Data;
//using Notion.Client;
//using LocalNotion;



// sync -o f26757a3-6505-499c-b65a-c5b8fa07501e bffe3340-e269-4f2a-9587-e793b70f5c3d 68944996-582b-453f-994f-d5562f4a6730 --filter-source "sphere10.com" "veloceon.com" --filter-root "Articles" "Services"

//const string APIKey = "YOUR_NOTION_API_KEY_HERE";
//const string DatabaseId = "f26757a3-6505-499c-b65a-c5b8fa07501e";
//const string DatabaseId2 = "68e1d4d0-a9a0-43cf-a0dd-6a7ef877d5ec";
//const string ArticleId = "9a6c5531-5b9d-41c5-98a0-55578f44a320";

//const string ColumnListId = "11819817-bf64-4f71-8499-b7ab395a6283";
//const string ColumnBlockInfo = "df82c962-6de2-46b1-949c-d93297e894dc";
//const string HowToArticleID = "bffe3340-e269-4f2a-9587-e793b70f5c3d";
//const string EmbeddedArticleID = "68944996-582b-453f-994f-d5562f4a6730";


////await PrintAllDatabase();
//var logger = new ConsoleLogger();
//SystemLog.RegisterLogger(logger);
//SystemLog.RegisterLogger(new FileAppendLogger("c:/temp/render/log.txt"));

//var client = NotionClientFactory.Create(new ClientOptions { AuthToken = APIKey });
////var repo = await LocalNotionRepository.CreateNew("c:\\temp\\render\\repo\\local_notion.json", LocalNotionMode.Offline, string.Empty, "c:\\temp\\objects",  "c:\\temp\\render\\pages", "c:\\temp\\render\\files", "c:\\temp\\templates", logger:logger); 
////var repo = await LocalNotionRepository.CreateNew("c:\\temp\\render\\local_notion.json", LocalNotionMode.Online, baseUrl: "/", logger:logger); 
////var repo = await LocalNotionRepository.CreateNew("c:\\temp\\render\\local_notion.json", LocalNotionMode.Offline, logger:logger); 
//var repo = await LocalNotionRepository.Open("c:\\temp\\render\\local_notion.json",  logger);

////repo.BaseUrl = "../../";

//var orchestrator = new NotionSyncOrchestrator(client, repo, logger);
////await orchestrator.RegisterPage("cda5ecf6-9476-4933-9d1f-52a1031b4f16");

////foreach (var page in repo.Resources.Where(x => x is LocalNotionPage).Cast<LocalNotionPage>()) {
////    orchestrator.RenderLocalPage(page.ID, PageRenderType.HTML, RenderMode.ReadOnly);
////}

//try {
////    await orchestrator.DownloadPage(HowToArticleID);
//  //  orchestrator.RenderLocalPage(HowToArticleID, PageRenderType.HTML, RenderMode.ReadOnly);
//    //orchestrator.RenderLocalPage(EmbeddedArticleID, PageRenderType.HTML, RenderMode.ReadOnly);
//} catch (Exception error) {
//    logger.LogException(error);
//}


//await orchestrator.DownloadDatabasePages(DatabaseId2);


///////////////////////////////////////////////////////////////////////////////////////////////////////////////
///////////////////////////////////////////////////////////////////////////////////////////////////////////////
///////////////////////////////////////////////////////////////////////////////////////////////////////////////
///////////////////////////////////////////////////////////////////////////////////////////////////////////////



////INotionRenderer consoleRenderer = new TextRenderer(new ConsoleTextWriter(), notionClient);

////await PrintAllDatabase();

////Console.WriteLine();

////await PrintDatabaseRows(DatabaseId);

////await PrintPage(ArticleId);

////await PrintPage(ColumnListId); 

////await PrintPage(ColumnBlockInfo);

////async Task PrintAllDatabase() {
////	var notionClient = NotionClientFactory.ExecuteCreateCommand(new ClientOptions { AuthToken = APIKey });
////	Console.WriteLine("Databases");
////	Console.WriteLine("=========");
////	var databases = await notionClient.Search.GetAllDatabases();
////	var renderer = new TextRenderer(notionClient);
////	foreach (var database in databases)
////		renderer.ExecuteRenderCommand(database);
////}

////async Task PrintDatabaseRows(string databaseId) {
////	Guard.ArgumentNotNull(databaseId, nameof(databaseId));
////	Console.WriteLine($"Database: {databaseId}");
////	Console.WriteLine("=".Repeat("Database: ".Length + databaseId.Length));;
////	var rows = await notionClient.Databases.GetAllDatabaseRows(databaseId);
////	Console.WriteLine($"Count: {rows.Length}");
////	for(var i = 0; i < rows.Length; i++) {
////		var row = rows[i];
////		Console.Write($"{i}");
////		await consoleRenderer.ExecuteRenderCommand(row);
////	}
////}

////async Task PrintPage(string pageID) {
////	await consoleRenderer.ExecuteRenderCommand(pageID);
////	//var xxx = blocks;
////}
