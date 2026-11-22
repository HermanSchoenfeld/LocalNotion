// Copyright (c) Herman Schoenfeld 2018 - Present. All rights reserved. (https://sphere10.com/products/localnotion)
// Author: Herman Schoenfeld <herman@sphere10.com>
//
// Distributed under the GPLv3 software license, see the accompanying file LICENSE
// or visit https://github.com/HermanSchoenfeld/localnotion/blob/master/LICENSE
//
// This notice must not be removed when duplicating this file or its contents, in whole or in part.

using System.Collections.Generic;
using LocalNotion.Core;
using Notion.Client;

namespace LocalNotion.Core.Tests;

internal sealed class FixtureRepository(IPathResolver pathResolver) : LocalNotionRepositoryDecorator(null), ILocalNotionRepository {

	public Dictionary<string, IObject> ObjectMap { get; } = new();

	public Dictionary<string, NotionObjectGraph> GraphMap { get; } = new();

	public Dictionary<string, LocalNotionResource> ResourceMap { get; } = new();

	public Dictionary<string, LocalNotionResource> Parents { get; } = new();

	public override IPathResolver Paths => pathResolver;

	public override string[] DefaultThemes => ["default"];

	string ILocalNotionRepository.CMSDatabaseID => CMSDatabaseIdentifier;

	public string CMSDatabaseIdentifier { get; set; }

	public int ObjectReads { get; private set; }

	public override bool TryGetObject(string idValue, out IObject valueValue) {
		ObjectReads++;
		return ObjectMap.TryGetValue(idValue, out valueValue);
	}

	public override bool ContainsObject(string idValue) => ObjectMap.ContainsKey(idValue);

	public override bool TryGetResource(string idValue, out LocalNotionResource valueValue) => ResourceMap.TryGetValue(idValue, out valueValue);

	public override bool ContainsResource(string idValue) => ResourceMap.ContainsKey(idValue);

	public override bool TryGetResourceGraph(string idValue, out NotionObjectGraph valueValue) => GraphMap.TryGetValue(idValue, out valueValue);

	public override bool TryGetParentResource(string idValue, out LocalNotionResource valueValue) => Parents.TryGetValue(idValue, out valueValue);

	public override bool TryFindRenderBySlug(string slugValue, out CachedSlug valueValue) {
		foreach (var resourceValue in ResourceMap.Values)
			foreach (var renderValue in resourceValue.Renders)
				if (renderValue.Value.Slug == slugValue) {
					valueValue = new(resourceValue.ID, renderValue.Key, renderValue.Value.Slug);
					return true;
				}
		valueValue = null;
		return false;
	}
}
