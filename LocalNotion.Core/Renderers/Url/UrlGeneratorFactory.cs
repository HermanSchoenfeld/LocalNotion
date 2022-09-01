using System.Diagnostics;
using Hydrogen;
using Notion.Client;

namespace LocalNotion.Core;

public static class UrlGeneratorFactory {

	public static IUrlResolver Create(ILocalNotionRepository repository) 
		=> Create(repository, repository.Mode);

	public static IUrlResolver Create(ILocalNotionRepository repository, LocalNotionMode mode) 
		=> mode switch {
			LocalNotionMode.Online => new RemoteUrlResolver(repository),
			LocalNotionMode.Offline => new LocalUrlResolver(repository),
			_ => throw new NotSupportedException(mode.ToString())
		};
}