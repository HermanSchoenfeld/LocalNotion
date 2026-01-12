// Copyright (c) Herman Schoenfeld 2018 - Present. All rights reserved. (https://sphere10.com/products/localnotion)
// Author: Herman Schoenfeld <herman@sphere10.com>
//
// Distributed under the GPLv3 software license, see the accompanying file LICENSE 
// or visit https://github.com/HermanSchoenfeld/localnotion/blob/master/LICENSE
//
// This notice must not be removed when duplicating this file or its contents, in whole or in part.

using Sphere10.Framework;

namespace LocalNotion.Core;


public abstract class LinkGeneratorBase : ILinkGenerator {

	protected LinkGeneratorBase(ILocalNotionRepository repository) {
		Repository = repository;
	}

	public abstract LocalNotionMode Mode { get; }

	public ILocalNotionRepository Repository { get; }


	public bool TryResolveResourceRender(string url, out LocalNotionResource resource, out RenderEntry entry) {
		resource = default;
		entry = default;

		if (LocalNotionRenderLink.TryParse(url, out var link)) {
			// Try to resolve by local notion resource link

			if (!Repository.TryGetResource(link.ResourceID, out resource))
				return false;

			if (!resource.TryGetRender(link.RenderType, out entry)) 
				return false;
			
			
		} else {
			// Try to resolve by slug

			if (!Repository.TryFindRenderBySlug(url, out var result))
				return false;
			
			if (!Repository.TryGetResource(result.ResourceID, out resource))
				return false;

			if (!resource.TryGetRender(result.RenderType, out entry))
				return false;
		}

		return true;
			
	}

	public abstract bool TryGenerate(LocalNotionResource from, string toResourceID, RenderType? renderType, out string url, out LocalNotionResource toResource);

}
