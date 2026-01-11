// Copyright (c) Herman Schoenfeld 2018 - Present. All rights reserved. (https://sphere10.com/products/localnotion)
// Author: Herman Schoenfeld <herman@sphere10.com>
//
// Distributed under the GPLv3 software license, see the accompanying file LICENSE 
// or visit https://github.com/HermanSchoenfeld/localnotion/blob/master/LICENSE
//
// This notice must not be removed when duplicating this file or its contents, in whole or in part.

using Sphere10.Framework;
using JsonSubTypes;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;

namespace LocalNotion.Core;

[JsonConverter(typeof(JsonSubtypes), "type")]
[JsonSubtypes.KnownSubType(typeof(LocalNotionFile), LocalNotionResourceType.File)]
[JsonSubtypes.KnownSubType(typeof(LocalNotionPage), LocalNotionResourceType.Page)]
[JsonSubtypes.KnownSubType(typeof(LocalNotionDatabase), LocalNotionResourceType.Database)]
public abstract class LocalNotionResource {

	[JsonProperty("type")]
	[JsonConverter(typeof(StringEnumConverter))]
	public abstract LocalNotionResourceType Type { get; }

	[JsonProperty("id")]
	public string ID { get; set; }

	[JsonProperty("title")]
	public string Title { get; set; }

	[JsonProperty("parent_resource", NullValueHandling = NullValueHandling.Ignore)]
	public string ParentResourceID { get; set; }

	[JsonProperty("renders", NullValueHandling = NullValueHandling.Ignore)]
	public IDictionary<RenderType, RenderEntry> Renders { get; set; } = new Dictionary<RenderType, RenderEntry>();
	
	[JsonProperty("last_synced_on")]
	public DateTime LastSyncedOn { get; set; }

	public bool TryGetRender(RenderType? renderType, out RenderEntry render) {
		render = default;

		// No best match render found
		if (renderType == null) {
			if (Renders.Count <= 0)
				return false;
		
			// Get best match
			render = Renders.MinBy(x => x.Key).Value;
			return true;
		}

		// Specifically requested render not found
		if (!Renders.TryGetValue(renderType.Value, out render)) 
			return false;

		return true;

	}

	public RenderEntry GetRender(RenderType? renderType) {
		if (!TryGetRender(renderType, out var render))
			throw new InvalidOperationException($"No render found with type {renderType}");
		return render;
	}
}


public class RenderEntry {

	[JsonProperty("local_path")]
	public string LocalPath { get; set; }

	[JsonProperty("slug")]
	public string Slug { get; set; }

}
