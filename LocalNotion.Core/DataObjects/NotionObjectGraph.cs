// Copyright (c) Herman Schoenfeld 2018 - Present. All rights reserved. (https://sphere10.com/products/localnotion)
// Author: Herman Schoenfeld <herman@sphere10.com>
//
// Distributed under the GPLv3 software license, see the accompanying file LICENSE 
// or visit https://github.com/HermanSchoenfeld/localnotion/blob/master/LICENSE
//
// This notice must not be removed when duplicating this file or its contents, in whole or in part.

using Newtonsoft.Json;
using Notion.Client;

namespace LocalNotion.Core;

public class NotionObjectGraph {

	[JsonProperty("objectID")]
	public virtual string ObjectID { get; set; }

	[JsonProperty("children", NullValueHandling = NullValueHandling.Ignore)]
	public virtual NotionObjectGraph[] Children { get; set; } = Array.Empty<NotionObjectGraph>();

	public IEnumerable<NotionObjectGraph> VisitAll() {
		yield return this;
		foreach (var child in Children) {
			foreach (var childVal in child.VisitAll())
				yield return childVal;
		}
	}

}
