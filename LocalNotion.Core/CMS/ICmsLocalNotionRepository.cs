// Copyright (c) Herman Schoenfeld 2018 - Present. All rights reserved. (https://sphere10.com/products/localnotion)
// Author: Herman Schoenfeld <herman@sphere10.com>
//
// Distributed under the GPLv3 software license, see the accompanying file LICENSE 
// or visit https://github.com/HermanSchoenfeld/localnotion/blob/master/LICENSE
//
// This notice must not be removed when duplicating this file or its contents, in whole or in part.

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