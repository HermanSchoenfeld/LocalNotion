namespace LocalNotion.Core;

public interface ICmsLocalNotionRepository : ILocalNotionRepository {
	CMSDatabase CMSDatabase { get; }

	bool ContainsCmsItem(string slug);

	bool TryGetCMSItem(string slug, out CMSItem cmsItem);
	
	void AddCMSItem(CMSItem cmsItem);

	void UpdateCMSItem(CMSItem cmsItem);

	void AddOrUpdateCMSItem(CMSItem cmsItem);

	void RemoveCmsItem(string slug);
}

public static class ICmsLocalNotionRepositoryExtensions {

	public static CMSItem GetCMSItem(this ICmsLocalNotionRepository cmsRepo, string slug) {
		if (!cmsRepo.TryGetCMSItem(slug, out var cmsItem))
			throw new InvalidOperationException($"CMS Item '{slug}' does not exist");
		return cmsItem;
	}
	
}