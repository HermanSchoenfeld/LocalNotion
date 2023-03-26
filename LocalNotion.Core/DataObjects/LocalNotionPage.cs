using Hydrogen;
using Newtonsoft.Json;
using Notion.Client;

namespace LocalNotion.Core;

public class LocalNotionPage : LocalNotionEditableResource {

	public override LocalNotionResourceType Type => LocalNotionResourceType.Page;
	
}