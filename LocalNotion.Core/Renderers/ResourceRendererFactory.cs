using Hydrogen;

namespace LocalNotion.Core;

public static class ResourceRendererFactory {
	public static ResourceRenderer Create(ILocalNotionRepository repository, ILogger logger = null) 
		=> repository.CMSDatabaseID is not null ? 
			new CMSResourceRenderer(new LocalNotionCMS(repository), repository, logger) : 
			new ResourceRenderer(repository, logger);
}
