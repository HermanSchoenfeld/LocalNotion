using System.Text.RegularExpressions;
using Hydrogen;
using LocalNotion.Core;
using Notion.Client;

namespace LocalNotion.Core;

public abstract class DatabaseRendererBase<TOutput> : IDatabaseRenderer<TOutput> {

	protected DatabaseRendererBase(LocalNotionDatabase database, Database notionDatabase, IEnumerable<Page> rows, ILinkGenerator resolver) {
		Guard.ArgumentNotNull(database, nameof(database));
		Guard.ArgumentNotNull(notionDatabase, nameof(notionDatabase));
		Guard.ArgumentNotNull(rows, nameof(rows));
		Guard.ArgumentNotNull(resolver, nameof(resolver));
		Database = database;
		NotionDatabase = notionDatabase;
		Rows = rows;
		Resolver = resolver;
	}

	protected LocalNotionDatabase Database { get; }

	protected Database NotionDatabase { get; }

	protected IEnumerable<Page> Rows { get; set; }

	protected ILinkGenerator Resolver { get; }
	
	public virtual TOutput Render() => throw new NotImplementedException();

}
