// Copyright (c) Herman Schoenfeld 2018 - Present. All rights reserved. (https://sphere10.com/products/localnotion)
// Author: Herman Schoenfeld <herman@sphere10.com>
//
// Distributed under the GPLv3 software license, see the accompanying file LICENSE
// or visit https://github.com/HermanSchoenfeld/localnotion/blob/master/LICENSE
//
// This notice must not be removed when duplicating this file or its contents, in whole or in part.

using Newtonsoft.Json;
using Newtonsoft.Json.Converters;

namespace LocalNotion.Core;

/// <summary>
/// An address a page or file used to have, kept in the repository registry so that links and search results
/// that still use it reach the current address. The repository records one whenever it changes an address
/// and drops it when the old address is used again. Hosting generators serve each redirect whose target is live.
/// </summary>
/// <remarks>
/// Addresses are site-relative slugs without a leading slash, as <see cref="CMSItem.Slug"/> and
/// <see cref="RenderEntry.Slug"/> store them. <see cref="CreatedOn"/> records when the address was retired,
/// so that old redirects can be culled later.
/// </remarks>
public class LocalNotionRedirect {

	[JsonProperty("from")]
	public string From { get; set; }

	[JsonProperty("to")]
	public string To { get; set; }

	[JsonProperty("type")]
	[JsonConverter(typeof(StringEnumConverter))]
	public RedirectType Type { get; set; }

	[JsonProperty("resource_id", NullValueHandling = NullValueHandling.Ignore)]
	public string ResourceID { get; set; }

	[JsonProperty("created_on")]
	public DateTime CreatedOn { get; set; }
}
