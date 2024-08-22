using System.Diagnostics;
using Hydrogen;
using Notion.Client;

namespace LocalNotion.Core;

public static class LinkGeneratorFactory {

	public static ILinkGenerator Create(ILocalNotionRepository repository) 
		=> Create(repository, repository.Paths.Mode);

	public static ILinkGenerator Create(ILocalNotionRepository repository, LocalNotionMode mode) 
		=> mode switch {
			LocalNotionMode.Online => new OnlineLinkGenerator(repository),
			LocalNotionMode.Offline => new OfflineLinkGenerator(repository),
			_ => throw new NotSupportedException(mode.ToString())
		};
}