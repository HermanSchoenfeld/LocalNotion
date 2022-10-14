using Notion.Client;

namespace LocalNotion.Core;

public delegate bool ObjectLookupDelegate(string objectID, out IObject value);